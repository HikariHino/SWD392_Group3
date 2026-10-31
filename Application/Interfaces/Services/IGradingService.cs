using Application.DTOs.Interviews;

namespace Application.Interfaces.Services;

public interface IGradingService
{
    Task<IReadOnlyList<AssessmentDto>> GradeAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssessmentDto>> GetAttemptAssessmentsAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<AssessmentDto> FinalizeAssessmentAsync(Guid assessmentId, FinalizeAssessmentRequest request, CancellationToken cancellationToken = default);
    Task<AttemptResultDto> GetAttemptResultAsync(Guid attemptId, CancellationToken cancellationToken = default);
}
