using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Exceptions;

namespace WebApi.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            _logger.LogError(ex, "An unhandled exception has occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "An error occurred while processing your request.";
        var detail = exception.Message;
        object? errors = null;

        switch (exception)
        {
            case ForbiddenException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Access denied.";
                break;
            case InvalidCredentialsException:
                statusCode = StatusCodes.Status401Unauthorized;
                title = "Authentication failed.";
                context.Response.Headers.WWWAuthenticate = "Bearer";
                break;
            case NotFoundException notFoundEx:
                statusCode = StatusCodes.Status404NotFound;
                title = "The specified resource was not found.";
                break;
            case ConflictException conflictEx:
                statusCode = StatusCodes.Status409Conflict;
                title = "A conflict occurred.";
                break;
            case ValidationException validationEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Validation errors occurred.";
                errors = validationEx.Errors;
                detail = validationEx.Message;
                break;
            case AiProviderTimeoutException:
                statusCode = StatusCodes.Status504GatewayTimeout;
                title = "AI provider timed out.";
                detail = "The AI provider did not respond in time. Please retry.";
                break;
            case AiProviderUnavailableException unavailableEx:
                statusCode = StatusCodes.Status503ServiceUnavailable;
                title = "AI provider is unavailable.";
                detail = unavailableEx.Message;
                break;
            case AiProviderResponseException:
                statusCode = StatusCodes.Status502BadGateway;
                title = "AI provider returned an invalid response.";
                detail = "The AI provider response could not be used. Please retry.";
                break;
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "An unexpected system error occurred.";
                detail = "Please contact support if the problem persists.";
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

        if (errors != null)
        {
            problemDetails.Extensions.Add("errors", errors);
        }

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
