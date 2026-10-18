using Microsoft.OpenApi;

namespace WebApi.Swagger;

public static class SwaggerRegistration
{
    public static IServiceCollection AddAivesSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.DocumentFilter<AuthorizationDocumentFilter>();
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
            });
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AIVES API - AI-Powered Viva Exam System", Version = "v1",
                Description = "API hệ thống thi vấn đáp trực tuyến AIVES - SWD392 Group 3 (.NET 10 Onion Architecture)"
            });
        });
        return services;
    }
}
