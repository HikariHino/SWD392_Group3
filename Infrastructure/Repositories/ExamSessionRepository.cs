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
    public Task<List<Question>> Questions(IReadOnlyCollection<Guid> ids) => db.Questions.Where(x => ids.Contains(x.Id)).ToListAsync();
    public Task<List<Rubric>> Rubrics(Guid courseId) => db.Rubrics.Where(x => x.CourseId == courseId).ToListAsync();
    public Task<ExamSession?> Get(Guid id) => db.ExamSessions.Include(x => x.ExamSessionQuestions).Include(x => x.Transcripts).SingleOrDefaultAsync(x => x.Id == id);
    public Task<List<ExamSession>> ListByCreator(Guid creatorId) => db.ExamSessions
        .Where(x => x.CreatedByUserId == creatorId)
        .Include(x => x.ExamSessionQuestions)
        .OrderByDescending(x => x.StartTime)
        .ToListAsync();
    public void Add(ExamSession session) => db.ExamSessions.Add(session);
    public async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Session or attempt was changed. Reload and retry."); }
    }
}
