using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.Extensions.Options;

namespace defconflix.Services
{
    /// <summary>
    /// Background service that processes work items from the queue.
    /// Supports configurable number of concurrent workers.
    /// </summary>
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly BackgroundJobOptions _options;

        public QueuedHostedService(
            IBackgroundTaskQueue taskQueue,
            ILogger<QueuedHostedService> logger,
            IOptions<BackgroundJobOptions> options)
        {
            _taskQueue = taskQueue;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workerCount = _options.MaxConcurrentWorkers;
            _logger.LogInformation("Queued Hosted Service started with {WorkerCount} worker(s)", workerCount);

            // Start multiple workers to process queue concurrently
            var workers = Enumerable.Range(0, workerCount)
                .Select(workerId => ProcessQueueAsync(workerId, stoppingToken))
                .ToList();

            await Task.WhenAll(workers);
        }

        private async Task ProcessQueueAsync(int workerId, CancellationToken stoppingToken)
        {
            _logger.LogDebug("Worker {WorkerId} started", workerId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                    _logger.LogDebug("Worker {WorkerId} dequeued work item. Queue depth: {QueueDepth}",
                        workerId, _taskQueue.Count);

                    await workItem(stoppingToken);

                    _logger.LogDebug("Worker {WorkerId} completed work item", workerId);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is signaled
                    _logger.LogDebug("Worker {WorkerId} cancelled", workerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {WorkerId}: Error occurred executing background work item", workerId);
                }
            }

            _logger.LogDebug("Worker {WorkerId} stopped", workerId);
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping. Remaining queue items: {Count}", _taskQueue.Count);
            await base.StopAsync(stoppingToken);
        }
    }
}
