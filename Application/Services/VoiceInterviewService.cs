using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Application.Interfaces.Services;

namespace Application.Services;

public sealed class VoiceInterviewService(
    ICurrentUser currentUser,
    IAttemptService attempts,
    IInterviewService interviews,
    ISpeechToTextService speechToText,
    ITextToSpeechService textToSpeech) : IVoiceInterviewService
{
    public async Task<VoiceAnswerDto> AnswerAsync(Guid attemptId, Guid questionId, Guid clientMessageId, SpeechAudioClip audio, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsStudent || currentUser.Id is null) throw new ForbiddenException();
        if (attemptId == Guid.Empty || questionId == Guid.Empty || clientMessageId == Guid.Empty)
            throw new ValidationException(new Dictionary<string, string[]> { ["Request"] = ["attemptId, questionId and clientMessageId are required."] });

        // Idempotent retries return persisted transcript data without another provider upload.
        var previous = await attempts.FindExistingAnswerAsync(attemptId, questionId, clientMessageId, cancellationToken);
        if (previous is not null)
        {
            var turns = await attempts.Turns(attemptId);
            var nextStudent = turns.Where(x => x.Speaker == "Student" && x.Sequence > previous.Sequence).Select(x => x.Sequence).DefaultIfEmpty(int.MaxValue).Min();
            var savedAi = turns.Where(x => x.Speaker == "AI" && x.Sequence > previous.Sequence && x.Sequence < nextStudent).OrderBy(x => x.Sequence).FirstOrDefault();
            var retryAiTurn = savedAi is null
                ? await interviews.GenerateFollowUpForStudentAsync(attemptId, currentUser.Id.Value, cancellationToken)
                : new FollowUpTurnDto(savedAi.Id, attemptId, savedAi.Sequence, savedAi.ContentText, savedAi.Timestamp, questionId);
            return await WithSpeech(previous.ContentText, previous, retryAiTurn, cancellationToken);
        }
        // Validate ownership/session state before sending potentially sensitive audio to an external service.
        await attempts.ValidateAnswerAsync(attemptId, questionId, cancellationToken);
        var transcription = await speechToText.TranscribeAsync(audio, cancellationToken);
        if (string.IsNullOrWhiteSpace(transcription.Text) || transcription.Text.Length > 20000)
            throw new ValidationException(new Dictionary<string, string[]> { ["Audio"] = ["Speech recognition did not produce a usable answer (1 to 20000 characters)."] });

        var (studentTurn, created) = await attempts.Answer(attemptId, new AnswerRequest
        {
            QuestionId = questionId,
            ClientMessageId = clientMessageId,
            AnswerText = transcription.Text.Trim()
        });
        if (!created)
            return new VoiceAnswerDto(studentTurn.ContentText, studentTurn, null, null, null, false);

        var aiTurn = await interviews.GenerateFollowUpForStudentAsync(attemptId, currentUser.Id.Value, cancellationToken);
        return await WithSpeech(transcription.Text.Trim(), studentTurn, aiTurn, cancellationToken);
    }

    async Task<VoiceAnswerDto> WithSpeech(string transcript, TurnDto studentTurn, FollowUpTurnDto aiTurn, CancellationToken cancellationToken)
    {
        try
        {
            var audioOutput = await textToSpeech.SynthesizeAsync(aiTurn.ContentText, cancellationToken);
            return new VoiceAnswerDto(transcript, studentTurn, aiTurn, Convert.ToBase64String(audioOutput.Content), audioOutput.ContentType, false);
        }
        catch (AiProviderException) when (!cancellationToken.IsCancellationRequested)
        {
            return new VoiceAnswerDto(transcript, studentTurn, aiTurn, null, null, true);
        }
    }
}
