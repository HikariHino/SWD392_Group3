using Domain.Entities.QuestionBank;
using Domain.Enums;

namespace Application.Interfaces.Repositories;

public interface IQuestionRepository : IGenericRepository<Question>
{
    // Returns an active question with active rubrics, tracked for updates by the unit of work.
    Task<Question?> GetWithRubricsAsync(Guid id);
    Task<(IEnumerable<Question> Items, int TotalCount)> GetPagedQuestionsAsync(
        Guid? courseId,
        BloomLevel? bloomLevel,
        string? searchTerm,
        int pageIndex,
        int pageSize);
}
