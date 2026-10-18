using Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Security;

public sealed class ActiveUserJwtEvents(IUnitOfWork unitOfWork) : JwtBearerEvents
{
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return WriteProblem(context.HttpContext, StatusCodes.Status401Unauthorized,
            "Authentication required.", "A valid access token for an active account is required.");
    }

    public override Task Forbidden(ForbiddenContext context) =>
        WriteProblem(context.HttpContext, StatusCodes.Status403Forbidden,
            "Access denied.", "You do not have permission to perform this action.");

    private static Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status, Title = title, Detail = detail, Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(problem));
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) || id == Guid.Empty)
        {
            context.Fail("Invalid token identity.");
            return;
        }
        var user = await unitOfWork.Users.GetByIdAsync(id);
        if (user == null || user.IsDeleted || user.Role is not ("Student" or "Lecturer") ||
            user.Role != context.Principal?.FindFirst("role")?.Value)
            context.Fail("Account is unavailable or token role is stale.");
    }
}
