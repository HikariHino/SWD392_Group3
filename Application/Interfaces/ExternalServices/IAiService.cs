using Application.DTOs.QuestionBank;

namespace Application.Interfaces.ExternalServices;

public interface IAiService
{
    Task<AiGeneratedQuestionDto> GenerateQuestionAsync(AiGenerateQuestionRequest request);
}
