using Application.DTOs.Auth;
using Domain.Entities.UserManagement;

namespace Application.Interfaces.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
}

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
