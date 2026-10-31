using Domain.Entities.Interviews;
using Domain.Entities.QuestionBank;
namespace Application.Interfaces.Repositories;
public interface IExamSessionRepository
{
    Task<bool> CourseExists(Guid id);
    Task<List<Question>> Questions(IReadOnlyCollection<Guid> ids);
    Task<ExamSession?> Get(Guid id);
    void Add(ExamSession session);
    Task Save();
}
