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
using Microsoft.AspNetCore.Authorization;
using WebApi.Controllers;

internal static class SessionLifecycleChecks
{
    sealed class Identity(Guid id, bool lecturer) : ICurrentUser
    {
        public Guid? Id => id;
        public bool IsLecturer => lecturer;
        public bool IsStudent => !lecturer;
    }
    public static async Task Run(Action<bool, string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var owner = new User { Username = "lecturer", PasswordHash = "hash", FullName = "Lecturer", Role = "Lecturer", CreatedAt = DateTime.UtcNow };
        var course = new Course { Code = "LIFE", Name = "Lifecycle" };
        var q = new Question { Course = course, Content = "Original", Points = 10, Rubrics = [new Rubric { Criteria = "Accuracy", Weight = 100, MaxScore = 10 }] };
        db.AddRange(owner, q); await db.SaveChangesAsync();
        var service = new ExamSessionService(new ExamSessionRepository(db), new Identity(owner.Id, true), TimeProvider.System);
        CreateSessionRequest Request() => new() { CourseId = course.Id, SessionTitle = " Test ", StartTime = DateTime.UtcNow.AddMinutes(-1), EndTime = DateTime.UtcNow.AddHours(1), QuestionIds = [q.Id] };
        async Task Reject<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action(); check(false, name); }
            catch (T) { check(true, name); }
        }
        var student = new ExamSessionService(new ExamSessionRepository(db), new Identity(owner.Id, false), TimeProvider.System);
        await Reject<ForbiddenException>(() => student.Create(Request()), "student cannot create session");
        var invalid = Request(); invalid.EndTime = invalid.StartTime;
        await Reject<ValidationException>(() => service.Create(invalid), "invalid session time rejected");
        invalid = Request(); invalid.StartTime = DateTime.SpecifyKind(invalid.StartTime, DateTimeKind.Unspecified);
        await Reject<ValidationException>(() => service.Create(invalid), "ambiguous non UTC time rejected");
        invalid = Request(); invalid.QuestionIds = [q.Id, q.Id];
        await Reject<ValidationException>(() => service.Create(invalid), "duplicate selected questions rejected");
        invalid = Request(); invalid.CourseId = Guid.NewGuid();
        await Reject<NotFoundException>(() => service.Create(invalid), "missing course rejected");
        var otherCourse = new Course { Code = "OTHER", Name = "Other" }; db.Add(otherCourse); await db.SaveChangesAsync();
        invalid = Request(); invalid.CourseId = otherCourse.Id;
        await Reject<ValidationException>(() => service.Create(invalid), "question from another course rejected");
        var draft = await service.Create(Request());
        check(draft.Status == "Draft" && draft.SessionTitle == "Test" && draft.CreatedByUserId == owner.Id, "create draft normalizes title and derives owner");
        var stranger = new ExamSessionService(new ExamSessionRepository(db), new Identity(Guid.NewGuid(), true), TimeProvider.System);
        await Reject<ForbiddenException>(() => stranger.Transition(draft.Id, "open"), "other lecturer cannot open session");
        await Reject<ForbiddenException>(() => stranger.Get(draft.Id), "other lecturer cannot read session");
        await Reject<ConflictException>(() => service.Transition(draft.Id, "close"), "draft cannot close");
        q.Rubrics.Single().Weight = 50; await db.SaveChangesAsync();
        await Reject<ValidationException>(() => service.Transition(draft.Id, "open"), "opening revalidates changed rubric");
        check((await service.Get(draft.Id)).Status == "Draft", "failed open preserves draft");
        q.Rubrics.Single().Weight = 100; q.Content = "At opening"; await db.SaveChangesAsync();
        var opened = await service.Transition(draft.Id, "open");
        check(opened.Status == "Open" && opened.Version == 1, "draft opens and advances version");
        var snapshot = await db.ExamSessionQuestions.SingleAsync(x => x.SessionId == draft.Id);
        check(snapshot.Content == "At opening" && snapshot.RubricSnapshotJson.Contains("Accuracy"), "opening captures current question and rubric snapshot");
        q.Content = "Later edit"; await db.SaveChangesAsync();
        check(snapshot.Content == "At opening", "source edits do not change opened snapshot");
        await Reject<ConflictException>(() => service.Transition(draft.Id, "open"), "cannot open twice");
        var attempt = new Transcript { SessionId = draft.Id, UserId = owner.Id }; db.Add(attempt); await db.SaveChangesAsync();
        var closed = await service.Transition(draft.Id, "close");
        check(closed.Status == "Closed" && attempt.Status == TranscriptStatus.Submitted && attempt.SubmittedAt != null && attempt.Version == 1, "closing atomically submits in progress attempt");
        await Reject<ConflictException>(() => service.Transition(draft.Id, "open"), "closed session cannot reopen");
        await Reject<ConflictException>(() => service.Transition(draft.Id, "cancel"), "closed session cannot cancel");
        var cancelled = await service.Create(Request());
        check((await service.Transition(cancelled.Id, "cancel")).Status == "Cancelled", "draft can cancel");
        await Reject<ConflictException>(() => service.Transition(cancelled.Id, "open"), "cancelled session cannot open");
        var openCancelled = await service.Create(Request());
        await service.Transition(openCancelled.Id, "open");
        check((await service.Transition(openCancelled.Id, "cancel")).Status == "Cancelled", "open session can cancel");
        var auth = typeof(ExamSessionsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        check(auth.Roles == "Lecturer", "session HTTP endpoints require lecturer");
        var controller = new ExamSessionsController(service);
        var response = (Microsoft.AspNetCore.Mvc.CreatedAtActionResult)(await controller.Create(Request())).Result!;
        check(response.StatusCode == 201 && response.ActionName == "Get", "session POST returns created location");
    }
}
