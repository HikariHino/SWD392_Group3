using AutoMapper;
using Application.Common;
using Application.DTOs.QuestionBank;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities.QuestionBank;
using FluentValidation;

namespace Application.Services;

public class QuestionBankService : IQuestionBankService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<QuestionQueryParameters> _queryValidator;
    private readonly IValidator<CreateQuestionRequest> _createValidator;
    private readonly IValidator<UpdateQuestionRequest> _updateValidator;
    private readonly Application.Interfaces.ExternalServices.IAiService? _aiService;

    public QuestionBankService(IUnitOfWork unitOfWork, IMapper mapper,
        IValidator<QuestionQueryParameters> queryValidator,
        IValidator<CreateQuestionRequest> createValidator,
        IValidator<UpdateQuestionRequest> updateValidator,
        Application.Interfaces.ExternalServices.IAiService? aiService = null)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _queryValidator = queryValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _aiService = aiService;
    }

    public async Task<PagedResponse<QuestionDto>> GetQuestionsAsync(QuestionQueryParameters query)
    {
        ThrowIfInvalid(await _queryValidator.ValidateAsync(query));
        if (query.CourseId.HasValue) await RequireActiveCourseAsync(query.CourseId.Value);

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
        ThrowIfInvalid(await _createValidator.ValidateAsync(request));
        await RequireActiveCourseAsync(request.CourseId);

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
        ThrowIfInvalid(await _updateValidator.ValidateAsync(request));
        var existingQuestion = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        if (existingQuestion == null || existingQuestion.IsDeleted) return null;

        // Update question attributes
        existingQuestion.Content = request.Content;
        existingQuestion.BloomLevel = request.BloomLevel;

        // Keep the replaced rubric rows for history, then add the new matrix.
        // The question and its included rubrics are already tracked by EF, so
        // calling Update on the whole graph would mark newly-added rubrics as
        // existing rows and cause a concurrency error on SaveChanges.
        foreach (var existingRubric in existingQuestion.Rubrics)
        {
            existingRubric.IsDeleted = true;
        }

        foreach (var rReq in request.Rubrics)
        {
            var rubric = new Rubric
            {
                Id = Guid.NewGuid(),
                QuestionId = id,
                Criteria = rReq.Criteria,
                Weight = rReq.Weight,
                MaxScore = rReq.MaxScore
            };
            existingQuestion.Rubrics.Add(rubric);
            // Explicitly mark replacements Added even though their Guid is assigned.
            await _unitOfWork.Rubrics.AddAsync(rubric);
        }

        // Save the tracked question and explicitly-added replacements together.
        await _unitOfWork.SaveChangesAsync();

        var updated = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        var result = updated ?? existingQuestion;
        var dto = _mapper.Map<QuestionDto>(result);
        // A tracked navigation can still contain the rubric rows that were
        // soft-deleted above, even though the database query filter excludes
        // them. Keep the PUT response consistent with subsequent GET calls.
        dto.Rubrics = _mapper.Map<List<RubricDto>>(result.Rubrics.Where(r => !r.IsDeleted));
        return dto;
    }

    public async Task<bool> DeleteQuestionAsync(Guid id)
    {
        var question = await _unitOfWork.Questions.GetWithRubricsAsync(id);
        if (question == null || question.IsDeleted) return false;

        // Soft delete question and rubrics
        question.IsDeleted = true;
        foreach (var rubric in question.Rubrics)
        {
            rubric.IsDeleted = true;
        }

        // The loaded graph is tracked; retain soft-deleted rows without removing them.
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ImportQuestionsAsync(string filePath)
    {
        // Mock import logic - ready for expansion
        return await Task.FromResult(true);
    }

    public async Task<AiGeneratedQuestionDto> GenerateQuestionWithAiAsync(AiGenerateQuestionRequest request)
    {
        if (request.CourseId != Guid.Empty)
        {
            await RequireActiveCourseAsync(request.CourseId);
        }

        if (_aiService != null)
        {
            return await _aiService.GenerateQuestionAsync(request);
        }

        return new AiGeneratedQuestionDto
        {
            CourseId = request.CourseId,
            Content = $"Hãy giải thích và phân tích nguyên lý, ưu nhược điểm của chủ đề '{request.Topic}' ở mức độ nhận thức {request.BloomLevel}?",
            BloomLevel = request.BloomLevel,
            SourceContext = "Giáo trình & Slide bài giảng chuyên ngành",
            AiConfidence = 95.0,
            Rubrics = new List<CreateRubricRequest>
            {
                new() { Criteria = $"Hiểu rõ khái niệm và cơ chế hoạt động cốt lõi của {request.Topic}", Weight = 30.0, MaxScore = 3.0 },
                new() { Criteria = "Phân tích và so sánh các khía cạnh kỹ thuật liên quan", Weight = 40.0, MaxScore = 4.0 },
                new() { Criteria = "Đưa ra giải pháp tối ưu và ví dụ thực tiễn", Weight = 30.0, MaxScore = 3.0 }
            }
        };
    }

    public async Task<int> ImportQuestionsBatchAsync(ImportQuestionsRequest request)
    {
        if (request.CourseId != Guid.Empty)
        {
            await RequireActiveCourseAsync(request.CourseId);
        }

        int count = 0;
        foreach (var qReq in request.Questions)
        {
            ThrowIfInvalid(await _createValidator.ValidateAsync(qReq));
            var question = _mapper.Map<Question>(qReq);
            question.Id = Guid.NewGuid();
            question.CourseId = request.CourseId != Guid.Empty ? request.CourseId : qReq.CourseId;

            foreach (var r in question.Rubrics)
            {
                r.Id = Guid.NewGuid();
                r.QuestionId = question.Id;
            }

            await _unitOfWork.Questions.AddAsync(question);
            count++;
        }

        await _unitOfWork.SaveChangesAsync();
        return count;
    }

    public async Task<IEnumerable<CourseDto>> GetCoursesAsync()
    {
        return _mapper.Map<IEnumerable<CourseDto>>((await _unitOfWork.Courses.GetAllAsync()).Where(c => !c.IsDeleted));
    }

    private async Task RequireActiveCourseAsync(Guid courseId)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(courseId);
        if (course == null || course.IsDeleted)
        {
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateQuestionRequest.CourseId)] = ["Course does not exist or has been deleted."]
            });
        }
    }

    private static void ThrowIfInvalid(FluentValidation.Results.ValidationResult validation)
    {
        if (validation.IsValid) return;
        throw new Application.Exceptions.ValidationException(validation.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
    }
}
