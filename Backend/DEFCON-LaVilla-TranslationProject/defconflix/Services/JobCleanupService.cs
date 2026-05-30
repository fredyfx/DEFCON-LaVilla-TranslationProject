using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.Extensions.Options;

namespace defconflix.Services
{
    /// <summary>
    /// Background service that periodically cleans up completed jobs from memory.
    /// Replaces fire-and-forget Task.Run cleanup with proper lifecycle management.
    /// </summary>
    public class JobCleanupService : BackgroundService
    {
        private readonly ILogger<JobCleanupService> _logger;
        private readonly IOnDemandFileCheckService _fileCheckService;
        private readonly BackgroundJobOptions _options;

        public JobCleanupService(
            ILogger<JobCleanupService> logger,
            IOnDemandFileCheckService fileCheckService,
            IOptions<BackgroundJobOptions> options)
        {
            _logger = logger;
            _fileCheckService = fileCheckService;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Job Cleanup Service started. Cleanup interval: {Interval}, Retention period: {Retention}",
                _options.CleanupInterval, _options.JobRetentionPeriod);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.CleanupInterval, stoppingToken);
                    await CleanupOldJobsAsync();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during job cleanup");
                }
            }
        }

        private async Task CleanupOldJobsAsync()
        {
            try
            {
                var cleanedCount = await _fileCheckService.CleanupCompletedJobsAsync(_options.JobRetentionPeriod);

                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} completed jobs older than {Retention}",
                        cleanedCount, _options.JobRetentionPeriod);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old jobs");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Job Cleanup Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}
