using Domain.Entities.Interviews;
namespace Application.Interfaces.Repositories;
public interface IAttemptRepository
{
    Task<ExamSession?> Session(Guid id);
    Task<Transcript?> Find(Guid sessionId, Guid userId);
    Task<Transcript?> Get(Guid id);
    void Add(Transcript attempt);
    void AddTurn(TranscriptTurn turn);
    Task Save();
}
