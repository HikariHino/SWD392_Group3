using Domain.Entities.Interviews;

namespace Application.Interfaces.Repositories;

public interface IGradingRepository
{
    Task<Transcript?> Attempt(Guid id);
    Task<ExamSession?> Session(Guid id);
    Task<IReadOnlyList<Assessment>> Assessments(Guid transcriptId);
    Task<Assessment?> Assessment(Guid id);
    void AddRange(IEnumerable<Assessment> assessments);
    Task Save();
}
