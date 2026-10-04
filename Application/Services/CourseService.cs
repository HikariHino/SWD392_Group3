using Application.DTOs.QuestionBank;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using AutoMapper;
using Domain.Entities.QuestionBank;
using FluentValidation;

namespace Application.Services;

public class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateCourseRequest> _createValidator;
    private readonly IValidator<UpdateCourseRequest> _updateValidator;

    public CourseService(IUnitOfWork unitOfWork, IMapper mapper,
        IValidator<CreateCourseRequest> createValidator, IValidator<UpdateCourseRequest> updateValidator)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IEnumerable<CourseDto>> GetCoursesAsync() =>
        _mapper.Map<IEnumerable<CourseDto>>((await _unitOfWork.Courses.GetAllAsync()).Where(c => !c.IsDeleted));

    public async Task<CourseDto> GetCourseByIdAsync(Guid id) => _mapper.Map<CourseDto>(await GetActiveAsync(id));

    public async Task<CourseDto> CreateCourseAsync(CreateCourseRequest request)
    {
        var normalized = new CreateCourseRequest
        {
            Code = request.Code?.Trim().ToUpperInvariant() ?? "",
            Name = request.Name?.Trim() ?? "", Description = request.Description?.Trim()
        };
        ThrowIfInvalid(await _createValidator.ValidateAsync(normalized));
        await RequireAvailableCodeAsync(normalized.Code);
        var course = new Course { Id = Guid.NewGuid(), Code = normalized.Code, Name = normalized.Name, Description = normalized.Description };
        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<CourseDto>(course);
    }

    public async Task<CourseDto> UpdateCourseAsync(Guid id, UpdateCourseRequest request)
    {
        var normalized = new UpdateCourseRequest
        {
            Code = request.Code?.Trim().ToUpperInvariant() ?? "",
            Name = request.Name?.Trim() ?? "", Description = request.Description?.Trim()
        };
        ThrowIfInvalid(await _updateValidator.ValidateAsync(normalized));
        var course = await GetActiveAsync(id);
        await RequireAvailableCodeAsync(normalized.Code, id);
        course.Code = normalized.Code;
        course.Name = normalized.Name;
        course.Description = normalized.Description;
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<CourseDto>(course);
    }

    public async Task DeleteCourseAsync(Guid id)
    {
        var course = await GetActiveAsync(id);
        if ((await _unitOfWork.Questions.FindAsync(q => q.CourseId == id && !q.IsDeleted)).Any())
            throw new ConflictException("Delete the active questions before deleting their course.");
        course.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Course> GetActiveAsync(Guid id)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        if (course == null || course.IsDeleted) throw new NotFoundException("Course not found.");
        return course;
    }

    private async Task RequireAvailableCodeAsync(string code, Guid? excludingId = null)
    {
        if ((await _unitOfWork.Courses.FindAsync(c => !c.IsDeleted && c.Id != excludingId && c.Code.ToUpper() == code)).Any())
            throw new ConflictException("Course code already exists.");
    }

    private static void ThrowIfInvalid(FluentValidation.Results.ValidationResult result)
    {
        if (!result.IsValid) throw new Application.Exceptions.ValidationException(result.Errors
            .GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }
}
