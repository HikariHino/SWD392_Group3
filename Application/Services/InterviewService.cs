using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities.Interviews;
using Domain.Enums;

namespace Application.Services;

public sealed class InterviewService(IAttemptRepository attempts, IAiService ai, ICurrentUser user, TimeProvider clock) : IInterviewService
{
    public async Task<FollowUpTurnDto> GenerateFollowUpAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        if (!user.IsStudent || user.Id is null) throw new ForbiddenException();
        return await GenerateForStudentAsync(attemptId, user.Id.Value, cancellationToken);
    }

    public Task<FollowUpTurnDto> GenerateFollowUpForStudentAsync(Guid attemptId, Guid studentId, CancellationToken cancellationToken = default)
    {
        if (studentId == Guid.Empty) throw new ForbiddenException();
        return GenerateForStudentAsync(attemptId, studentId, cancellationToken);
    }

    private async Task<FollowUpTurnDto> GenerateForStudentAsync(Guid attemptId, Guid studentId, CancellationToken cancellationToken)
    {
        var attempt = await attempts.Get(attemptId) ?? throw new NotFoundException("Attempt not found.");
        if (attempt.UserId != studentId) throw new ForbiddenException();
        var session = await attempts.Session(attempt.SessionId) ?? throw new NotFoundException("Session not found.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (attempt.Status != TranscriptStatus.InProgress || session.Status != ExamSessionStatus.Open || now < session.StartTime || now >= session.EndTime)
            throw new ConflictException("Attempt does not currently accept an AI follow-up.");

        var lastStudentTurn = attempt.Turns.Where(x => x.Speaker == "Student" && x.ExamSessionQuestionId.HasValue)
            .OrderByDescending(x => x.Sequence).FirstOrDefault()
            ?? throw new ValidationException(new Dictionary<string, string[]> { ["Attempt"] = ["Save a student answer before requesting a follow-up."] });
        var question = session.ExamSessionQuestions.SingleOrDefault(x => x.Id == lastStudentTurn.ExamSessionQuestionId)
            ?? throw new ConflictException("The answer does not reference this session's question snapshot.");

        var conversation = attempt.Turns.OrderBy(x => x.Sequence)
            .Select(x => new AiConversationTurn(x.Speaker, x.ContentText)).ToArray();
        var response = await ai.GenerateFollowUpAsync(new AiFollowUpRequest
        {
            CourseName = session.Course?.Name ?? session.CourseId.ToString(),
            QuestionContent = question.Content,
            RubricSnapshotJson = question.RubricSnapshotJson,
            Conversation = conversation
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Content) || response.Content.Length > 2000)
            throw new AiProviderResponseException();
        var turn = new TranscriptTurn
        {
            TranscriptId = attempt.Id,
            ExamSessionQuestionId = question.Id,
            Sequence = attempt.Turns.Select(x => x.Sequence).DefaultIfEmpty(0).Max() + 1,
            Speaker = "AI",
            ContentText = response.Content.Trim(),
            Timestamp = clock.GetUtcNow().UtcDateTime
        };
        attempts.AddTurn(turn);
        attempt.Version++;
        session.Version++;
        await attempts.Save();
        return new FollowUpTurnDto(turn.Id, attempt.Id, turn.Sequence, turn.ContentText, turn.Timestamp, question.QuestionId);
    }
}
