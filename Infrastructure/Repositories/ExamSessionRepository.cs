using Application.Exceptions;
using Application.Interfaces.Repositories;
using Domain.Entities.Interviews;
using Domain.Entities.QuestionBank;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;
public class ExamSessionRepository(AivesDbContext db) : IExamSessionRepository
{
    public Task<bool> CourseExists(Guid id) => db.Courses.AnyAsync(x => x.Id == id);
    public Task<List<Question>> Questions(IReadOnlyCollection<Guid> ids) => db.Questions.Include(x => x.Rubrics).Where(x => ids.Contains(x.Id)).ToListAsync();
    public Task<ExamSession?> Get(Guid id) => db.ExamSessions.Include(x => x.ExamSessionQuestions).Include(x => x.Transcripts).SingleOrDefaultAsync(x => x.Id == id);
    public void Add(ExamSession session) => db.ExamSessions.Add(session);
    public async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Session or attempt was changed. Reload and retry."); }
    }
}
