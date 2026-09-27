using Domain.Entities.QuestionBank;
using Domain.Enums;

namespace Application.Interfaces.Repositories;

public interface IQuestionRepository : IGenericRepository<Question>
{
    Task<Question?> GetWithRubricsAsync(Guid id);
    Task<(IEnumerable<Question> Items, int TotalCount)> GetPagedQuestionsAsync(
        Guid? courseId,
        BloomLevel? bloomLevel,
        string? searchTerm,
        int pageIndex,
        int pageSize);
}
