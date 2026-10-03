using Application.DTOs.UserManagement;
using FluentValidation;

namespace Application.Validators.UserManagement;

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().MaximumLength(100)
            .When(x => x.FullName != null);

        RuleFor(x => x.Role)
            .Must(role => role == "Student" || role == "Lecturer")
            .WithMessage("Role must be Student or Lecturer.")
            .When(x => x.Role != null);
    }
}
