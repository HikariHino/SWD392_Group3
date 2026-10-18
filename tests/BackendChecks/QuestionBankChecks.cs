using System.Linq.Expressions;
using Application.DTOs.QuestionBank;
using Application.Interfaces.Repositories;
using Application.Mappings;
using Application.Services;
using Application.Validators.QuestionBank;
using AutoMapper;
using Domain.Common;
using Domain.Entities.QuestionBank;
using Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

internal static class QuestionBankChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var validator = new QuestionQueryParametersValidator();
        check(validator.Validate(new QuestionQueryParameters()).IsValid, "default question query accepted");
        foreach (var (index, size) in new[] { (0, 10), (-1, 10), (1, 0), (1, -1), (1, 101), (int.MaxValue, 100) })
            check(!validator.Validate(new QuestionQueryParameters() { PageIndex = index, PageSize = size }).IsValid, $"invalid paging {index}/{size} rejected");
        check(validator.Validate(new QuestionQueryParameters() { PageSize = 100 }).IsValid, "maximum page size accepted");
        check(!validator.Validate(new QuestionQueryParameters() { CourseId = Guid.Empty }).IsValid, "empty query course rejected");
        check(!validator.Validate(new QuestionQueryParameters() { BloomLevel = (BloomLevel)99 }).IsValid, "invalid Bloom enum rejected");

        using var uow = new SpyUnitOfWork();
        var mapper = new MapperConfiguration(config => config.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var service = new QuestionBankService(uow, mapper, validator, new CreateQuestionRequestValidator(), new UpdateQuestionRequestValidator());
        async Task Rejected(Func<Task> operation, string field, string name)
        {
            try { await operation(); check(false, name); }
            catch (Application.Exceptions.ValidationException error) { check(error.Errors.ContainsKey(field), name); }
        }

        await Rejected(() => service.GetQuestionsAsync(new() { PageSize = 0 }), "PageSize", "service blocks invalid query before repository");
        check(uow.QuestionsSpy.QueryCalls == 0, "invalid paging never reaches data query");
        var courseId = Guid.NewGuid();
        await Rejected(() => service.GetQuestionsAsync(new() { CourseId = courseId }), "CourseId", "missing query course returns field validation error");
        var request = new CreateQuestionRequest
        {
            CourseId = courseId, Content = "Explain Onion", BloomLevel = BloomLevel.Understand,
            Rubrics = [new() { Criteria = "Correct explanation", Weight = 100, MaxScore = 10 }]
        };
        await Rejected(() => service.CreateQuestionAsync(request), "CourseId", "create blocks nonexistent course");
        check(uow.SaveCalls == 0 && uow.QuestionsSpy.AddCalls == 0, "unknown course cannot persist a question");
        await uow.Courses.AddAsync(new Course { Id = courseId, IsDeleted = true });
        await Rejected(() => service.CreateQuestionAsync(request), "CourseId", "tracked soft-deleted course rejected on create");
        await Rejected(() => service.GetQuestionsAsync(new() { CourseId = courseId }), "CourseId", "soft-deleted course rejected on query");
        ((MemoryRepository<Course>)uow.Courses).Items.Clear();
        await uow.Courses.AddAsync(new Course { Id = courseId, Code = "SWD392", Name = "Design" });
        uow.QuestionsSpy.Returned = [new Question { Id = Guid.NewGuid(), Content = "Explain Onion", CourseId = courseId }];
        var page = await service.GetQuestionsAsync(new() { CourseId = courseId, BloomLevel = BloomLevel.Analyze, SearchTerm = "Onion", PageIndex = 2, PageSize = 10 });
        check(uow.QuestionsSpy.LastQuery == (courseId, BloomLevel.Analyze, "Onion", 2, 10), "course/Bloom/search/paging forwarded unchanged");
        check(page.PageIndex == 2 && page.PageSize == 10 && page.TotalCount == 21 && page.TotalPages == 3 && page.HasNextPage && page.HasPreviousPage, "paging metadata preserves filtered repository total");
        check(page.Items.Single().Content == "Explain Onion", "paged entities mapped to DTOs");
        await service.CreateQuestionAsync(request);
        check(uow.SaveCalls == 1 && uow.QuestionsSpy.AddCalls == 1, "valid course allows question persistence");
        check(uow.QuestionsSpy.Added!.Rubrics.Single().QuestionId == uow.QuestionsSpy.Added.Id, "created rubric linked to generated question id");
    }
}

internal class MemoryRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    public List<T> Items { get; } = [];
    public Task<T?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
    public Task<IEnumerable<T>> GetAllAsync() => Task.FromResult<IEnumerable<T>>(Items);
    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(Items.Where(predicate.Compile()));
    public virtual Task AddAsync(T item) { Items.Add(item); return Task.CompletedTask; }
    public Task AddRangeAsync(IEnumerable<T> items) { Items.AddRange(items); return Task.CompletedTask; }
    public void Update(T item) { }
    public void Remove(T item) => Items.Remove(item);
    public void RemoveRange(IEnumerable<T> items) { foreach (var item in items.ToArray()) Items.Remove(item); }
}

internal sealed class SpyQuestions : MemoryRepository<Question>, IQuestionRepository
{
    public int QueryCalls, AddCalls;
    public Question? Added;
    public Question[] Returned = [];
    public (Guid?, BloomLevel?, string?, int, int) LastQuery;
    public Task<Question?> GetWithRubricsAsync(Guid id) => GetByIdAsync(id);
    public override Task AddAsync(Question item) { AddCalls++; Added = item; return base.AddAsync(item); }
    public Task<(IEnumerable<Question> Items, int TotalCount)> GetPagedQuestionsAsync(Guid? courseId, BloomLevel? bloomLevel, string? searchTerm, int pageIndex, int pageSize)
    {
        QueryCalls++;
        LastQuery = (courseId, bloomLevel, searchTerm, pageIndex, pageSize);
        return Task.FromResult<(IEnumerable<Question>, int)>((Returned, 21));
    }
}

internal sealed class SpyUnitOfWork : IUnitOfWork
{
    public SpyQuestions QuestionsSpy { get; } = new();
    public int SaveCalls;
    public IQuestionRepository Questions => QuestionsSpy;
    public IGenericRepository<Course> Courses { get; } = new MemoryRepository<Course>();
    public IGenericRepository<Rubric> Rubrics { get; } = new MemoryRepository<Rubric>();
    public IUserRepository Users => throw new NotSupportedException();
    public Task<int> SaveChangesAsync() { SaveCalls++; return Task.FromResult(1); }
    public void Dispose() { }
}
