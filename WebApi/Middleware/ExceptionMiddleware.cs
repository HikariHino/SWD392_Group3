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
