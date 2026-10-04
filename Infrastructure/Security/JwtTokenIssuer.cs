using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.DTOs.Auth;
using Application.Interfaces.Services;
using Domain.Entities.UserManagement;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public sealed class JwtTokenIssuer(JwtSettings settings, TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken Issue(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(settings.ExpiryMinutes);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim("sub", user.Id.ToString()), new Claim("name", user.Username),
             new Claim("role", user.Role), new Claim("jti", Guid.NewGuid().ToString())],
            now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
