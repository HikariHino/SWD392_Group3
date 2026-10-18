using System.Text;
using Application.DTOs.Auth;
using FluentValidation;

namespace Application.Validators.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty()
            .Must(x => x == null || Encoding.UTF8.GetByteCount(x) <= 72)
            .WithMessage("Password must not exceed 72 UTF-8 bytes.");
    }
}
