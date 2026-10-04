using Application.DTOs.QuestionBank;
using Application.Exceptions;
using Application.Mappings;
using Application.Services;
using Application.Validators.QuestionBank;
using AutoMapper;
using Domain.Entities.QuestionBank;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Controllers;

internal static class CourseChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        CourseService Service(AivesDbContext context) => new(new UnitOfWork(context), mapper,
            new CreateCourseRequestValidator(), new UpdateCourseRequestValidator());
        async Task Reject<T>(Func<Task> operation, string name) where T : Exception
        {
            try { await operation(); check(false, name); }
            catch (T) { check(true, name); }
        }

        Guid firstId;
        using (var context = new AivesDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var service = Service(context);
            await Reject<Application.Exceptions.ValidationException>(() => service.CreateCourseAsync(new() { Code = " ", Name = "Name" }), "blank course code returns validation error");
            await Reject<Application.Exceptions.ValidationException>(() => service.CreateCourseAsync(new() { Code = new string('a', 51), Name = "Name" }), "course code database length enforced");
            await Reject<Application.Exceptions.ValidationException>(() => service.CreateCourseAsync(new() { Code = "A", Name = new string('a', 201) }), "course name database length enforced");
            await Reject<Application.Exceptions.ValidationException>(() => service.CreateCourseAsync(new() { Code = "A", Name = "Name", Description = new string('a', 1001) }), "course description database length enforced");
            check(await context.Courses.CountAsync() == 0, "invalid course requests do not persist");
            var controller = new CoursesController(service);
            var created = await controller.CreateCourse(new() { Code = " swd392 ", Name = " Design ", Description = " Description " });
            var response = (CreatedAtActionResult)created.Result!;
            var dto = (CourseDto)response.Value!;
            firstId = dto.Id;
            check(response.StatusCode == 201 && response.ActionName == nameof(CoursesController.GetCourseById) && (Guid)response.RouteValues!["id"]! == firstId, "course POST returns 201 with correct Location action/id");
            check(dto.Code == "SWD392" && dto.Name == "Design" && dto.Description == "Description", "course input normalized before save");
            await Reject<ConflictException>(() => service.CreateCourseAsync(new() { Code = "swd392", Name = "Duplicate" }), "case insensitive duplicate course rejected");
            var second = await service.CreateCourseAsync(new() { Code = "PRN231", Name = "API" });
            await Reject<ConflictException>(() => service.UpdateCourseAsync(second.Id, new() { Code = "SWD392", Name = "Changed" }), "update cannot take another course code");
            check((await service.GetCourseByIdAsync(second.Id)).Code == "PRN231", "conflicting update preserves original course");
            var updated = await service.UpdateCourseAsync(firstId, new() { Code = "swd392", Name = "New name" });
            check(updated.Name == "New name" && updated.Description == null, "PUT updates course and clears omitted optional description");
            await Reject<NotFoundException>(() => service.GetCourseByIdAsync(Guid.NewGuid()), "missing course GET is not found");
            await Reject<NotFoundException>(() => service.UpdateCourseAsync(Guid.NewGuid(), new() { Code = "VALID", Name = "Valid" }), "missing course PUT is not found");
            await Reject<NotFoundException>(() => service.DeleteCourseAsync(Guid.NewGuid()), "missing course DELETE is not found");
            context.Questions.Add(new Question { Id = Guid.NewGuid(), CourseId = firstId, Content = "Active question" });
            await context.SaveChangesAsync();
            await Reject<ConflictException>(() => service.DeleteCourseAsync(firstId), "course with active questions cannot be deleted");
            check(!(await context.Courses.FindAsync(firstId))!.IsDeleted, "blocked course deletion preserves active course");
            foreach (var question in await context.Questions.ToListAsync()) question.IsDeleted = true;
            await context.SaveChangesAsync();
            var deletedResponse = await controller.DeleteCourse(firstId);
            check(deletedResponse is NoContentResult, "course DELETE returns 204 after questions soft-deleted");
            await Reject<NotFoundException>(() => service.GetCourseByIdAsync(firstId), "deleted course hidden in tracked context");
            await Reject<NotFoundException>(() => service.UpdateCourseAsync(firstId, new() { Code = "OTHER", Name = "Other" }), "deleted course cannot be updated");
            check((await service.GetCoursesAsync()).All(c => c.Id != firstId), "course list excludes soft-deleted course");
            var questionService = new QuestionBankService(new UnitOfWork(context), mapper, new QuestionQueryParametersValidator(),
                new CreateQuestionRequestValidator(), new UpdateQuestionRequestValidator());
            check((await questionService.GetCoursesAsync()).All(c => c.Id != firstId), "legacy courses endpoint returns active CourseDto collection");
            check(typeof(CourseDto).GetProperties().Select(p => p.Name).Order().SequenceEqual(new[] { "Code", "Description", "Id", "Name" }.Order()), "CourseDto exposes no navigation or audit internals");
        }
        using (var context = new AivesDbContext(options))
        {
            var service = Service(context);
            await Reject<NotFoundException>(() => service.GetCourseByIdAsync(firstId), "deleted course hidden after fresh context reload");
            check(await context.Courses.IgnoreQueryFilters().AnyAsync(c => c.Id == firstId && c.IsDeleted), "course deletion retains database history");
            var replacement = await service.CreateCourseAsync(new() { Code = "SWD392", Name = "New course" });
            check(replacement.Id != firstId, "deleted course code can be reused with new identity");
        }
    }
}
