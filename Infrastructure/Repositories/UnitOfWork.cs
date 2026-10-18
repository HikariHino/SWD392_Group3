using Application.Interfaces.Repositories;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AivesDbContext _context;
    private IQuestionRepository? _questions;
    private IUserRepository? _users;
    private IGenericRepository<Course>? _courses;
    private IGenericRepository<Rubric>? _rubrics;

    public UnitOfWork(AivesDbContext context)
    {
        _context = context;
    }

    public IQuestionRepository Questions => _questions ??= new QuestionRepository(_context);
    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IGenericRepository<Course> Courses => _courses ??= new GenericRepository<Course>(_context);
    public IGenericRepository<Rubric> Rubrics => _rubrics ??= new GenericRepository<Rubric>(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
