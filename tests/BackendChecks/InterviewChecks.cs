using Domain.Entities.Interviews;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

internal static class InterviewChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var user = new User { CreatedAt = DateTime.UtcNow, Username = "exam-user", PasswordHash = "hash", FullName = "Student", Role = "Student" };
        var course = new Course { Code = "EXAM", Name = "Exam course" };
        var question = new Question { Course = course, Content = "Original" };
        var session = new ExamSession { Course = course, CreatedByUser = user, SessionTitle = "Session", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
        var snapshot = new ExamSessionQuestion { Session = session, Question = question, Content = question.Content, Points = 10, RubricSnapshotJson = "[{\"criteria\":\"Accuracy\",\"weight\":100,\"maxScore\":10}]" };
        var transcript = new Transcript { Session = session, Student = user };
        var turn = new TranscriptTurn { Transcript = transcript, ExamSessionQuestion = snapshot, Sequence = 1, Speaker = "Student", ContentText = "Answer", ClientMessageId = Guid.NewGuid() };
        db.AddRange(snapshot, turn);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        check(await db.TranscriptTurns.CountAsync() == 1, "exam graph persists with relational foreign keys");
        async Task Reject(object entity, string name)
        {
            db.Add(entity);
            try { await db.SaveChangesAsync(); check(false, name); }
            catch (DbUpdateException) { check(true, name); }
            db.ChangeTracker.Clear();
        }
        await Reject(new Transcript { SessionId = session.Id, UserId = user.Id }, "duplicate student attempt rejected");
        await Reject(new Transcript { SessionId = Guid.NewGuid(), UserId = user.Id }, "missing session FK rejected");
        await Reject(new TranscriptTurn { TranscriptId = transcript.Id, Sequence = 1, Speaker = "AI", ContentText = "Duplicate" }, "duplicate turn sequence rejected");
        await Reject(new TranscriptTurn { TranscriptId = transcript.Id, Sequence = 2, Speaker = "Student", ContentText = "Retry", ClientMessageId = turn.ClientMessageId }, "duplicate client message rejected");
        var source = await db.Questions.SingleAsync();
        source.Content = "Edited";
        source.IsDeleted = true;
        await db.SaveChangesAsync();
        var stored = await db.ExamSessionQuestions.SingleAsync();
        check(stored.Content == "Original" && stored.RubricSnapshotJson.Contains("Accuracy"), "snapshot survives source edit and soft delete");
        db.ChangeTracker.Clear();
        source = await db.Questions.IgnoreQueryFilters().SingleAsync();
        db.Remove(source);
        try { await db.SaveChangesAsync(); check(false, "question history prevents hard delete"); }
        catch (DbUpdateException) { check(true, "question history prevents hard delete"); }
        db.ChangeTracker.Clear();
        using var competing = new AivesDbContext(options);
        var stale = await competing.Transcripts.SingleAsync();
        var current = await db.Transcripts.SingleAsync();
        current.Version++;
        await db.SaveChangesAsync();
        stale.Version++;
        try { await competing.SaveChangesAsync(); check(false, "stale transcript version rejected"); }
        catch (DbUpdateConcurrencyException) { check(true, "stale transcript version rejected"); }
    }
}
