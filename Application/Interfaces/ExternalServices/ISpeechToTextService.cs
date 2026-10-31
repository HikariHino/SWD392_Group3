namespace Application.Interfaces.ExternalServices;

public sealed record SpeechAudioClip(byte[] Content, string FileName, string ContentType);
public sealed record SpeechTranscription(string Text, string Locale);

public interface ISpeechToTextService
{
    Task<SpeechTranscription> TranscribeAsync(SpeechAudioClip audio, CancellationToken cancellationToken = default);
}
