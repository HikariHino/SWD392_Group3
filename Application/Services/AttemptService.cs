using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities.Interviews;
using Domain.Enums;
namespace Application.Services;
public class AttemptService(IAttemptRepository repo, ICurrentUser user, TimeProvider clock) : IAttemptService
{
    void Student()
    {
        if (!user.IsStudent || user.Id is null) throw new ForbiddenException();
    }
    static DateTime Utc(DateTime time) => DateTime.SpecifyKind(time, DateTimeKind.Utc);
    static AttemptDto Dto(Transcript t, ExamSession s) => new(t.Id, t.SessionId, t.UserId, t.Status.ToString(), Utc(t.StartedAt), t.SubmittedAt is {} at ? Utc(at) : null, t.Version, Utc(s.StartTime), Utc(s.EndTime));
    static TurnDto Dto(TranscriptTurn t, ExamSession s) => new(t.Id, t.TranscriptId, t.Sequence, t.Speaker, t.ContentText, Utc(t.Timestamp), s.ExamSessionQuestions.SingleOrDefault(x => x.Id == t.ExamSessionQuestionId)?.QuestionId, t.ClientMessageId);
    async Task<ExamSession> Session(Guid id) => await repo.Session(id) ?? throw new NotFoundException("Session not found.");
    async Task<(Transcript, ExamSession)> Owned(Guid id, bool write = false)
    {
        if (write) Student();
        var t = await repo.Get(id) ?? throw new NotFoundException("Attempt not found.");
        if (!(user.IsStudent && user.Id == t.UserId) && !(user.IsLecturer && !write)) throw new ForbiddenException();
        var s = await Session(t.SessionId);
        if (user.IsLecturer && s.CreatedByUserId != user.Id) throw new ForbiddenException();
        return (t, s);
    }
    void Writable(ExamSession s)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (s.Status != ExamSessionStatus.Open || now < s.StartTime || now >= s.EndTime)
            throw new ConflictException("Session is not open within its exam window.");
    }
    public async Task<(AttemptDto Attempt, bool Created)> Start(Guid sessionId)
    {
        Student();
        var s = await Session(sessionId);
        var existing = await repo.Find(sessionId, user.Id!.Value);
        if (existing is { IsDeleted: true }) throw new ConflictException("Attempt is archived; a second attempt is not allowed.");
        if (existing != null) return (Dto(existing, s), false);
        Writable(s);
        if (s.ExamSessionQuestions.Count == 0 || s.ExamSessionQuestions.Any(x => string.IsNullOrWhiteSpace(x.Content)))
            throw new ConflictException("Session snapshot is incomplete.");
        var t = new Transcript { SessionId = sessionId, UserId = user.Id.Value, StartedAt = clock.GetUtcNow().UtcDateTime };
        // Every student write also updates the session token: close/cancel cannot commit
        // concurrently with a start/answer based on the old Open state.
        s.Version++;
        repo.Add(t); await repo.Save();
        return (Dto(t, s), true);
    }
    public async Task<AttemptDto> Get(Guid id)
    {
        var (attempt, session) = await Owned(id);
        return Dto(attempt, session);
    }
    public async Task<IReadOnlyList<AttemptQuestionDto>> Questions(Guid id)
    {
        var (_, s) = await Owned(id);
        return s.ExamSessionQuestions.OrderBy(x => x.DisplayOrder).Select(x => new AttemptQuestionDto(x.QuestionId, x.DisplayOrder, x.Content, x.Points)).ToArray();
    }
    public async Task<IReadOnlyList<TurnDto>> Turns(Guid id)
    {
        var (t, s) = await Owned(id);
        return t.Turns.OrderBy(x => x.Sequence).Select(x => Dto(x, s)).ToArray();
    }
    public async Task<(TurnDto Turn, bool Created)> Answer(Guid id, AnswerRequest request)
    {
        var (t, s) = await Owned(id, true);
        if (request.ClientMessageId == Guid.Empty || request.QuestionId == Guid.Empty || string.IsNullOrWhiteSpace(request.AnswerText) || request.AnswerText.Length > 20000)
            throw new ValidationException(new Dictionary<string, string[]> { ["Answer"] = ["Provide questionId, clientMessageId and 1 to 20000 characters of answer text."] });
        var snapshot = s.ExamSessionQuestions.SingleOrDefault(x => x.QuestionId == request.QuestionId)
            ?? throw new ValidationException(new Dictionary<string, string[]> { ["QuestionId"] = ["Question does not belong to this session."] });
        var existing = t.Turns.SingleOrDefault(x => x.ClientMessageId == request.ClientMessageId);
        if (existing != null)
        {
            if (existing.ContentText != request.AnswerText || existing.ExamSessionQuestionId != snapshot.Id)
                throw new ConflictException("clientMessageId was already used for a different answer.");
            return (Dto(existing, s), false);
        }
        Writable(s);
        if (t.Status != TranscriptStatus.InProgress) throw new ConflictException("Attempt no longer accepts answers.");
        var turn = new TranscriptTurn { TranscriptId = t.Id, ExamSessionQuestionId = snapshot.Id, Sequence = t.Turns.Select(x => x.Sequence).DefaultIfEmpty(0).Max() + 1, Speaker = "Student", ContentText = request.AnswerText, ClientMessageId = request.ClientMessageId, Timestamp = clock.GetUtcNow().UtcDateTime };
        repo.AddTurn(turn); t.Version++; s.Version++;
        await repo.Save();
        return (Dto(turn, s), true);
    }
    public async Task ValidateAnswerAsync(Guid id, Guid questionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (t, s) = await Owned(id, true);
        if (questionId == Guid.Empty || !s.ExamSessionQuestions.Any(x => x.QuestionId == questionId))
            throw new ValidationException(new Dictionary<string, string[]> { ["QuestionId"] = ["Question does not belong to this session."] });
        Writable(s);
        if (t.Status != TranscriptStatus.InProgress) throw new ConflictException("Attempt no longer accepts answers.");
    }
    public async Task<TurnDto?> FindExistingAnswerAsync(Guid id, Guid questionId, Guid clientMessageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (t, s) = await Owned(id, true);
        if (questionId == Guid.Empty || clientMessageId == Guid.Empty)
            throw new ValidationException(new Dictionary<string, string[]> { ["Request"] = ["questionId and clientMessageId are required."] });
        var snapshot = s.ExamSessionQuestions.SingleOrDefault(x => x.QuestionId == questionId)
            ?? throw new ValidationException(new Dictionary<string, string[]> { ["QuestionId"] = ["Question does not belong to this session."] });
        var existing = t.Turns.SingleOrDefault(x => x.ClientMessageId == clientMessageId);
        if (existing is null) return null;
        if (existing.Speaker != "Student" || existing.ExamSessionQuestionId != snapshot.Id)
            throw new ConflictException("clientMessageId was already used for a different answer.");
        return Dto(existing, s);
    }
    public async Task<AttemptDto> Submit(Guid id)
    {
        var (t, s) = await Owned(id, true);
        if (s.Status == ExamSessionStatus.Cancelled) throw new ConflictException("Session was cancelled.");
        if (t.Status != TranscriptStatus.InProgress) return Dto(t, s);
        if (s.Status != ExamSessionStatus.Open) throw new ConflictException("Session is not open.");
        // Submission is allowed after EndTime so a student can finish an expired attempt.
        t.Status = TranscriptStatus.Submitted; t.SubmittedAt = clock.GetUtcNow().UtcDateTime;
        t.Version++; s.Version++;
        await repo.Save(); return Dto(t, s);
    }
}
