using Application.Common;
using Application.DTOs.QuestionBank;
using Domain.Entities.QuestionBank;

namespace Application.Interfaces.Services;

public interface IQuestionBankService
{
    Task<PagedResponse<QuestionDto>> GetQuestionsAsync(QuestionQueryParameters query);
    Task<QuestionDto?> GetQuestionByIdAsync(Guid id);
    Task<QuestionDto> CreateQuestionAsync(CreateQuestionRequest request);
    Task<QuestionDto?> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request);
    Task<bool> DeleteQuestionAsync(Guid id);
    Task<bool> ImportQuestionsAsync(string filePath);
    Task<IEnumerable<CourseDto>> GetCoursesAsync();
}
