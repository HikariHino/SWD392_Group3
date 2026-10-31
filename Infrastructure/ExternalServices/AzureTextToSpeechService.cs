using System.Net;
using System.Security;
using System.Text;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.ExternalServices;

public sealed class AzureTextToSpeechService(HttpClient http, IConfiguration config) : ITextToSpeechService
{
    public async Task<SpeechAudio> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]> { ["Text"] = ["Provide 1 to 2000 characters of text."] });
        var options = AzureSpeechOptions.Load(config);
        if (string.IsNullOrWhiteSpace(options.SubscriptionKey) || string.IsNullOrWhiteSpace(options.Region))
            throw new AiProviderUnavailableException("Azure Speech is not configured.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120)));
        var endpoint = $"https://{options.Region}.tts.speech.microsoft.com/cognitiveservices/v1";
        var safeText = SecurityElement.Escape(text) ?? "";
        var ssml = $"<speak version=\"1.0\" xml:lang=\"{System.Security.SecurityElement.Escape(options.Locale)}\"><voice name=\"{System.Security.SecurityElement.Escape(options.Voice)}\">{safeText}</voice></speak>";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml") };
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
        request.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", options.OutputFormat);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) throw new AiProviderUnavailableException("Azure Speech is temporarily unavailable.");
                throw new AiProviderResponseException();
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            if (bytes.Length == 0 || bytes.Length > 5 * 1024 * 1024) throw new AiProviderResponseException();
            var (contentType, extension) = options.OutputFormat.ToLowerInvariant() switch
            {
                var format when format.StartsWith("riff-", StringComparison.Ordinal) => ("audio/wav", ".wav"),
                var format when format.StartsWith("audio-", StringComparison.Ordinal) && format.EndsWith("-mp3", StringComparison.Ordinal) => ("audio/mpeg", ".mp3"),
                var format when format.StartsWith("ogg-", StringComparison.Ordinal) => ("audio/ogg", ".ogg"),
                var format when format.StartsWith("webm-", StringComparison.Ordinal) => ("audio/webm", ".webm"),
                var format when format.StartsWith("raw-", StringComparison.Ordinal) => ("audio/pcm", ".pcm"),
                _ => throw new AiProviderResponseException()
            };
            return new SpeechAudio(bytes, contentType, "response" + extension);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new AiProviderTimeoutException(); }
        catch (HttpRequestException) { throw new AiProviderUnavailableException("Azure Speech could not be reached."); }
    }
}
