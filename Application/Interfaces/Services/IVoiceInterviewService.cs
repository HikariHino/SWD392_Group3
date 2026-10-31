using Application.DTOs.Interviews;
using Application.Interfaces.ExternalServices;

namespace Application.Interfaces.Services;

public interface IVoiceInterviewService
{
    Task<VoiceAnswerDto> AnswerAsync(Guid attemptId, Guid questionId, Guid clientMessageId, SpeechAudioClip audio, CancellationToken cancellationToken = default);
}
