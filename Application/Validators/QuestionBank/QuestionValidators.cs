using FluentValidation;
using Application.DTOs.QuestionBank;

namespace Application.Validators.QuestionBank;

public class QuestionQueryParametersValidator : AbstractValidator<QuestionQueryParameters>
{
    public QuestionQueryParametersValidator()
    {
        RuleFor(q => q.PageIndex).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q).Must(q => (long)(q.PageIndex - 1L) * q.PageSize <= int.MaxValue)
            .OverridePropertyName(nameof(QuestionQueryParameters.PageIndex))
            .WithMessage("Page offset exceeds the supported range.")
            .When(q => q.PageIndex >= 1 && q.PageSize is >= 1 and <= 100);
        RuleFor(q => q.CourseId).Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("CourseId must be a non-empty UUID when supplied.");
        RuleFor(q => q.BloomLevel).IsInEnum().When(q => q.BloomLevel.HasValue);
        RuleFor(q => q.SearchTerm).MaximumLength(2000);
    }
}

public class CreateRubricRequestValidator : AbstractValidator<CreateRubricRequest>
{
    public CreateRubricRequestValidator()
    {
        RuleFor(r => r.Criteria)
            .NotEmpty().WithMessage("Tiêu chí Rubric không được để trống.")
            .MaximumLength(500).WithMessage("Tiêu chí Rubric không được vượt quá 500 ký tự.");

        RuleFor(r => r.Weight)
            .GreaterThan(0).WithMessage("Trọng số (Weight) của tiêu chí phải lớn hơn 0.")
            .LessThanOrEqualTo(100).WithMessage("Trọng số (Weight) không được vượt quá 100%.");

        RuleFor(r => r.MaxScore)
            .GreaterThan(0).WithMessage("Điểm tối đa (MaxScore) phải lớn hơn 0.")
            .LessThanOrEqualTo(100).WithMessage("Điểm tối đa không được vượt quá 100.");
    }
}

public class CreateQuestionRequestValidator : AbstractValidator<CreateQuestionRequest>
{
    public CreateQuestionRequestValidator()
    {
        RuleFor(q => q.Content)
            .NotEmpty().WithMessage("Nội dung câu hỏi không được để trống.")
            .MaximumLength(2000).WithMessage("Nội dung câu hỏi không được vượt quá 2000 ký tự.");

        RuleFor(q => q.CourseId)
            .NotEmpty().WithMessage("Vui lòng chọn môn học hợp lệ.");

        RuleFor(q => q.BloomLevel)
            .IsInEnum().WithMessage("Mức độ nhận thức (Bloom Level) không hợp lệ.");

        RuleFor(q => q.Rubrics)
            .NotEmpty().WithMessage("Câu hỏi phải có ít nhất 1 tiêu chí Rubric đánh giá.")
            .Must(rubrics => rubrics == null || Math.Abs(rubrics.Sum(r => r.Weight) - 100.0) < 0.01)
            .WithMessage("Tổng trọng số (%) của tất cả các tiêu chí Rubric phải bằng đúng 100%.");

        RuleForEach(q => q.Rubrics)
            .SetValidator(new CreateRubricRequestValidator());
    }
}

public class UpdateQuestionRequestValidator : AbstractValidator<UpdateQuestionRequest>
{
    public UpdateQuestionRequestValidator()
    {
        RuleFor(q => q.Content)
            .NotEmpty().WithMessage("Nội dung câu hỏi không được để trống.")
            .MaximumLength(2000).WithMessage("Nội dung câu hỏi không được vượt quá 2000 ký tự.");

        RuleFor(q => q.BloomLevel)
            .IsInEnum().WithMessage("Mức độ nhận thức (Bloom Level) không hợp lệ.");

        RuleFor(q => q.Rubrics)
            .NotEmpty().WithMessage("Câu hỏi phải có ít nhất 1 tiêu chí Rubric đánh giá.")
            .Must(rubrics => rubrics == null || Math.Abs(rubrics.Sum(r => r.Weight) - 100.0) < 0.01)
            .WithMessage("Tổng trọng số (%) của tất cả các tiêu chí Rubric phải bằng đúng 100%.");

        RuleForEach(q => q.Rubrics)
            .SetValidator(new CreateRubricRequestValidator());
    }
}
