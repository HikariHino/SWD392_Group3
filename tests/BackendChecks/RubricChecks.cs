using Application.DTOs.QuestionBank;
using Application.Exceptions;
using Application.Mappings;
using Application.Services;
using Application.Validators.QuestionBank;
using AutoMapper;
using Domain.Entities.QuestionBank;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class RubricChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var validator = new ReplaceCourseRubricsRequestValidator();
        ReplaceCourseRubricsRequest Request(double weight = 100) => new() { Rubrics = [new() { Criteria = "Course-wide criterion", Weight = weight, MaxScore = 10 }] };
        check(validator.Validate(Request()).IsValid, "course rubric weight 100 accepted");
        check(!validator.Validate(Request(99)).IsValid, "course rubric total must be 100");
        check(!validator.Validate(new ReplaceCourseRubricsRequest { Rubrics = [null!] }).IsValid, "null course rubric rejected");
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        using var db = new AivesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var course = new Course { Code = "C09", Name = "Course rubric" };
        db.AddRange(course, new Question { Course = course, Content = "Question A" }, new Question { Course = course, Content = "Question B" });
        await db.SaveChangesAsync();
        var service = new CourseService(new UnitOfWork(db), mapper, new CreateCourseRequestValidator(), new UpdateCourseRequestValidator(), validator);
        var updated = await service.ReplaceRubricsAsync(course.Id, Request());
        check(updated.Rubrics.Single().Criteria == "Course-wide criterion", "course returns its shared rubric");
        check((await db.Questions.CountAsync()) == 2 && (await db.Rubrics.CountAsync()) == 1, "one rubric serves all course questions");
        var oldId = updated.Rubrics.Single().Id;
        updated = await service.ReplaceRubricsAsync(course.Id, Request());
        check(updated.Rubrics.Single().Id != oldId && (await db.Rubrics.IgnoreQueryFilters().CountAsync()) == 2, "replacing preserves course rubric history");
        try { await service.ReplaceRubricsAsync(course.Id, Request(50)); check(false, "invalid course rubric update rejected"); }
        catch (ValidationException) { check(true, "invalid course rubric update rejected"); }
        check((await db.Rubrics.CountAsync()) == 1, "invalid replacement preserves active course rubric");
    }
}
