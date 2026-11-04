using System.Net;
using System.Text;
using System.Text.Json;
using Application.DTOs.QuestionBank;
using Application.DTOs.Interviews;
using Application.Exceptions;
using Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class AiProviderChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        AiGenerateQuestionRequest Request() => new() { CourseId = Guid.NewGuid(), Topic = "Onion Architecture" };
        IConfiguration Config(params (string Key, string Value)[] values) => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();
        GeminiService Service(HttpMessageHandler handler, IConfiguration config) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, config, NullLogger<GeminiService>.Instance);
        async Task Reject<T>(Func<Task> operation, string name) where T : Exception
        {
            try { await operation(); check(false, name); }
            catch (T) { check(true, name); }
        }

        await Reject<AiProviderUnavailableException>(() => Service(new Handler(_ => new(HttpStatusCode.OK)), Config()).GenerateQuestionAsync(Request()), "missing AI key is unavailable");
        string? requestUri = null;
        string? body = null;
        var success = await Service(new Handler(async request =>
        {
            requestUri = request.RequestUri!.ToString();
            body = await request.Content!.ReadAsStringAsync();
            return Json(Envelope("""{"content":"Explain dependency direction","sourceContext":"Course notes","aiConfidence":91.5}"""));
        }), Config(("Gemini:ApiKey", "test-key"))).GenerateQuestionAsync(Request());
        check(success.Content == "Explain dependency direction" && success.AiConfidence == 91.5, "structured Gemini response maps to question DTO");
        var promptText = JsonDocument.Parse(body!).RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
        check(requestUri!.Contains("generateContent?key=test-key") && promptText!.Contains("Không tạo rubric") && body!.Contains("responseJsonSchema"), "Gemini request uses configured JSON schema and Vietnamese course-level rubric prompt");
        var fenced = await Service(new Handler(_ => Json(Envelope("```json\n{\"content\":\"Explain DI\",\"aiConfidence\":80}\n```"))), Config(("Gemini:ApiKey", "test-key"))).GenerateQuestionAsync(Request());
        check(fenced.Content == "Explain DI" && fenced.AiConfidence == 80, "fenced JSON candidate remains usable");
        string? followUpBody = null;
        var followUp = await Service(new Handler(async request =>
        {
            followUpBody = await request.Content!.ReadAsStringAsync();
            return Json(Envelope("""{"content":"Why does that boundary matter?","sourceContext":"Exam context"}"""));
        }), Config(("Gemini:ApiKey", "test-key"))).GenerateFollowUpAsync(new AiFollowUpRequest
        {
            CourseName = "Architecture", QuestionContent = "Explain Onion", RubricSnapshotJson = "[]",
            Conversation = [new AiConversationTurn("Student", "It separates concerns.")]
        });
        check(followUp.Content.StartsWith("Why does") && followUpBody!.Contains("It separates concerns"), "structured Gemini follow-up uses persisted conversation context");
        var criterionId = Guid.NewGuid();
        var assessmentText = JsonSerializer.Serialize(new
        {
            rubricScores = new[] { new { rubricId = criterionId, earnedScore = 3m } },
            feedback = "Good explanation.",
            confidenceLevel = 0.8m
        });
        var assessment = await Service(new Handler(_ => Json(Envelope(assessmentText))), Config(("Gemini:ApiKey", "test-key"))).GenerateAssessmentAsync(new AiAssessmentRequest
        {
            QuestionContent = "Explain DIP", StudentAnswer = "Depend on abstractions.",
            Rubric = [new RubricSnapshotCriterion(criterionId, "Accuracy", 100m, 10m, null)]
        });
        check(assessment.RubricScores.Single().EarnedScore == 3m && assessment.ConfidenceLevel == 0.8m,
            "structured Gemini assessment maps rubric scores");
        await Reject<AiProviderResponseException>(() => Service(new Handler(_ => Json("{}")), Config(("Gemini:ApiKey", "test-key"))).GenerateQuestionAsync(Request()), "malformed provider envelope rejected");
        await Reject<AiProviderResponseException>(() => Service(new Handler(_ => Json(Envelope("{}"))), Config(("Gemini:ApiKey", "test-key"))).GenerateQuestionAsync(Request()), "invalid structured output rejected");
        await Reject<AiProviderUnavailableException>(() => Service(new Handler(_ => new(HttpStatusCode.TooManyRequests)), Config(("Gemini:ApiKey", "test-key"))).GenerateQuestionAsync(Request()), "provider quota failure is unavailable");
        await Reject<AiProviderTimeoutException>(() => Service(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new(HttpStatusCode.OK);
        }), Config(("Gemini:ApiKey", "test-key"), ("Gemini:TimeoutSeconds", "1"))).GenerateQuestionAsync(Request()), "provider timeout is classified");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await Service(new Handler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Json("{}");
            }), Config(("Gemini:ApiKey", "test-key"))).GenerateQuestionAsync(Request(), cancellation.Token);
            check(false, "caller cancellation propagates");
        }
        catch (OperationCanceledException) { check(true, "caller cancellation propagates"); }
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string Envelope(string text) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text } } } } }
    });

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
        public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : this((request, _) => Task.FromResult(respond(request))) { }
        public Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : this((request, _) => respond(request)) { }
        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _respond(request, cancellationToken);
    }
}
