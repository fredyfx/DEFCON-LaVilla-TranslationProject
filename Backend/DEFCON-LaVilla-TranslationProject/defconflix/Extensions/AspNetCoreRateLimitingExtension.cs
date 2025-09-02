using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace defconflix.Extensions
{
    public static class AspNetCoreRateLimitingExtension
    {
        public static void AddRateLimiterFX(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRateLimiter(options =>
            {
                // Global rate limit
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // API Key rate limit (more restrictive)
                options.AddFixedWindowLimiter("ApiKeyPolicy", options =>
                {
                    options.PermitLimit = 100;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 10;
                });

                // OAuth endpoints rate limit
                options.AddFixedWindowLimiter("AuthPolicy", options =>
                {
                    options.PermitLimit = 50;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 20;
                });

                // More permissive for authenticated users
                options.AddTokenBucketLimiter("AuthenticatedPolicy", options =>
                {
                    options.TokenLimit = 500;
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 20;
                    options.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
                    options.TokensPerPeriod = 200;
                    options.AutoReplenishment = true;
                });

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken: token);
                };
            });

        }
    }
}
