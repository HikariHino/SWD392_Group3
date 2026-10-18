using Application.DTOs.Auth;
using Application.DTOs.UserManagement;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using FluentValidation;

namespace Application.Services;

public sealed class AuthService(IUnitOfWork unitOfWork, IPasswordHasher hasher,
    IAccessTokenIssuer tokens, IValidator<LoginRequest> validator) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            throw new Exceptions.ValidationException(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));

        var user = await unitOfWork.Users.GetByUsernameAsync(request.Username.Trim());
        if (user == null || user.IsDeleted || user.Role is not ("Student" or "Lecturer") ||
            !hasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var token = tokens.Issue(user);
        return new(token.Value, token.ExpiresAt, new UserDto
        {
            Id = user.Id, Username = user.Username, FullName = user.FullName,
            Role = user.Role, CreatedAt = user.CreatedAt
        });
    }
}
