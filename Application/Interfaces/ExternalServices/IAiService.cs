using Application.DTOs.QuestionBank;
using Application.DTOs.Interviews;

namespace Application.Interfaces.ExternalServices;

public interface IAiService
{
    Task<AiGeneratedQuestionDto> GenerateQuestionAsync(AiGenerateQuestionRequest request, CancellationToken cancellationToken = default);
    Task<AiFollowUpDto> GenerateFollowUpAsync(AiFollowUpRequest request, CancellationToken cancellationToken = default);
    Task<AiAssessmentResponse> GenerateAssessmentAsync(AiAssessmentRequest request, CancellationToken cancellationToken = default);
}
