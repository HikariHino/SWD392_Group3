using Application.Exceptions;
using Application.Interfaces.Repositories;
using Domain.Entities.Interviews;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class GradingRepository(AivesDbContext db) : IGradingRepository
{
    public Task<Transcript?> Attempt(Guid id) => db.Transcripts.Include(x => x.Turns).SingleOrDefaultAsync(x => x.Id == id);
    public Task<ExamSession?> Session(Guid id) => db.ExamSessions.Include(x => x.ExamSessionQuestions).SingleOrDefaultAsync(x => x.Id == id);
    public async Task<IReadOnlyList<Assessment>> Assessments(Guid transcriptId) => await db.Assessments.Where(x => x.TranscriptId == transcriptId).ToListAsync();
    public void AddRange(IEnumerable<Assessment> assessments) => db.Assessments.AddRange(assessments);
    public async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Attempt changed. Reload and retry grading."); }
    }
}
