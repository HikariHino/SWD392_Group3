using System.Net;
using System.Text;
using System.Text.Json;
using Application.DTOs.QuestionBank;
using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ExternalServices;

/// <summary>Gemini REST adapter. Secrets are supplied by IConfiguration (for example Gemini__ApiKey), never by source files.</summary>
public sealed class GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger) : IAiService
{
    private const int DefaultTimeoutSeconds = 20;
    private static readonly object QuestionSchema = new
    {
        type = "object",
        properties = new
        {
            content = new { type = "string" },
            sourceContext = new { type = "string" },
            aiConfidence = new { type = "number" }
        },
        required = new[] { "content", "aiConfidence" },
        additionalProperties = false
    };
    private static readonly object FollowUpSchema = new
    {
        type = "object",
        properties = new { content = new { type = "string" }, sourceContext = new { type = "string" } },
        required = new[] { "content" },
        additionalProperties = false
    };
    private static readonly object AssessmentSchema = new
    {
        type = "object",
        properties = new
        {
            rubricScores = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new { rubricId = new { type = "string" }, earnedScore = new { type = "number" } },
                    required = new[] { "rubricId", "earnedScore" },
                    additionalProperties = false
                }
            },
            feedback = new { type = "string" },
            confidenceLevel = new { type = "number" }
        },
        required = new[] { "rubricScores", "feedback", "confidenceLevel" },
        additionalProperties = false
    };

    public async Task<AiGeneratedQuestionDto> GenerateQuestionAsync(
        AiGenerateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
            throw new ValidationException(new Dictionary<string, string[]> { [nameof(request.Topic)] = ["Topic is required."] });

        var prompt = $"Hãy tạo đúng một câu hỏi vấn đáp bậc đại học bằng tiếng Việt.\n" +
            $"Chủ đề: {request.Topic}\nMức Bloom: {request.BloomLevel}\n" +
            "Trường content và sourceContext (nếu có) phải viết bằng tiếng Việt tự nhiên. " +
            "Chỉ trả về JSON theo đúng response schema, không bọc trong Markdown và không thêm bất kỳ văn bản nào khác.\n" +
            "Không tạo rubric vì rubric được cấu hình một lần ở cấp môn học.";
        var candidateText = await GenerateCandidateTextAsync(prompt, QuestionSchema, cancellationToken);
        try { return ParseQuestion(candidateText, request); }
        catch (AiProviderResponseException ex)
        {
            logger.LogWarning(ex, "Gemini returned a question that did not match the requested schema.");
            throw;
        }
    }

    public async Task<AiFollowUpDto> GenerateFollowUpAsync(AiFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionContent) || request.Conversation.Count == 0)
            throw new AiProviderResponseException();
        var history = string.Join("\n", request.Conversation.TakeLast(12).Select(x =>
            $"{x.Speaker}: {x.ContentText[..Math.Min(x.ContentText.Length, 1000)]}"));
        var prompt = "You are a university oral-exam examiner. Generate one concise follow-up question only.\n" +
            $"Course: {request.CourseName}\nQuestion being assessed: {request.QuestionContent}\n" +
            $"Course rubric snapshot: {request.RubricSnapshotJson}\nConversation:\n{history}\n" +
            "Return JSON only. Follow the response schema exactly; do not wrap it in Markdown. Do not provide answers, scores, feedback, or rubric changes.";
        var candidateText = await GenerateCandidateTextAsync(prompt, FollowUpSchema, cancellationToken);
        try
        {
            var generated = JsonSerializer.Deserialize<GeneratedFollowUp>(candidateText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (generated is null || string.IsNullOrWhiteSpace(generated.Content) || generated.Content.Trim().Length > 2000)
                throw new JsonException();
            return new AiFollowUpDto { Content = generated.Content.Trim(), SourceContext = generated.SourceContext?.Trim() ?? string.Empty };
        }
        catch (JsonException) { throw new AiProviderResponseException(); }
    }

    public async Task<AiAssessmentResponse> GenerateAssessmentAsync(AiAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionContent) || string.IsNullOrWhiteSpace(request.StudentAnswer) || request.Rubric.Count == 0)
            throw new AiProviderResponseException();
        var rubric = JsonSerializer.Serialize(request.Rubric.Select(x => new { rubricId = x.RubricId, criteria = x.Criteria, maxScore = x.MaxScore, weight = x.Weight }));
        var prompt = "Grade this university oral-exam answer. Return JSON only.\n" +
            $"Question: {request.QuestionContent}\nExpected answer: {request.ExpectedAnswer}\nStudent answer: {request.StudentAnswer}\nRubric: {rubric}\n" +
            "Follow the response schema exactly; do not wrap it in Markdown. Return each rubricId exactly once. confidenceLevel must be from 0 to 1.";
        var candidateText = await GenerateCandidateTextAsync(prompt, AssessmentSchema, cancellationToken);
        try
        {
            var generated = JsonSerializer.Deserialize<GeneratedAssessment>(candidateText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (generated?.RubricScores is null || generated.RubricScores.Count == 0 || generated.Feedback is null || generated.Feedback.Length > 4000 ||
                generated.ConfidenceLevel is < 0m or > 1m)
                throw new JsonException();
            return new AiAssessmentResponse { RubricScores = generated.RubricScores, Feedback = generated.Feedback.Trim(), ConfidenceLevel = generated.ConfidenceLevel };
        }
        catch (JsonException) { throw new AiProviderResponseException(); }
    }

    private async Task<string> GenerateCandidateTextAsync(string prompt, object responseJsonSchema, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_GEMINI", StringComparison.Ordinal))
            throw new AiProviderUnavailableException("Gemini API key is not configured.");
        var model = configuration["Gemini:Model"] ?? "gemini-1.5-flash";
        var timeoutSeconds = int.TryParse(configuration["Gemini:TimeoutSeconds"], out var configuredTimeout) ? configuredTimeout : DefaultTimeoutSeconds;
        if (timeoutSeconds is < 1 or > 120) timeoutSeconds = DefaultTimeoutSeconds;
        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", responseJsonSchema, temperature = 0.2 }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderTimeoutException();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Gemini request failed before a response.");
            throw new AiProviderUnavailableException("Gemini could not be reached.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("Gemini quota or rate limit reached: {StatusCode}", (int)response.StatusCode);
                throw new AiProviderUnavailableException("Gemini quota/rate limit has been reached. Try again later or use a billed API project.");
            }
            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                logger.LogWarning("Gemini temporary failure: {StatusCode}", (int)response.StatusCode);
                throw new AiProviderUnavailableException("Gemini is temporarily unavailable.");
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini rejected the request: {StatusCode}", (int)response.StatusCode);
                throw new AiProviderResponseException();
            }

            string body;
            try { body = await response.Content.ReadAsStringAsync(linked.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new AiProviderTimeoutException(); }
            try { return ExtractCandidateText(body); }
            catch (AiProviderResponseException ex)
            {
                logger.LogWarning(ex, "Gemini response did not contain a usable text candidate.");
                throw;
            }
        }
    }

    private static string ExtractCandidateText(string body)
    {
        try
        {
            using var envelope = JsonDocument.Parse(body);
            var candidateText = envelope.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(candidateText)) throw new JsonException();
            return candidateText;
        }
        catch (JsonException) { throw new AiProviderResponseException(); }
        catch (KeyNotFoundException) { throw new AiProviderResponseException(); }
        catch (IndexOutOfRangeException) { throw new AiProviderResponseException(); }
    }

    private static AiGeneratedQuestionDto ParseQuestion(string candidateText, AiGenerateQuestionRequest request)
    {
        try
        {
            var generated = JsonSerializer.Deserialize<GeneratedQuestion>(NormalizeJson(candidateText), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (generated is null || string.IsNullOrWhiteSpace(generated.Content) || generated.Content.Trim().Length > 2000 ||
                !double.IsFinite(generated.AiConfidence) || generated.AiConfidence is < 0 or > 100)
                throw new JsonException();
            return new AiGeneratedQuestionDto
            {
                CourseId = request.CourseId,
                Content = generated.Content.Trim(),
                BloomLevel = request.BloomLevel,
                SourceContext = generated.SourceContext?.Trim() ?? string.Empty,
                AiConfidence = generated.AiConfidence
            };
        }
        catch (JsonException) { throw new AiProviderResponseException(); }
        catch (KeyNotFoundException) { throw new AiProviderResponseException(); }
        catch (IndexOutOfRangeException) { throw new AiProviderResponseException(); }
    }

    // Older/provider-edge responses may still use a Markdown fence despite responseMimeType.
    // Accept that harmless presentation wrapper without accepting arbitrary prose around JSON.
    private static string NormalizeJson(string candidateText)
    {
        var trimmed = candidateText.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0 || !trimmed.EndsWith("```", StringComparison.Ordinal)) return trimmed;
        return trimmed[(firstLineEnd + 1)..^3].Trim();
    }

    private sealed class GeneratedQuestion
    {
        public string? Content { get; set; }
        public string? SourceContext { get; set; }
        public double AiConfidence { get; set; }
    }

    private sealed class GeneratedFollowUp
    {
        public string? Content { get; set; }
        public string? SourceContext { get; set; }
    }

    private sealed class GeneratedAssessment
    {
        public List<RubricEarnedScore>? RubricScores { get; set; }
        public string? Feedback { get; set; }
        public decimal? ConfidenceLevel { get; set; }
    }
}
