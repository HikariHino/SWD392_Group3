using System.Net;
using System.Text;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;

internal static class SpeechProviderChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var values = new Dictionary<string, string?>
        {
            ["AzureSpeech:SubscriptionKey"] = "test-key",
            ["AzureSpeech:SpeechToTextEndpoint"] = "https://speech.test/",
            ["AzureSpeech:Region"] = "southeastasia",
            ["AzureSpeech:Locale"] = "vi-VN",
            ["AzureSpeech:Voice"] = "vi-VN-HoaiMyNeural",
            ["AzureSpeech:TimeoutSeconds"] = "1"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var audio = new SpeechAudioClip([.. "RIFF0000WAVEdata"u8.ToArray()], "answer.wav", "audio/wav");
        var successful = new StubHandler((request, _) =>
        {
            check(request.Headers.Contains("Ocp-Apim-Subscription-Key"), "STT sends subscription key header");
            check(request.RequestUri!.Query.Contains("2025-10-15"), "STT uses current fast transcription contract");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"combinedPhrases\":[{\"text\":\"Xin chào\"}]}") });
        });
        var stt = new AzureSpeechToTextService(new HttpClient(successful), config);
        var transcript = await stt.TranscribeAsync(audio);
        check(transcript.Text == "Xin chào" && transcript.Locale == "vi-VN", "Vietnamese transcription parsed");

        var invalid = false;
        try { await stt.TranscribeAsync(new SpeechAudioClip([1, 2, 3], "answer.wav", "audio/wav")); }
        catch (Application.Exceptions.ValidationException) { invalid = true; }
        check(invalid && successful.Calls == 1, "invalid audio rejected before provider request");
        var invalidType = false;
        try { await stt.TranscribeAsync(audio with { ContentType = "not a media type" }); }
        catch (Application.Exceptions.ValidationException) { invalidType = true; }
        check(invalidType && successful.Calls == 1, "invalid audio content type rejected before provider request");

        var failingStt = new AzureSpeechToTextService(new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))), config);
        var unavailable = false;
        try { await failingStt.TranscribeAsync(audio); } catch (AiProviderUnavailableException) { unavailable = true; }
        check(unavailable, "STT provider outage mapped to unavailable");

        var timeoutStt = new AzureSpeechToTextService(new HttpClient(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })), config);
        var timedOut = false;
        try { await timeoutStt.TranscribeAsync(audio); } catch (AiProviderTimeoutException) { timedOut = true; }
        check(timedOut, "STT timeout mapped to provider timeout");

        var ttsHandler = new StubHandler((request, _) =>
        {
            check(request.Headers.Contains("X-Microsoft-OutputFormat"), "TTS declares frontend audio format");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x52, 0x49, 0x46, 0x46]) });
        });
        var tts = new AzureTextToSpeechService(new HttpClient(ttsHandler), config);
        var synthesized = await tts.SynthesizeAsync("A < B & C");
        check(synthesized.ContentType == "audio/wav" && synthesized.Content.SequenceEqual(new byte[] { 0x52, 0x49, 0x46, 0x46 }), "TTS returns WAV bytes and MIME type");
        check(ttsHandler.LastBody!.Contains("A &lt; B &amp; C", StringComparison.Ordinal), "TTS escapes text in SSML");
        var mp3Config = new ConfigurationBuilder().AddInMemoryCollection(values.Concat(new[] { new KeyValuePair<string, string?>("AzureSpeech:OutputFormat", "audio-24khz-48kbitrate-mono-mp3") })).Build();
        var mp3 = await new AzureTextToSpeechService(new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) }))), mp3Config).SynthesizeAsync("hello");
        check(mp3.ContentType == "audio/mpeg" && mp3.FileName.EndsWith(".mp3", StringComparison.Ordinal), "TTS maps configured MP3 format to correct frontend metadata");

        var missingConfig = new AzureTextToSpeechService(new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))), new ConfigurationBuilder().Build());
        var notConfigured = false;
        try { await missingConfig.SynthesizeAsync("hello"); } catch (AiProviderUnavailableException) { notConfigured = true; }
        check(notConfigured, "TTS missing credentials produce safe unavailable error");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.Content is not null) LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return await send(request, cancellationToken);
        }
    }
}
