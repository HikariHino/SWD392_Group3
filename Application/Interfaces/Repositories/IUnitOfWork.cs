using Domain.Entities.QuestionBank;

namespace Application.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    IQuestionRepository Questions { get; }
    IGenericRepository<Course> Courses { get; }
    IGenericRepository<Rubric> Rubrics { get; }
    IUserRepository Users { get; }

    Task<int> SaveChangesAsync();
}
