namespace Application.Interfaces.ExternalServices;

public sealed record SpeechAudio(byte[] Content, string ContentType, string FileName);

public interface ITextToSpeechService
{
    Task<SpeechAudio> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
}
