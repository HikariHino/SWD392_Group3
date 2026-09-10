namespace Application.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    // Cáº§n thÃªm cÃ¡c IRepository cá»¥ thá»ƒ á»Ÿ Ä‘Ã¢y sau nÃ y, vÃ­ dá»¥:
    // IQuestionRepository Questions { get; }
    
    Task<int> SaveChangesAsync();
}

