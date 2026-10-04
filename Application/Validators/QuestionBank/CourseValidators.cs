using Application.DTOs.QuestionBank;
using FluentValidation;

namespace Application.Validators.QuestionBank;

public abstract class CourseRequestValidator<T> : AbstractValidator<T> where T : CourseRequest
{
    protected CourseRequestValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

public sealed class CreateCourseRequestValidator : CourseRequestValidator<CreateCourseRequest> { }
public sealed class UpdateCourseRequestValidator : CourseRequestValidator<UpdateCourseRequest> { }
