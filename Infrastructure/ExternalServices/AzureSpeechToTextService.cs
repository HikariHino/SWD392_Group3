using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.ExternalServices;

public sealed class AzureSpeechToTextService(HttpClient http, IConfiguration config) : ISpeechToTextService
{
    private const int MaxAudioBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> Extensions = [".wav", ".mp3", ".ogg", ".webm", ".mp4"];

    public async Task<SpeechTranscription> TranscribeAsync(SpeechAudioClip audio, CancellationToken cancellationToken = default)
    {
        if (audio.Content is not { Length: > 0 and <= MaxAudioBytes } || !MediaTypeHeaderValue.TryParse(audio.ContentType, out _) || !ValidAudio(audio))
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]> { ["Audio"] = ["Upload a valid WAV, MP3, OGG, WebM or MP4 audio file up to 10 MiB."] });
        var options = AzureSpeechOptions.Load(config);
        if (!Uri.TryCreate(options.SpeechToTextEndpoint, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(options.SubscriptionKey))
            throw new AiProviderUnavailableException("Azure Speech is not configured.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120)));
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(audio.Content);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(audio.ContentType);
        form.Add(content, "audio", Path.GetFileName(audio.FileName));
        form.Add(new StringContent(JsonSerializer.Serialize(new { locales = new[] { options.Locale } })), "definition");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "speechtotext/transcriptions:transcribe?api-version=2025-10-15")) { Content = form };
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw MapFailure(response.StatusCode);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var root = json.RootElement;
            var text = ReadTexts(root, "combinedPhrases");
            if (string.IsNullOrWhiteSpace(text)) text = ReadTexts(root, "phrases");
            if (string.IsNullOrWhiteSpace(text)) throw new AiProviderResponseException();
            return new SpeechTranscription(text.Trim(), options.Locale);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new AiProviderTimeoutException(); }
        catch (HttpRequestException) { throw new AiProviderUnavailableException("Azure Speech could not be reached."); }
        catch (JsonException) { throw new AiProviderResponseException(); }
    }

    static string ReadTexts(JsonElement root, string property) => root.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array
        ? string.Join(" ", items.EnumerateArray().Select(x => x.TryGetProperty("text", out var text) ? text.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)))
        : "";

    static Exception MapFailure(HttpStatusCode status) => (int)status == 429 || (int)status >= 500
        ? new AiProviderUnavailableException("Azure Speech is temporarily unavailable.")
        : new AiProviderResponseException();

    static bool ValidAudio(SpeechAudioClip audio)
    {
        var ext = Path.GetExtension(audio.FileName).ToLowerInvariant();
        if (!Extensions.Contains(ext)) return false;
        var b = audio.Content;
        return ext switch
        {
            ".wav" => b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WAVE"u8),
            ".ogg" => b.AsSpan().StartsWith("OggS"u8),
            ".webm" => b.Length >= 4 && b.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            ".mp4" => b.Length >= 8 && b.AsSpan(4, 4).SequenceEqual("ftyp"u8),
            ".mp3" => b.AsSpan().StartsWith("ID3"u8) || (b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0),
            _ => false
        };
    }
}
