using Microsoft.EntityFrameworkCore;
using Application.Interfaces.Repositories;
using Domain.Entities.QuestionBank;
using Domain.Enums;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public class QuestionRepository : GenericRepository<Question>, IQuestionRepository
{
    public QuestionRepository(AivesDbContext context) : base(context)
    {
    }

    public async Task<Question?> GetWithRubricsAsync(Guid id)
    {
        return await _dbSet
            .Include(q => q.Course)
            .Include(q => q.Rubrics)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
    }

    public async Task<(IEnumerable<Question> Items, int TotalCount)> GetPagedQuestionsAsync(
        Guid? courseId,
        BloomLevel? bloomLevel,
        string? searchTerm,
        int pageIndex,
        int pageSize)
    {
        var query = _dbSet
            .Include(q => q.Course)
            .Include(q => q.Rubrics)
            .AsNoTracking()
            .AsQueryable();

        if (courseId.HasValue && courseId.Value != Guid.Empty)
        {
            query = query.Where(q => q.CourseId == courseId.Value);
        }

        if (bloomLevel.HasValue)
        {
            query = query.Where(q => q.BloomLevel == bloomLevel.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(q => q.Content.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .ThenBy(q => q.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
