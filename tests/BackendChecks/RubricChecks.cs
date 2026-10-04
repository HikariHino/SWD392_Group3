using Application.DTOs.QuestionBank;
using Application.Mappings;
using Application.Services;
using Application.Validators.QuestionBank;
using AutoMapper;
using Domain.Entities.QuestionBank;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class RubricChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var validator = new UpdateQuestionRequestValidator();
        UpdateQuestionRequest Request(double weight = 100) => new()
        {
            Content = "Updated question", BloomLevel = BloomLevel.Apply,
            Rubrics = [new() { Criteria = "Updated rubric", Weight = weight, MaxScore = 10 }]
        };
        check(validator.Validate(Request()).IsValid, "replacement rubric weight 100 accepted");
        check(!validator.Validate(Request(99)).IsValid, "invalid rubric total rejected");
        check(!validator.Validate(Request(double.NaN)).IsValid, "NaN weight rejected");
        var bad = Request(); bad.Rubrics[0].MaxScore = double.PositiveInfinity;
        check(!validator.Validate(bad).IsValid, "infinite rubric max score rejected");
        bad = Request(); bad.Rubrics = null!;
        check(!validator.Validate(bad).IsValid, "null rubric collection rejected without exception");
        bad = Request(); bad.Rubrics = [null!];
        check(!validator.Validate(bad).IsValid, "null rubric element rejected without exception");
        var createValidator = new CreateQuestionRequestValidator();
        check(!createValidator.Validate(new CreateQuestionRequest
        {
            CourseId = Guid.NewGuid(), Content = "Q", Rubrics = [null!]
        }).IsValid, "create null rubric element rejected without exception");

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        QuestionBankService Service(AivesDbContext context) => new(new UnitOfWork(context), mapper,
            new QuestionQueryParametersValidator(), new CreateQuestionRequestValidator(), validator);
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var oldRubricId = Guid.NewGuid();
        using (var context = new AivesDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Courses.Add(new Course { Id = courseId, Code = "C09", Name = "Rubric check" });
            context.Questions.Add(new Question
            {
                Id = questionId, CourseId = courseId, Content = "Original question",
                Rubrics = [new() { Id = oldRubricId, QuestionId = questionId, Criteria = "Old rubric", Weight = 100, MaxScore = 10 }]
            });
            await context.SaveChangesAsync();
        }
        Guid replacementId;
        using (var context = new AivesDbContext(options))
        {
            var service = Service(context);
            try { await service.UpdateQuestionAsync(questionId, Request(99)); check(false, "invalid service update rejected"); }
            catch (Application.Exceptions.ValidationException) { check(true, "invalid service update rejected"); }
            check((await context.Questions.SingleAsync()).Content == "Original question", "invalid update leaves question unchanged");
            var result = await service.UpdateQuestionAsync(questionId, Request());
            replacementId = result!.Rubrics.Single().Id;
            check(replacementId != oldRubricId && result.Content == "Updated question", "replace returns new active rubric only in tracked context");
            check((await context.Rubrics.IgnoreQueryFilters().SingleAsync(r => r.Id == oldRubricId)).IsDeleted, "old rubric soft-deleted and retained");
        }
        using (var context = new AivesDbContext(options))
        {
            var service = Service(context);
            var result = await service.GetQuestionByIdAsync(questionId);
            check(result!.Rubrics.Single().Id == replacementId, "fresh context reload only returns replacement rubric");
            check(await context.Rubrics.IgnoreQueryFilters().CountAsync() == 2, "replacement inserts rather than deleting history");
            result = await service.UpdateQuestionAsync(questionId, Request());
            check(result!.Rubrics.Count == 1 && await context.Rubrics.IgnoreQueryFilters().CountAsync() == 3, "repeat replacement keeps one active rubric and all historical rows");
            check(await service.DeleteQuestionAsync(questionId), "soft delete existing question succeeds");
            check(await service.GetQuestionByIdAsync(questionId) == null, "deleted question hidden in same context");
            check(await service.UpdateQuestionAsync(questionId, Request()) == null, "deleted question cannot be updated");
            check(!await service.DeleteQuestionAsync(questionId), "repeat delete returns missing");
        }
        using (var context = new AivesDbContext(options))
        {
            var service = Service(context);
            check(await service.GetQuestionByIdAsync(questionId) == null, "deleted question hidden in fresh context");
            check(await context.Questions.IgnoreQueryFilters().CountAsync() == 1, "soft delete retains question row");
            check(await context.Rubrics.CountAsync() == 0, "deleted rubrics excluded by query filter");
            check(await context.Rubrics.IgnoreQueryFilters().AllAsync(r => r.IsDeleted), "all rubric history remains deleted");
            var page = await service.GetQuestionsAsync(new());
            check(page.TotalCount == 0 && !page.Items.Any(), "deleted question excluded from list and total count");

            var otherCourseId = Guid.NewGuid();
            context.Courses.Add(new Course { Id = otherCourseId, Code = "OTHER", Name = "Other" });
            context.Questions.AddRange(
                new Question { Id = Guid.NewGuid(), CourseId = courseId, Content = "Explain Onion A", BloomLevel = BloomLevel.Analyze },
                new Question { Id = Guid.NewGuid(), CourseId = courseId, Content = "Explain Onion B", BloomLevel = BloomLevel.Analyze },
                new Question { Id = Guid.NewGuid(), CourseId = courseId, Content = "Explain Onion C", BloomLevel = BloomLevel.Remember },
                new Question { Id = Guid.NewGuid(), CourseId = otherCourseId, Content = "Explain Onion D", BloomLevel = BloomLevel.Analyze });
            await context.SaveChangesAsync();
            var filtered = new QuestionQueryParameters { CourseId = courseId, BloomLevel = BloomLevel.Analyze, SearchTerm = " Onion ", PageSize = 1 };
            var first = await service.GetQuestionsAsync(filtered);
            filtered.PageIndex = 2;
            var second = await service.GetQuestionsAsync(filtered);
            check(first.TotalCount == 2 && first.Items.Count() == 1 && second.TotalCount == 2, "relational filters combine course/Bloom/search before pagination");
            check(first.Items.Single().Id != second.Items.Single().Id && first.TotalPages == 2, "relational pagination returns distinct pages with filtered totals");
        }
        Console.WriteLine("SQLite persistence checks passed; live SQL Server not used.");
    }
}
