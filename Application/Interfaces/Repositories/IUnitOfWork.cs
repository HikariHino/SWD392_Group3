using Domain.Entities.QuestionBank;

namespace Application.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    IQuestionRepository Questions { get;     IUserRepository Users { get; }
}
    IGenericRepository<Course> Courses { get;     IUserRepository Users { get; }
}
    IGenericRepository<Rubric> Rubrics { get;     IUserRepository Users { get; }
}
    
    Task<int> SaveChangesAsync();
    IUserRepository Users { get; }
}
