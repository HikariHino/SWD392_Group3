using Application.DTOs.Interviews;
using Application.Exceptions;
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
using WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;

internal static class AttemptChecks
{
    sealed class Identity(Guid id, bool lecturer = false) : ICurrentUser
    {
        public Guid? Id => id;
        public bool IsLecturer => lecturer;
        public bool IsStudent => !lecturer;
    }
    sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    public static async Task Run(Action<bool, string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var clock = new FixedClock();
        User User(string name) => new() { Username = name, PasswordHash = "hash", FullName = name, Role = "Student", CreatedAt = clock.Now.UtcDateTime };
        var student = User("student"); var lecturer = User("lecturer"); var other = User("other");
        var course = new Course { Code = "ATTEMPT", Name = "Attempts" };
        var question = new Question { Course = course, Content = "Mutable", ExpectedAnswer = "Secret" };
        var session = new ExamSession { Course = course, CreatedByUser = lecturer, SessionTitle = "Exam", Status = ExamSessionStatus.Open, StartTime = clock.Now.UtcDateTime.AddMinutes(-1), EndTime = clock.Now.UtcDateTime.AddHours(1) };
        var snapshot = new ExamSessionQuestion { Session = session, Question = question, Content = "Snapshot question", Points = 10, RubricSnapshotJson = "[]" };
        db.AddRange(student, other, snapshot); await db.SaveChangesAsync();
        AttemptService Service(Guid id, bool teacher = false) => new(new AttemptRepository(db), new Identity(id, teacher), clock);
        var service = Service(student.Id);
        async Task Reject<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action(); check(false, name); }
            catch (T) { check(true, name); }
        }
        await Reject<ForbiddenException>(() => Service(lecturer.Id, true).Start(session.Id), "lecturer cannot start student attempt");
        session.Status = ExamSessionStatus.Draft; await db.SaveChangesAsync();
        await Reject<ConflictException>(() => service.Start(session.Id), "draft session cannot be joined");
        session.Status = ExamSessionStatus.Open; session.StartTime = clock.Now.UtcDateTime.AddMinutes(1); await db.SaveChangesAsync();
        await Reject<ConflictException>(() => service.Start(session.Id), "student cannot join before start");
        session.StartTime = clock.Now.UtcDateTime.AddMinutes(-1); session.EndTime = clock.Now.UtcDateTime; await db.SaveChangesAsync();
        await Reject<ConflictException>(() => service.Start(session.Id), "student cannot join at end boundary");
        session.EndTime = clock.Now.UtcDateTime.AddHours(1); await db.SaveChangesAsync();
        var controller = new AttemptsController(service);
        var response = (CreatedAtActionResult)(await controller.Start(session.Id)).Result!;
        var attempt = (AttemptDto)response.Value!;
        check(response.StatusCode == 201 && attempt.UserId == student.Id, "attempt POST derives student and returns created location");
        check(attempt.SessionStartTime == session.StartTime && attempt.SessionEndTime == session.EndTime, "student attempt response includes session window for countdown");
        var retry = await service.Start(session.Id);
        check(!retry.Created && retry.Attempt.Id == attempt.Id && await db.Transcripts.CountAsync() == 1, "start retry returns same attempt");
        await Reject<ForbiddenException>(() => Service(other.Id).Get(attempt.Id), "other student cannot read attempt");
        await Reject<ForbiddenException>(() => Service(other.Id, true).Get(attempt.Id), "other lecturer cannot read attempt");
        check((await Service(lecturer.Id, true).Get(attempt.Id)).Id == attempt.Id, "session lecturer can read attempt");
        var questions = await service.Questions(attempt.Id);
        check(questions.Single().Content == "Snapshot question" && !System.Text.Json.JsonSerializer.Serialize(questions).Contains("Secret"), "student receives snapshot without expected answer");
        var request = new AnswerRequest { QuestionId = question.Id, AnswerText = "My answer", ClientMessageId = Guid.NewGuid() };
        await Reject<ForbiddenException>(() => Service(other.Id).Answer(attempt.Id, request), "other student cannot write answer");
        await Reject<ForbiddenException>(() => Service(lecturer.Id, true).Answer(attempt.Id, request), "lecturer cannot write student answer");
        await Reject<ValidationException>(() => service.Answer(attempt.Id, new() { QuestionId = Guid.NewGuid(), AnswerText = "Wrong question", ClientMessageId = Guid.NewGuid() }), "question outside session rejected");
        await Reject<ValidationException>(() => service.Answer(attempt.Id, new() { QuestionId = question.Id, AnswerText = " ", ClientMessageId = Guid.NewGuid() }), "blank answer rejected");
        var answer = await service.Answer(attempt.Id, request);
        check(answer.Created && answer.Turn.Sequence == 1 && answer.Turn.Speaker == "Student" && answer.Turn.QuestionId == question.Id, "answer persists server sequence speaker and question");
        check(!(await service.Answer(attempt.Id, request)).Created && await db.TranscriptTurns.CountAsync() == 1, "message retry is idempotent");
        await Reject<ConflictException>(() => service.Answer(attempt.Id, new() { QuestionId = question.Id, AnswerText = "Changed", ClientMessageId = request.ClientMessageId }), "message key cannot change payload");
        var second = await service.Answer(attempt.Id, new() { QuestionId = question.Id, AnswerText = "More", ClientMessageId = Guid.NewGuid() });
        check(second.Turn.Sequence == 2 && (await service.Turns(attempt.Id)).Count == 2, "new answer appends ordered turn");
        clock.Now = clock.Now.AddHours(2);
        await Reject<ConflictException>(() => service.Answer(attempt.Id, new() { QuestionId = question.Id, AnswerText = "Late", ClientMessageId = Guid.NewGuid() }), "answers after end rejected");
        var submitted = await service.Submit(attempt.Id);
        check(submitted.Status == "Submitted" && submitted.SubmittedAt == clock.Now.UtcDateTime, "expired attempt can submit with server timestamp");
        check((await service.Submit(attempt.Id)).Version == submitted.Version, "submit retry does not advance version");
        check(!(await service.Answer(attempt.Id, request)).Created, "persisted message can retry after submit");
        clock.Now = clock.Now.AddHours(-2);
        await Reject<ConflictException>(() => service.Answer(attempt.Id, new() { QuestionId = question.Id, AnswerText = "After submit", ClientMessageId = Guid.NewGuid() }), "submitted attempt rejects new answers");
        await Reject<ForbiddenException>(() => Service(other.Id).Submit(attempt.Id), "other student cannot submit attempt");
        // A stale student write must roll back its inserted turn when a lecturer closes.
        using var staleDb = new AivesDbContext(options);
        var staleSession = await staleDb.ExamSessions.SingleAsync();
        var staleAttempt = await staleDb.Transcripts.SingleAsync();
        session.Status = ExamSessionStatus.Closed; session.Version++; await db.SaveChangesAsync();
        staleSession.Version++; staleAttempt.Version++;
        staleDb.TranscriptTurns.Add(new TranscriptTurn { TranscriptId = attempt.Id, Sequence = 3, Speaker = "Student", ContentText = "Race" });
        await Reject<ConflictException>(() => new AttemptRepository(staleDb).Save(), "stale student write conflicts with close");
        check(await db.TranscriptTurns.CountAsync() == 2, "conflicting answer insertion rolls back atomically");
        check(!(await service.Start(session.Id)).Created, "start retry retrieves existing attempt after close");
        await Reject<ConflictException>(() => Service(other.Id).Start(session.Id), "closed session rejects new attempt");
        using (var reloaded = new AivesDbContext(options))
        {
            var fresh = new AttemptService(new AttemptRepository(reloaded), new Identity(student.Id), clock);
            check((await fresh.Turns(attempt.Id)).Select(x => x.ContentText).SequenceEqual(new[] { "My answer", "More" }), "answer history survives fresh context reload");
            check(!(await fresh.Answer(attempt.Id, request)).Created, "message retry resolves from persisted history");
        }
        session.Status = ExamSessionStatus.Cancelled; await db.SaveChangesAsync();
        await Reject<ConflictException>(() => service.Submit(attempt.Id), "cancelled session rejects submit");
        var archived = await db.Transcripts.SingleAsync(); archived.IsDeleted = true; await db.SaveChangesAsync();
        await Reject<ConflictException>(() => service.Start(session.Id), "archived attempt cannot be recreated");
        var role = typeof(AttemptsController).GetMethod(nameof(AttemptsController.Answer))!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single();
        check(role.Roles == "Student", "answer HTTP endpoint requires Student role");
    }
}
