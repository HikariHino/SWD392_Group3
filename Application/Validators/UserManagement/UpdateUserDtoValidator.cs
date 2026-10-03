using Application.DTOs.UserManagement;
using FluentValidation;

namespace Application.Validators.UserManagement;

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.Role)
            .Must(role => string.IsNullOrEmpty(role) || role == "Student" || role == "Lecturer" || role == "Admin")
            .WithMessage("Role must be Student, Lecturer, or Admin.");
    }
}
