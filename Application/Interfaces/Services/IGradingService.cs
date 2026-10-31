using Application.DTOs.Interviews;

namespace Application.Interfaces.Services;

public interface IGradingService
{
    Task<IReadOnlyList<AssessmentDto>> GradeAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);
}
