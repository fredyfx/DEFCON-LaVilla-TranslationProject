namespace defconflix.Middleware
{
    /// <summary>
    /// Middleware that adds correlation ID to requests for tracing through logs.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        public const string CorrelationIdHeader = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Get or generate correlation ID
            var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N")[..12]; // Short ID for readability
            }

            // Add to context items for access throughout request
            context.Items["CorrelationId"] = correlationId;

            // Add to response headers
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
                return Task.CompletedTask;
            });

            // Add to logging scope
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestPath"] = context.Request.Path.ToString(),
                ["RequestMethod"] = context.Request.Method
            }))
            {
                _logger.LogDebug("Request started: {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    await _next(context);
                }
                finally
                {
                    sw.Stop();
                    _logger.LogDebug("Request completed: {Method} {Path} - {StatusCode} in {ElapsedMs}ms",
                        context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
                }
            }
        }
    }

    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }

    public static class HttpContextCorrelationExtensions
    {
        public static string? GetCorrelationId(this HttpContext context)
        {
            return context.Items["CorrelationId"] as string;
        }
    }
}
