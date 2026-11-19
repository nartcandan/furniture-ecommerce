using FluentValidation;
using FurnitureEcommerce.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace FurnitureEcommerce.Api.ExceptionHandling;

/// <summary>
/// Turns exceptions into ProblemDetails answers. The application's own exceptions carry a message meant for
/// the client. Anything else gets a general message, so internal details never reach the client, and the
/// <c>traceId</c> in the answer finds the full error in the logs.
/// </summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Not a real HTTP status. Logs use it, like nginx, for a request the client gave up on.</summary>
    public const int ClientClosedRequest = 499;

    /// <summary>How long the client should wait before trying again when the database cannot be reached.</summary>
    public const int RetryAfterSeconds = 5;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The browser left the page or closed the connection. Nobody reads an answer, and it is not an error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("The client closed the request before it finished.");
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problemDetails = exception switch
        {
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            UnauthorizedException => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            PaymentGatewayException => Problem(
                StatusCodes.Status502BadGateway, "Payment provider error", exception.Message),
            ValidationException validationException => new ValidationProblemDetails(
                validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            },
            _ when IsDatabaseUnavailable(exception) => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                "The store cannot reach its database right now. Please try again in a moment."),
            _ => null
        };

        // Unknown exceptions fall through to the default 500 answer, which is logged with the exception.
        if (problemDetails is null)
        {
            return false;
        }

        if (problemDetails.Status == StatusCodes.Status503ServiceUnavailable)
        {
            // Passes on its own, but it is logged as an error: a database that keeps failing needs a person.
            logger.LogError(exception, "The database could not be reached.");
            httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };

    /// <summary>
    /// A connection the database refused, dropped or did not answer in time. EF Core may wrap it, so the whole
    /// chain of inner exceptions is checked. Errors in a query itself are not transient and stay a 500.
    /// </summary>
    public static bool IsDatabaseUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true } or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
