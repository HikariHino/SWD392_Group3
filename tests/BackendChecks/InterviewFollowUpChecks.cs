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
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebApi.Controllers;

internal static class InterviewFollowUpChecks
{
    private sealed class Identity(Guid id) : ICurrentUser
    {
        public Guid? Id => id;
        public bool IsLecturer => false;
        public bool IsStudent => true;
    }

    private sealed class FakeAi : IAiService
    {
        public AiFollowUpRequest? Request { get; private set; }
        public Task<Application.DTOs.QuestionBank.AiGeneratedQuestionDto> GenerateQuestionAsync(Application.DTOs.QuestionBank.AiGenerateQuestionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiFollowUpDto> GenerateFollowUpAsync(AiFollowUpRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new AiFollowUpDto { Content = "What trade-off would change your design?", SourceContext = "Fake provider" });
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
        var student = new User { Username = "follow-up", PasswordHash = "hash", FullName = "Student", Role = "Student", CreatedAt = now };
        var course = new Course { Code = "FOLLOW", Name = "Follow-up course" };
        var question = new Question { Course = course, Content = "Original source question" };
        var session = new ExamSession { Course = course, CreatedByUserId = student.Id, SessionTitle = "Open", Status = ExamSessionStatus.Open, StartTime = now.AddMinutes(-1), EndTime = now.AddHours(1) };
        var snapshot = new ExamSessionQuestion { Session = session, Question = question, Content = "Frozen exam question", Points = 10, RubricSnapshotJson = "[{\"criteria\":\"Accuracy\",\"weight\":100}]" };
        var attempt = new Transcript { Session = session, Student = student, Status = TranscriptStatus.InProgress };
        var answer = new TranscriptTurn { Transcript = attempt, ExamSessionQuestion = snapshot, Sequence = 1, Speaker = "Student", ContentText = "I would isolate the domain." };
        db.AddRange(snapshot, answer); await db.SaveChangesAsync();
        var fake = new FakeAi();
        var service = new InterviewService(new AttemptRepository(db), fake, new Identity(student.Id), TimeProvider.System);
        var followUp = await service.GenerateFollowUpAsync(attempt.Id);
        check(followUp.Sequence == 2 && followUp.QuestionId == question.Id && followUp.ContentText.StartsWith("What trade-off"), "AI follow-up persists ordered turn for answered snapshot question");
        check(fake.Request!.CourseName == "Follow-up course" && fake.Request.QuestionContent == "Frozen exam question" && fake.Request.RubricSnapshotJson.Contains("Accuracy"), "AI receives course, frozen question and rubric context");
        check(fake.Request.Conversation.Single().ContentText == answer.ContentText, "AI receives persisted conversation history");
        check((await db.TranscriptTurns.OrderBy(x => x.Sequence).LastAsync()).Speaker == "AI", "follow-up is stored with AI speaker");
        async Task Reject<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action(); check(false, name); }
            catch (T) { check(true, name); }
        }
        await Reject<ForbiddenException>(() => new InterviewService(new AttemptRepository(db), fake, new Identity(Guid.NewGuid()), TimeProvider.System).GenerateFollowUpAsync(attempt.Id), "other student cannot request follow-up");
        attempt.Status = TranscriptStatus.Submitted; await db.SaveChangesAsync();
        await Reject<ConflictException>(() => service.GenerateFollowUpAsync(attempt.Id), "submitted attempt cannot request follow-up");
        var emptyAttempt = new Transcript { Session = session, Student = new User { Username = "empty", PasswordHash = "hash", FullName = "Empty", Role = "Student", CreatedAt = now }, Status = TranscriptStatus.InProgress };
        db.Add(emptyAttempt); await db.SaveChangesAsync();
        await Reject<ValidationException>(() => new InterviewService(new AttemptRepository(db), fake, new Identity(emptyAttempt.UserId), TimeProvider.System).GenerateFollowUpAsync(emptyAttempt.Id), "follow-up requires persisted student answer");
        var controllerAuth = typeof(InterviewController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        check(controllerAuth.Roles == "Student", "follow-up HTTP endpoint requires Student role");
    }
}
