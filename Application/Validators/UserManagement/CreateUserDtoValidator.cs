using Application.DTOs.UserManagement;
using FluentValidation;

namespace Application.Validators.UserManagement;

public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(50);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .Must(password => password == null || System.Text.Encoding.UTF8.GetByteCount(password) <= 72)
            .WithMessage("Password must not exceed 72 UTF-8 bytes for BCrypt.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(100);

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => role == "Student" || role == "Lecturer")
            .WithMessage("Role must be Student or Lecturer.");
    }
}
