using GridTrack.Application.Abstractions.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            var exceptionDetails = GetExceptionDetails(exception);

            var problemDetails = new ProblemDetails
            {
                Status = exceptionDetails.Status,
                Type = exceptionDetails.Type,
                Title = exceptionDetails.Title,
                Detail = exceptionDetails.Detail
            };

            if (exceptionDetails.Errors is not null) problemDetails.Extensions["errors"] = exceptionDetails.Errors;

            context.Response.StatusCode = exceptionDetails.Status;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }

    private static ExceptionDetails GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => new ExceptionDetails(
                StatusCodes.Status400BadRequest,
                "ValidationFailure",
                "Validation error",
                "One or more validation errors has occurred",
                validationException.Errors),
        
            DbUpdateConcurrencyException => new ExceptionDetails(
                StatusCodes.Status409Conflict,
                "ConcurrencyError",
                "Concurrency conflict",
                "The resource was modified by another user",
                null),
        
            DbUpdateException => new ExceptionDetails(
                StatusCodes.Status500InternalServerError,
                "DatabaseError",
                "Database error",
                "A database error occurred",
                null),
        
            _ => new ExceptionDetails(
                StatusCodes.Status500InternalServerError,
                "ServerError",
                "Server error",
                "An unexpected error has occurred",
                null)
        };
    }

    private record ExceptionDetails(
        int Status,
        string Type,
        string Title,
        string Detail,
        IEnumerable<object>? Errors);
}