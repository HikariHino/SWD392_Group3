using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;

namespace Application.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    IQuestionRepository Questions { get; }
    IUserRepository Users { get; }
    IGenericRepository<Course> Courses { get; }
    IGenericRepository<Rubric> Rubrics { get; }
    
    Task<int> SaveChangesAsync();
}
