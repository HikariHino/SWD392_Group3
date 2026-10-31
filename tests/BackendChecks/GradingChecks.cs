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
        var snapshot = new ExamSessionQuestion { Session = session, Question = question, Content = "Frozen question", ExpectedAnswer = "Expected", Points = 10m, RubricSnapshotJson = rubricJson, DisplayOrder = 1 };
        var unansweredQuestion = new Question { Course = course, Content = "Unanswered question" };
        var unansweredSnapshot = new ExamSessionQuestion { Session = session, Question = unansweredQuestion, Content = "Frozen unanswered question", Points = 10m, RubricSnapshotJson = rubricJson, DisplayOrder = 2 };
        var attempt = new Transcript { Session = session, Student = student, Status = TranscriptStatus.Submitted, SubmittedAt = now };
        db.AddRange(otherLecturer, unansweredSnapshot, new TranscriptTurn { Transcript = attempt, ExamSessionQuestion = snapshot, Sequence = 1, Speaker = "Student", ContentText = "Student answer" });
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
        check(graded.Count == 2 && graded.Single(x => x.AiSuggestedScore.HasValue).AiSuggestedScore == 6m &&
              graded.Single(x => x.AiSuggestedScore.HasValue).AiConfidenceLevel == .8m && graded.Single(x => !x.AiSuggestedScore.HasValue).StudentAnswer == string.Empty,
            "AI rubric scores are validated and an unanswered question gets a lecturer-review placeholder");
        check(attempt.Status == TranscriptStatus.AwaitingReview && attempt.GradedAt != null, "successful grading moves attempt to awaiting review");
        var retry = await Service(lecturer.Id, true).GradeAttemptAsync(attempt.Id);
        check(retry.Count == 2 && retry.Select(x => x.Id).ToHashSet().SetEquals(graded.Select(x => x.Id)) &&
              await db.Assessments.CountAsync() == 2, "grading retry is idempotent and does not duplicate assessment");
        var assessmentId = graded.Single(x => x.AiSuggestedScore.HasValue).Id;
        var draftResult = await Service(lecturer.Id, true).GetAttemptResultAsync(attempt.Id);
        check(draftResult.AiSuggestedTotal is null && draftResult.TotalScore is null && !draftResult.IsComplete &&
              draftResult.PendingQuestionCount == 2, "lecturer draft result withholds totals while final scores are pending");
        await Reject<ConflictException>(() => Service(student.Id, false).GetAttemptResultAsync(attempt.Id),
            "student cannot view result before official finalization");
        await Reject<ForbiddenException>(() => Service(otherLecturer.Id, true).GetAttemptResultAsync(attempt.Id),
            "non-owner lecturer cannot view attempt result");
        var review = await Service(lecturer.Id, true).GetAttemptAssessmentsAsync(attempt.Id);
        check(review.Count == 2 && review.Single(x => x.AiSuggestedScore.HasValue).AiSuggestedScore == 6m &&
              review.All(x => x.FinalScore is null),
            "review response separates AI suggestion from official final score");
        await Reject<ForbiddenException>(() => Service(otherLecturer.Id, true).GetAttemptAssessmentsAsync(attempt.Id),
            "non-owner lecturer cannot view assessment review");
        await Reject<ForbiddenException>(() => Service(student.Id, false).GetAttemptAssessmentsAsync(attempt.Id),
            "student cannot view lecturer assessment review");
        await Reject<ForbiddenException>(() => Service(otherLecturer.Id, true).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 7m, Version = 0 }), "non-owner lecturer cannot finalize score");
        await Reject<ForbiddenException>(() => Service(student.Id, false).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 7m, Version = 0 }), "student cannot finalize score");
        await Reject<ValidationException>(() => Service(lecturer.Id, true).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 11m, Version = 0 }), "final score above question points rejected");
        await Reject<ValidationException>(() => Service(lecturer.Id, true).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 7m }), "missing assessment version rejected");
        await Reject<ConflictException>(() => Service(lecturer.Id, true).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 7m, Version = 3 }), "stale assessment version rejected");
        var finalized = await Service(lecturer.Id, true).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 7.126m, TeacherComment = "  Reviewed and adjusted.  ", Version = 0 });
        check(finalized.FinalScore == 7.13m && finalized.TeacherComment == "Reviewed and adjusted." &&
              finalized.FinalizedByUserId == lecturer.Id && finalized.FinalizedAt != null && finalized.Version == 1,
            "lecturer finalization rounds score, trims comment, audits actor/time and increments version");
        var pendingAfterFirst = await Service(lecturer.Id, true).GetAttemptResultAsync(attempt.Id);
        check(pendingAfterFirst.TotalScore is null && pendingAfterFirst.PendingQuestionCount == 1 && !pendingAfterFirst.IsComplete,
            "official total remains unavailable until the unanswered question is finalized");
        await Reject<ConflictException>(() => Service(student.Id, false).GetAttemptResultAsync(attempt.Id),
            "student cannot see a result while one question remains unfinalized");
        var unansweredAssessment = graded.Single(x => !x.AiSuggestedScore.HasValue);
        await Service(lecturer.Id, true).FinalizeAssessmentAsync(unansweredAssessment.Id,
            new FinalizeAssessmentRequest { FinalScore = 0m, TeacherComment = "No answer submitted.", Version = 0 });
        db.ChangeTracker.Clear();
        var persisted = await db.Assessments.SingleAsync(x => x.Id == assessmentId);
        check(persisted.FinalScore == 7.13m && persisted.TeacherComment == "Reviewed and adjusted." &&
              persisted.FinalizedByUserId == lecturer.Id && persisted.FinalizedAt != null && persisted.Version == 1,
            "final decision is persisted in database and survives EF reload");
        var officialResult = await Service(student.Id, false).GetAttemptResultAsync(attempt.Id);
        check(officialResult.Status == "Finalized" && officialResult.TotalScore == 3.57m &&
              officialResult.AiSuggestedTotal is null && officialResult.IsComplete && officialResult.PendingQuestionCount == 0,
            "student owner receives official total after every answered or unanswered question is finalized");
        await Reject<ConflictException>(() => Service(lecturer.Id, true).FinalizeAssessmentAsync(assessmentId,
            new FinalizeAssessmentRequest { FinalScore = 8m, TeacherComment = "Overwrite", Version = 1 }),
            "finalized assessment cannot be overwritten");
        db.ChangeTracker.Clear();
        var badCourse = new Course { Code = "BADGRADE", Name = "Bad" };
        var badQuestion = new Question { Course = badCourse, Content = "Bad question" };
        var badSession = new ExamSession { Course = badCourse, CreatedByUserId = lecturer.Id, SessionTitle = "Bad", Status = ExamSessionStatus.Closed, StartTime = now.AddHours(-1), EndTime = now };
        var badSnapshot = new ExamSessionQuestion { Session = badSession, Question = badQuestion, Content = "Bad frozen", Points = 10m, RubricSnapshotJson = rubricJson };
        var badAttempt = new Transcript { Session = badSession, UserId = student.Id, Status = TranscriptStatus.Submitted };
        db.Add(new TranscriptTurn { Transcript = badAttempt, ExamSessionQuestion = badSnapshot, Sequence = 1, Speaker = "Student", ContentText = "Answer" }); await db.SaveChangesAsync();
        await Reject<AiProviderResponseException>(() => Service(lecturer.Id, true, _ => new AiAssessmentResponse { RubricScores = [new(firstId, 3m), new(firstId, 3m)], Feedback = "Bad", ConfidenceLevel = .5m }).GradeAttemptAsync(badAttempt.Id), "invalid AI rubric output rejected");
        check(badAttempt.Status == TranscriptStatus.Submitted && await db.Assessments.CountAsync() == 2, "invalid AI output leaves no partial assessment or state transition");
        var incompleteResult = await Service(lecturer.Id, true).GetAttemptResultAsync(badAttempt.Id);
        check(!incompleteResult.IsComplete && incompleteResult.PendingQuestionCount == 1 && incompleteResult.TotalScore is null,
            "incomplete grading remains a lecturer-visible draft with no official total");
        var auth = typeof(GradingController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        check(auth.Roles == "Lecturer", "grade HTTP endpoint requires Lecturer role");
        var assessmentAuth = typeof(AssessmentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        check(assessmentAuth.Roles == "Lecturer", "assessment review and finalization HTTP endpoints require Lecturer role");
    }
}
