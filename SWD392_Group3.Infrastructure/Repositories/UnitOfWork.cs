using SWD392_Group3.Application.Interfaces.Repositories;
using SWD392_Group3.Infrastructure.Persistence;

namespace SWD392_Group3.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AivesDbContext _context;

    public UnitOfWork(AivesDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
