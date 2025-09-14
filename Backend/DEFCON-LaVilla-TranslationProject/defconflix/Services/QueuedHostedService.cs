using defconflix.Interfaces;

namespace defconflix.Services
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _serviceProvider;

        public QueuedHostedService(
            IBackgroundTaskQueue taskQueue,
            ILogger<QueuedHostedService> logger,
            IServiceProvider serviceProvider)
        {
            _taskQueue = taskQueue;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);
                    await workItem(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is signaled
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing background work item");
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}
