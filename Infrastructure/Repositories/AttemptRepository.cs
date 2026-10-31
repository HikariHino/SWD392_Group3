using Application.Exceptions;
using Application.Interfaces.Repositories;
using Domain.Entities.Interviews;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace Infrastructure.Repositories;
public class AttemptRepository(AivesDbContext db) : IAttemptRepository
{
    public Task<ExamSession?> Session(Guid id) => db.ExamSessions.Include(x => x.Course).Include(x => x.ExamSessionQuestions).SingleOrDefaultAsync(x => x.Id == id);
    public Task<Transcript?> Find(Guid sessionId, Guid userId) => db.Transcripts.IgnoreQueryFilters().Include(x => x.Turns).SingleOrDefaultAsync(x => x.SessionId == sessionId && x.UserId == userId);
    public Task<Transcript?> Get(Guid id) => db.Transcripts.Include(x => x.Turns).SingleOrDefaultAsync(x => x.Id == id);
    public void Add(Transcript attempt) => db.Transcripts.Add(attempt);
    public void AddTurn(TranscriptTurn turn) => db.TranscriptTurns.Add(turn);
    public async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Session or attempt changed. Retry the request."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ConflictException("Concurrent duplicate request. Retry the request."); }
    }
}
