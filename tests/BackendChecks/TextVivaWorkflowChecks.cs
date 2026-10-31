using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Entities.Interviews;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

internal static class TextVivaWorkflowChecks
{
    private sealed class Identity(Guid id) : ICurrentUser
    {
        public Guid? Id => id;
        public bool IsLecturer => false;
        public bool IsStudent => true;
    }
    private sealed class WorkflowAi(bool fail = false) : IAiService
    {
        public Task<Application.DTOs.QuestionBank.AiGeneratedQuestionDto> GenerateQuestionAsync(Application.DTOs.QuestionBank.AiGenerateQuestionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiFollowUpDto> GenerateFollowUpAsync(AiFollowUpRequest request, CancellationToken cancellationToken = default)
        {
            if (fail) throw new AiProviderUnavailableException("fake provider unavailable");
            return Task.FromResult(new AiFollowUpDto { Content = "Can you justify that boundary?" });
        }
        public Task<AiAssessmentResponse> GenerateAssessmentAsync(AiAssessmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    public static async Task Run(Action<bool, string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var student = new User { Username = "workflow-student", PasswordHash = "hash", FullName = "Workflow Student", Role = "Student", CreatedAt = now };
        var course = new Course { Code = "TEXT", Name = "Text Viva" };
        var source = new Question { Course = course, Content = "Source question" };
        var session = new ExamSession { Course = course, CreatedByUserId = student.Id, SessionTitle = "Open", Status = ExamSessionStatus.Open, StartTime = now.AddMinutes(-1), EndTime = now.AddHours(1) };
        var snapshot = new ExamSessionQuestion { Session = session, Question = source, Content = "Frozen question", Points = 10, RubricSnapshotJson = "[]" };
        db.AddRange(student, snapshot); await db.SaveChangesAsync();
        var attemptService = new AttemptService(new AttemptRepository(db), new Identity(student.Id), TimeProvider.System);
        var started = await attemptService.Start(session.Id);
        var answer = await attemptService.Answer(started.Attempt.Id, new AnswerRequest { QuestionId = source.Id, AnswerText = "My persisted answer", ClientMessageId = Guid.NewGuid() });
        var followUp = await new InterviewService(new AttemptRepository(db), new WorkflowAi(), new Identity(student.Id), TimeProvider.System).GenerateFollowUpAsync(started.Attempt.Id);
        var submitted = await attemptService.Submit(started.Attempt.Id);
        check(started.Created && answer.Created && followUp.Sequence == 2 && submitted.Status == "Submitted", "text viva workflow persists start, answer, AI follow-up and submission");
        var turns = await attemptService.Turns(started.Attempt.Id);
        check(turns.Select(x => x.Speaker).SequenceEqual(new[] { "Student", "AI" }), "text viva turns retain ordered student and AI history");
        async Task Reject<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action(); check(false, name); }
            catch (T) { check(true, name); }
        }
        await Reject<ConflictException>(() => new InterviewService(new AttemptRepository(db), new WorkflowAi(), new Identity(student.Id), TimeProvider.System).GenerateFollowUpAsync(started.Attempt.Id), "submitted workflow rejects additional AI follow-up");
        var retrySession = new ExamSession { Course = course, CreatedByUserId = student.Id, SessionTitle = "Second", Status = ExamSessionStatus.Open, StartTime = now.AddMinutes(-1), EndTime = now.AddHours(1) };
        var retrySnapshot = new ExamSessionQuestion { Session = retrySession, Question = source, Content = "Second frozen question", Points = 10, RubricSnapshotJson = "[]" };
        db.Add(retrySnapshot); await db.SaveChangesAsync();
        var retryAttempt = (await attemptService.Start(retrySession.Id)).Attempt;
        await attemptService.Answer(retryAttempt.Id, new AnswerRequest { QuestionId = source.Id, AnswerText = "Answer", ClientMessageId = Guid.NewGuid() });
        await Reject<AiProviderUnavailableException>(() => new InterviewService(new AttemptRepository(db), new WorkflowAi(true), new Identity(student.Id), TimeProvider.System).GenerateFollowUpAsync(retryAttempt.Id), "provider failure does not create AI turn");
        check((await attemptService.Turns(retryAttempt.Id)).Count == 1, "provider failure leaves persisted student answer intact");
    }
}
