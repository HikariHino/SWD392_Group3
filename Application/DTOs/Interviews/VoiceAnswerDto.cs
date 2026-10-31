namespace Application.DTOs.Interviews;

public sealed record VoiceAnswerDto(
    string Transcript,
    TurnDto StudentTurn,
    FollowUpTurnDto? AiTurn,
    string? AudioBase64,
    string? AudioContentType,
    bool SpeechOutputUnavailable);
