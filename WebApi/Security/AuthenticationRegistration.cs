using Application.Interfaces.Services;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace WebApi.Security;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddAivesAuthentication(this IServiceCollection services, JwtSettings settings)
    {
        settings.Validate();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ActiveUserJwtEvents>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = settings.ValidationParameters();
            options.EventsType = typeof(ActiveUserJwtEvents);
        });
        services.AddAuthorization();
        return services;
    }
}
