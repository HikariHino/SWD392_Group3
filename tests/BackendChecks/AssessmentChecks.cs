using Application.DTOs.Interviews;
using Application.Services;
using Domain.Entities.Interviews;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

internal static class AssessmentChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var rubric = new[]
        {
            new RubricSnapshotCriterion(first, "Accuracy", 40m, 4m, null),
            new RubricSnapshotCriterion(second, "Reasoning", 60m, 6m, null)
        };
        check(ScoreCalculator.CalculateQuestionScore(10m, rubric, [new(first, 3m), new(second, 3m)]) == 6m, "question score follows weighted rubric formula");
        check(ScoreCalculator.CalculateAttemptTotal([(6m, 10m), (10m, 20m)]) == 16m / 3m, "attempt total uses all question points as denominator");
        check(ScoreCalculator.RoundForStorage(2.345m) == 2.35m && ScoreCalculator.RoundForStorage(-2.345m) == -2.35m, "score storage rounds away from zero only at final value");
        void Reject(Action action, string name)
        {
            try { action(); check(false, name); }
            catch (ArgumentException) { check(true, name); }
        }
        Reject(() => ScoreCalculator.CalculateQuestionScore(10m, rubric, [new(first, 3m), new(first, 2m)]), "duplicate rubric score rejected");
        Reject(() => ScoreCalculator.CalculateQuestionScore(10m, rubric, [new(first, 5m), new(second, 3m)]), "criterion score above max rejected");
        Reject(() => ScoreCalculator.CalculateQuestionScore(10m, [new(first, "Bad", 90m, 4m, null)], [new(first, 1m)]), "rubric weights not totaling 100 rejected");

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var lecturer = new User { Username = "assessor", PasswordHash = "hash", FullName = "Assessor", Role = "Lecturer", CreatedAt = DateTime.UtcNow };
        var student = new User { Username = "assessed", PasswordHash = "hash", FullName = "Student", Role = "Student", CreatedAt = DateTime.UtcNow };
        var course = new Course { Code = "ASSESS", Name = "Assessment" };
        var question = new Question { Course = course, Content = "Question" };
        var session = new ExamSession { Course = course, CreatedByUser = lecturer, SessionTitle = "Session", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
        var snapshot = new ExamSessionQuestion { Session = session, Question = question, Content = "Frozen", Points = 10m, RubricSnapshotJson = "[{\"rubricId\":\"" + first + "\"}]" };
        var attempt = new Transcript { Session = session, Student = student };
        var assessment = new Assessment { Transcript = attempt, ExamSessionQuestion = snapshot, StudentAnswer = "Answer", Points = 10m, RubricSnapshotJson = snapshot.RubricSnapshotJson, FinalizedByUser = lecturer };
        db.Add(assessment); await db.SaveChangesAsync();
        check(await db.Assessments.CountAsync() == 1 && assessment.FinalizedByUserId == lecturer.Id, "assessment persists transcript/question snapshot and finalizer FK");
        db.Assessments.Add(new Assessment { TranscriptId = attempt.Id, ExamSessionQuestionId = snapshot.Id, StudentAnswer = "Duplicate", Points = 10m, RubricSnapshotJson = "[]" });
        try { await db.SaveChangesAsync(); check(false, "one assessment per transcript question enforced"); }
        catch (DbUpdateException) { check(true, "one assessment per transcript question enforced"); }
        db.ChangeTracker.Clear();
        var stored = await db.Assessments.SingleAsync();
        stored.IsDeleted = true; await db.SaveChangesAsync();
        check(await db.Assessments.CountAsync() == 0 && await db.Assessments.IgnoreQueryFilters().CountAsync() == 1, "assessment soft delete retains history and hides active query");
    }
}
