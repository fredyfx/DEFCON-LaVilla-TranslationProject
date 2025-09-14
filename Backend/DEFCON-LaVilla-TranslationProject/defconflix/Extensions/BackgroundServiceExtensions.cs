using defconflix.Interfaces;
using defconflix.Services;

namespace defconflix.Extensions
{
    public static class BackgroundServiceExtensions
    {
        public static IServiceCollection AddFileCheckBackgroundService(this IServiceCollection services)
        {
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton<IOnDemandFileCheckService, OnDemandFileCheckService>();
            services.AddHostedService<QueuedHostedService>();
            return services;
        }
    }
}
