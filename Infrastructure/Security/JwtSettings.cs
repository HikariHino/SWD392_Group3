using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience) ||
            Encoding.UTF8.GetByteCount(SigningKey) < 32 || ExpiryMinutes is < 1 or > 120)
            throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience, Jwt:SigningKey (at least 32 UTF-8 bytes), and Jwt:ExpiryMinutes (1-120) externally.");
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero,
        NameClaimType = "name", RoleClaimType = "role"
    };
}
