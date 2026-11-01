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
        var question = await _unitOfWork.Questions.GetByIdAsync(id);
        if (question == null) return null;

        return _mapper.Map<QuestionDto>(question);
    }

    public async Task<QuestionDto> CreateQuestionAsync(CreateQuestionRequest request)
    {
        ThrowIfInvalid(await _createValidator.ValidateAsync(request));
        await RequireActiveCourseAsync(request.CourseId);

        var question = _mapper.Map<Question>(request);
        question.Id = Guid.NewGuid();

        await _unitOfWork.Questions.AddAsync(question);
        await _unitOfWork.SaveChangesAsync();

        // Reload with details for mapping
        var created = await _unitOfWork.Questions.GetByIdAsync(question.Id);
        return _mapper.Map<QuestionDto>(created ?? question);
    }

    public async Task<QuestionDto?> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request)
    {
        ThrowIfInvalid(await _updateValidator.ValidateAsync(request));
        var existingQuestion = await _unitOfWork.Questions.GetByIdAsync(id);
        if (existingQuestion == null || existingQuestion.IsDeleted) return null;

        // Update question attributes
        existingQuestion.Content = request.Content;
        existingQuestion.BloomLevel = request.BloomLevel;

        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<QuestionDto>(existingQuestion);
    }

    public async Task<bool> DeleteQuestionAsync(Guid id)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id);
        if (question == null || question.IsDeleted) return false;

        question.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<AiGeneratedQuestionDto> GenerateQuestionWithAiAsync(AiGenerateQuestionRequest request)
    {
        if (request.CourseId != Guid.Empty)
        {
            await RequireActiveCourseAsync(request.CourseId);
        }

        if (_aiService != null)
        {
            try
            {
                return await _aiService.GenerateQuestionAsync(request);
            }
            catch (Application.Exceptions.AiProviderException)
            {
                // Keep authoring usable during a temporary provider outage; the UI labels this as a draft.
            }
        }

        return new AiGeneratedQuestionDto
        {
            CourseId = request.CourseId,
            Content = $"Hãy giải thích và phân tích nguyên lý, ưu nhược điểm của chủ đề '{request.Topic}' ở mức độ nhận thức {request.BloomLevel}?",
            BloomLevel = request.BloomLevel,
            SourceContext = "Bản nháp dự phòng: Gemini chưa phản hồi; hãy kiểm tra và chỉnh sửa trước khi lưu.",
            AiConfidence = 0.0
        };
    }

    public async Task<int> ImportQuestionsBatchAsync(ImportQuestionsRequest request)
    {
        if (request is null || request.Questions is not { Count: > 0 })
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]> { ["file"] = ["At least one valid question is required."] });
        await RequireActiveCourseAsync(request.CourseId);

        var validated = new List<Question>(request.Questions.Count);
        foreach (var qReq in request.Questions)
        {
            qReq.CourseId = request.CourseId;
            ThrowIfInvalid(await _createValidator.ValidateAsync(qReq));
            var question = _mapper.Map<Question>(qReq);
            question.Id = Guid.NewGuid();
            question.CourseId = qReq.CourseId;
            validated.Add(question);
        }

        foreach (var question in validated)
            await _unitOfWork.Questions.AddAsync(question);
        await _unitOfWork.SaveChangesAsync();
        return validated.Count;
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
