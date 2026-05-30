using defconflix.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace defconflix.Exceptions
{
    internal sealed class CustomExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<CustomExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public CustomExceptionHandler(ILogger<CustomExceptionHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var correlationId = httpContext.GetCorrelationId() ?? "unknown";

            // Determine status code based on exception type
            var (status, title, showDetails) = exception switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", true),
                InvalidOperationException => (StatusCodes.Status400BadRequest, "Invalid Operation", true),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", false),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", true),
                NotImplementedException => (StatusCodes.Status501NotImplemented, "Not Implemented", false),
                OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled", false),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", false)
            };

            // Log the exception with correlation ID
            _logger.LogError(exception,
                "Exception occurred. CorrelationId: {CorrelationId}, Type: {ExceptionType}, Path: {Path}, Status: {StatusCode}",
                correlationId,
                exception.GetType().Name,
                httpContext.Request.Path,
                status);

            httpContext.Response.StatusCode = status;

            // Build problem details
            var problemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["correlationId"] = correlationId,
                    ["traceId"] = httpContext.TraceIdentifier
                }
            };

            // Only show exception details for client errors or in development
            if (showDetails || _environment.IsDevelopment())
            {
                problemDetails.Detail = exception.Message;
                problemDetails.Type = exception.GetType().Name;

                // Include stack trace only in development
                if (_environment.IsDevelopment())
                {
                    problemDetails.Extensions["stackTrace"] = exception.StackTrace;
                }
            }
            else
            {
                problemDetails.Detail = "An unexpected error occurred. Please try again later.";
            }

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
