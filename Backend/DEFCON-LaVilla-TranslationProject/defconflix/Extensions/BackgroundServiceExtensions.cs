using defconflix.Interfaces;
using defconflix.Models;
using defconflix.Services;

namespace defconflix.Extensions
{
    public static class BackgroundServiceExtensions
    {
        public static IServiceCollection AddFileCheckBackgroundService(this IServiceCollection services, IConfiguration? configuration = null)
        {
            // Register options from configuration or use defaults
            if (configuration != null)
            {
                services.Configure<BackgroundJobOptions>(
                    configuration.GetSection(BackgroundJobOptions.SectionName));
            }
            else
            {
                services.Configure<BackgroundJobOptions>(_ => { });
            }

            // Core queue infrastructure
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            // Job services
            services.AddSingleton<IOnDemandFileCheckService, OnDemandFileCheckService>();

            // Background hosted services
            services.AddHostedService<QueuedHostedService>();
            services.AddHostedService<JobCleanupService>();

            return services;
        }
    }
}
