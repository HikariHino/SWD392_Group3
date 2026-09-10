namespace SWD392_Group3.Application.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    // Cần thêm các IRepository cụ thể ở đây sau này, ví dụ:
    // IQuestionRepository Questions { get; }
    
    Task<int> SaveChangesAsync();
}
