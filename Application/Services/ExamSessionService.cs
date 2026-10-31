using System.Text.Json;
using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities.Interviews;
using Domain.Entities.QuestionBank;
using Domain.Enums;
namespace Application.Services;
public class ExamSessionService(IExamSessionRepository repo, ICurrentUser user, TimeProvider clock) : IExamSessionService
{
    static void Invalid(string field, string message) => throw new ValidationException(new Dictionary<string, string[]> { [field] = [message] });
    void Lecturer()
    {
        if (!user.IsLecturer || user.Id is null) throw new ForbiddenException();
    }
    async Task<ExamSession> Owned(Guid id)
    {
        Lecturer();
        var session = await repo.Get(id) ?? throw new NotFoundException("Session not found.");
        if (session.CreatedByUserId != user.Id) throw new ForbiddenException();
        return session;
    }
    static SessionDto Dto(ExamSession s) => new(s.Id, s.CourseId, s.SessionTitle, DateTime.SpecifyKind(s.StartTime, DateTimeKind.Utc), DateTime.SpecifyKind(s.EndTime, DateTimeKind.Utc), s.Status.ToString(), s.Version, s.CreatedByUserId, s.ExamSessionQuestions.OrderBy(x => x.DisplayOrder).Select(x => x.QuestionId).ToArray());
    public async Task<SessionDto> Get(Guid id) => Dto(await Owned(id));
    public async Task<SessionDto> Create(CreateSessionRequest request)
    {
        Lecturer();
        var title = request.SessionTitle?.Trim() ?? "";
        if (title.Length is 0 or > 200) Invalid("SessionTitle", "Title must contain 1 to 200 characters.");
        if (request.StartTime.Kind != DateTimeKind.Utc || request.EndTime.Kind != DateTimeKind.Utc || request.StartTime >= request.EndTime || request.EndTime <= clock.GetUtcNow().UtcDateTime)
            Invalid("EndTime", "Use UTC times with start before end and end in the future.");
        if (request.QuestionIds is null || request.QuestionIds.Count == 0 || request.QuestionIds.Distinct().Count() != request.QuestionIds.Count)
            Invalid("QuestionIds", "Select a nonempty list of distinct questions.");
        if (!await repo.CourseExists(request.CourseId)) throw new NotFoundException("Course not found.");
        var questions = await repo.Questions(request.QuestionIds!);
        ValidateQuestions(questions, request.QuestionIds!.Count, request.CourseId);
        var session = new ExamSession { CourseId = request.CourseId, CreatedByUserId = user.Id!.Value, SessionTitle = title, StartTime = request.StartTime, EndTime = request.EndTime };
        for (var i = 0; i < request.QuestionIds.Count; i++)
            session.ExamSessionQuestions.Add(new ExamSessionQuestion { QuestionId = request.QuestionIds[i], DisplayOrder = i + 1 });
        repo.Add(session);
        await repo.Save();
        return Dto(session);
    }
    static void ValidateQuestions(List<Question> questions, int count, Guid courseId)
    {
        if (questions.Count != count || questions.Any(x => x.CourseId != courseId)) Invalid("QuestionIds", "Questions must be active and belong to this course.");
        foreach (var q in questions)
        {
            var rubrics = q.Rubrics.Where(x => !x.IsDeleted).ToArray();
            if (string.IsNullOrWhiteSpace(q.Content) || q.Points <= 0 || rubrics.Length == 0 || rubrics.Any(x => string.IsNullOrWhiteSpace(x.Criteria) || !double.IsFinite(x.Weight) || !double.IsFinite(x.MaxScore) || x.Weight <= 0 || x.Weight > 100 || x.MaxScore <= 0 || x.MaxScore > 99999999999999d) || Math.Abs(rubrics.Sum(x => x.Weight) - 100) > 0.01)
                Invalid("QuestionIds", "Every question needs positive points and valid rubrics totaling 100 percent.");
        }
    }
    public async Task<SessionDto> Transition(Guid id, string action)
    {
        var s = await Owned(id);
        var now = clock.GetUtcNow().UtcDateTime;
        var next = action switch
        {
            "open" when s.Status == ExamSessionStatus.Draft => ExamSessionStatus.Open,
            "close" when s.Status == ExamSessionStatus.Open => ExamSessionStatus.Closed,
            "cancel" when s.Status is ExamSessionStatus.Draft or ExamSessionStatus.Open => ExamSessionStatus.Cancelled,
            _ => throw new ConflictException("Invalid session transition.")
        };
        if (next == ExamSessionStatus.Open)
        {
            if (s.StartTime >= s.EndTime || now >= s.EndTime) throw new ConflictException("Session has expired or its time range is invalid.");
            if (!await repo.CourseExists(s.CourseId)) throw new ConflictException("Course is no longer active.");
            var ids = s.ExamSessionQuestions.Select(x => x.QuestionId).ToArray();
            if (ids.Length == 0) Invalid("QuestionIds", "Session needs questions.");
            var questions = await repo.Questions(ids);
            ValidateQuestions(questions, ids.Length, s.CourseId);
            foreach (var snapshot in s.ExamSessionQuestions)
            {
                var q = questions.Single(x => x.Id == snapshot.QuestionId);
                snapshot.Content = q.Content; snapshot.ExpectedAnswer = q.ExpectedAnswer;
                snapshot.Points = q.Points; snapshot.BloomLevel = (int)q.BloomLevel; snapshot.AiModel = q.AiModel;
                snapshot.RubricSnapshotJson = JsonSerializer.Serialize(q.Rubrics.Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => new { rubricId = x.Id, criteria = x.Criteria, weight = (decimal)x.Weight, maxScore = (decimal)x.MaxScore, aiGradingPrompt = x.AiGradingPrompt }));
            }
        }
        if (next == ExamSessionStatus.Closed)
            foreach (var attempt in s.Transcripts.Where(x => x.Status == TranscriptStatus.InProgress))
            {
                attempt.Status = TranscriptStatus.Submitted; attempt.SubmittedAt = now; attempt.Version++;
            }
        s.Status = next; s.Version++;
        await repo.Save();
        return Dto(s);
    }
}
