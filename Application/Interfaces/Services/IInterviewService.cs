using Application.DTOs.Interviews;

namespace Application.Interfaces.Services
{
    public interface IInterviewService
    {
        Task<FollowUpTurnDto> GenerateFollowUpAsync(Guid attemptId, CancellationToken cancellationToken = default);
    }
}
