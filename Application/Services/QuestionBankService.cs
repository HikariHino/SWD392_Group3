using AutoMapper;
using Application.Common;
using Application.DTOs.QuestionBank;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities.QuestionBank;

namespace Application.Services;

public class QuestionBankService : IQuestionBankService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuestionBankService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<QuestionDto>> GetQuestionsAsync(QuestionQueryParameters query)
    {
        var (items, totalCount) = await _unitOfWork.Questions.GetPagedQuestionsAsync(
            query.CourseId,
            query.BloomLevel,
            query.SearchTerm,
            query.PageIndex,
            query.PageSize);

        var dtos = _mapper.Map<IEnumerable<QuestionDto>>(items);

        return new PagedResponse<QuestionDto>(dtos, totalCount, query.PageIndex, query.PageSize);
    }

    public async Task<QuestionDto?> GetQuestionByIdAsync(Guid id)
    {
        var question = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        if (question == null) return null;

        return _mapper.Map<QuestionDto>(question);
    }

    public async Task<QuestionDto> CreateQuestionAsync(CreateQuestionRequest request)
    {
        var question = _mapper.Map<Question>(request);
        question.Id = Guid.NewGuid();

        // Ensure Rubrics have IDs and link to Question
        foreach (var rubric in question.Rubrics)
        {
            rubric.Id = Guid.NewGuid();
            rubric.QuestionId = question.Id;
        }

        await _unitOfWork.Questions.AddAsync(question);
        await _unitOfWork.SaveChangesAsync();

        // Reload with details for mapping
        var created = await _unitOfWork.Questions.GetWithRubricsAsync(question.Id);
        return _mapper.Map<QuestionDto>(created ?? question);
    }

    public async Task<QuestionDto?> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request)
    {
        var existingQuestion = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        if (existingQuestion == null) return null;

        // Update question attributes
        existingQuestion.Content = request.Content;
        existingQuestion.BloomLevel = request.BloomLevel;

        // Clear and replace rubrics
        existingQuestion.Rubrics.Clear();
        foreach (var rReq in request.Rubrics)
        {
            existingQuestion.Rubrics.Add(new Rubric
            {
                Id = Guid.NewGuid(),
                QuestionId = id,
                Criteria = rReq.Criteria,
                Weight = rReq.Weight,
                MaxScore = rReq.MaxScore
            });
        }

        _unitOfWork.Questions.Update(existingQuestion);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        return _mapper.Map<QuestionDto>(updated ?? existingQuestion);
    }

    public async Task<bool> DeleteQuestionAsync(Guid id)
    {
        var question = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        if (question == null) return false;

        // Soft delete question and rubrics
        question.IsDeleted = true;
        foreach (var rubric in question.Rubrics)
        {
            rubric.IsDeleted = true;
        }

        _unitOfWork.Questions.Update(question);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ImportQuestionsAsync(string filePath)
    {
        // Mock import logic - ready for expansion
        return await Task.FromResult(true);
    }

    public async Task<IEnumerable<Course>> GetCoursesAsync()
    {
        return await _unitOfWork.Courses.GetAllAsync();
    }
}
