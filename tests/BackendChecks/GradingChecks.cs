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

internal static class GradingChecks
{
    private sealed class Identity(Guid id, bool lecturer) : ICurrentUser
    {
        public Guid? Id => id;
        public bool IsLecturer => lecturer;
        public bool IsStudent => !lecturer;
    }
    private sealed class FakeAi(Func<AiAssessmentRequest, AiAssessmentResponse> result) : IAiService
    {
        public Task<Application.DTOs.QuestionBank.AiGeneratedQuestionDto> GenerateQuestionAsync(Application.DTOs.QuestionBank.AiGenerateQuestionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiFollowUpDto> GenerateFollowUpAsync(AiFollowUpRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiAssessmentResponse> GenerateAssessmentAsync(AiAssessmentRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result(request));
    }
    public static async Task Run(Action<bool, string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var lecturer = new User { Username = "grading-owner", PasswordHash = "hash", FullName = "Owner", Role = "Lecturer", CreatedAt = now };
        var otherLecturer = new User { Username = "grading-other", PasswordHash = "hash", FullName = "Other", Role = "Lecturer", CreatedAt = now };
        var student = new User { Username = "grading-student", PasswordHash = "hash", FullName = "Student", Role = "Student", CreatedAt = now };
        var course = new Course { Code = "GRADE", Name = "Grading" };
        var question = new Question { Course = course, Content = "Question" };
        var session = new ExamSession { Course = course, CreatedByUser = lecturer, SessionTitle = "Closed", Status = ExamSessionStatus.Closed, StartTime = now.AddHours(-1), EndTime = now };
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var rubricJson = $"[{{\"rubricId\":\"{firstId}\",\"criteria\":\"Accuracy\",\"weight\":40,\"maxScore\":4}},{{\"rubricId\":\"{secondId}\",\"criteria\":\"Reasoning\",\"weight\":60,\"maxScore\":6}}]";
        var snapshot = new ExamSessionQuestion { Session = session, Question = question, Content = "Frozen question", ExpectedAnswer = "Expected", Points = 10m, RubricSnapshotJson = rubricJson };
        var attempt = new Transcript { Session = session, Student = student, Status = TranscriptStatus.Submitted, SubmittedAt = now };
        db.AddRange(otherLecturer, new TranscriptTurn { Transcript = attempt, ExamSessionQuestion = snapshot, Sequence = 1, Speaker = "Student", ContentText = "Student answer" });
        await db.SaveChangesAsync();
        AiAssessmentResponse Valid(AiAssessmentRequest request) => new() { RubricScores = [new(firstId, 3m), new(secondId, 3m)], Feedback = "Clear but incomplete.", ConfidenceLevel = .8m };
        GradingService Service(Guid id, bool lecturerRole, Func<AiAssessmentRequest, AiAssessmentResponse>? response = null) => new(new GradingRepository(db), new FakeAi(response ?? Valid), new Identity(id, lecturerRole), TimeProvider.System);
        async Task Reject<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action(); check(false, name); }
            catch (T) { check(true, name); }
        }
        await Reject<ForbiddenException>(() => Service(student.Id, false).GradeAttemptAsync(attempt.Id), "student cannot trigger grading");
        await Reject<ForbiddenException>(() => Service(otherLecturer.Id, true).GradeAttemptAsync(attempt.Id), "non-owner lecturer cannot trigger grading");
        var graded = await Service(lecturer.Id, true).GradeAttemptAsync(attempt.Id);
        check(graded.Count == 1 && graded.Single().AiSuggestedScore == 6m && graded.Single().AiConfidenceLevel == .8m, "AI rubric scores are validated and persisted as calculated suggestion");
        check(attempt.Status == TranscriptStatus.AwaitingReview && attempt.GradedAt != null, "successful grading moves attempt to awaiting review");
        var retry = await Service(lecturer.Id, true).GradeAttemptAsync(attempt.Id);
        check(retry.Single().Id == graded.Single().Id && await db.Assessments.CountAsync() == 1, "grading retry is idempotent and does not duplicate assessment");
        var badCourse = new Course { Code = "BADGRADE", Name = "Bad" };
        var badQuestion = new Question { Course = badCourse, Content = "Bad question" };
        var badSession = new ExamSession { Course = badCourse, CreatedByUser = lecturer, SessionTitle = "Bad", Status = ExamSessionStatus.Closed, StartTime = now.AddHours(-1), EndTime = now };
        var badSnapshot = new ExamSessionQuestion { Session = badSession, Question = badQuestion, Content = "Bad frozen", Points = 10m, RubricSnapshotJson = rubricJson };
        var badAttempt = new Transcript { Session = badSession, Student = student, Status = TranscriptStatus.Submitted };
        db.Add(new TranscriptTurn { Transcript = badAttempt, ExamSessionQuestion = badSnapshot, Sequence = 1, Speaker = "Student", ContentText = "Answer" }); await db.SaveChangesAsync();
        await Reject<AiProviderResponseException>(() => Service(lecturer.Id, true, _ => new AiAssessmentResponse { RubricScores = [new(firstId, 3m), new(firstId, 3m)], Feedback = "Bad", ConfidenceLevel = .5m }).GradeAttemptAsync(badAttempt.Id), "invalid AI rubric output rejected");
        check(badAttempt.Status == TranscriptStatus.Submitted && await db.Assessments.CountAsync() == 1, "invalid AI output leaves no partial assessment or state transition");
        var auth = typeof(GradingController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        check(auth.Roles == "Lecturer", "grade HTTP endpoint requires Lecturer role");
    }
}
