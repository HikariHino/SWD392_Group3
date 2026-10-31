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

    public async Task<AiGeneratedQuestionDto> GenerateQuestionAsync(
        AiGenerateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
            throw new ValidationException(new Dictionary<string, string[]> { [nameof(request.Topic)] = ["Topic is required."] });

        var prompt = $"You generate exactly one university oral-exam question.\n" +
            $"Topic: {request.Topic}\nBloom level: {request.BloomLevel}\n" +
            "Return JSON only, matching this schema:\n" +
            "{ \"content\": \"nonempty question\", \"sourceContext\": \"optional source context\", \"aiConfidence\": 0.0 }\n" +
            "Do not generate a rubric: rubrics are configured once for the whole course.";
        var candidateText = await GenerateCandidateTextAsync(prompt, cancellationToken);
        return ParseQuestion(candidateText, request);
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
            "Return JSON only: { \"content\": \"nonempty follow-up question\", \"sourceContext\": \"optional\" }. Do not provide answers, scores, feedback, or rubric changes.";
        var candidateText = await GenerateCandidateTextAsync(prompt, cancellationToken);
        try
        {
            var generated = JsonSerializer.Deserialize<GeneratedFollowUp>(candidateText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (generated is null || string.IsNullOrWhiteSpace(generated.Content) || generated.Content.Trim().Length > 2000)
                throw new JsonException();
            return new AiFollowUpDto { Content = generated.Content.Trim(), SourceContext = generated.SourceContext?.Trim() ?? string.Empty };
        }
        catch (JsonException) { throw new AiProviderResponseException(); }
    }

    private async Task<string> GenerateCandidateTextAsync(string prompt, CancellationToken cancellationToken)
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
            generationConfig = new { responseMimeType = "application/json", temperature = 0.2 }
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
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
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
            return ExtractCandidateText(body);
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
            var generated = JsonSerializer.Deserialize<GeneratedQuestion>(candidateText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
}
