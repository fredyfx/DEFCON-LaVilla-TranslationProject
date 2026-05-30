using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace defconflix.Services
{
    public class OnDemandFileCheckService : IOnDemandFileCheckService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OnDemandFileCheckService> _logger;
        private readonly BackgroundJobOptions _options;
        private readonly ConcurrentDictionary<string, FileCheckJobStatus> _jobs = new();
        private readonly object _lockObject = new();

        public OnDemandFileCheckService(
            IBackgroundTaskQueue taskQueue,
            IServiceProvider serviceProvider,
            ILogger<OnDemandFileCheckService> logger,
            IOptions<BackgroundJobOptions> options)
        {
            _taskQueue = taskQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
        }

        public Task<bool> CancelJobAsync(string jobId)
        {
            lock (_lockObject)
            {
                if (_jobs.TryGetValue(jobId, out var job) && !job.IsCompleted)
                {
                    job.CancellationTokenSource?.Cancel();
                    job.Status = JobStatus.Cancelled;
                    job.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Cancelled file check job {JobId}", jobId);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
        }

        public Task<List<FileCheckJobStatus>> GetActiveJobsAsync()
        {
            lock (_lockObject)
            {
                var activeJobs = _jobs.Values
                    .Where(j => !j.IsCompleted)
                    .OrderByDescending(j => j.StartedAt)
                    .ToList();

                return Task.FromResult(activeJobs);
            }
        }

        public Task<List<FileCheckJobStatus>> GetAllJobsAsync()
        {
            lock (_lockObject)
            {
                var allJobs = _jobs.Values
                    .OrderByDescending(j => j.StartedAt)
                    .ToList();

                return Task.FromResult(allJobs);
            }
        }

        public Task<FileCheckJobStatus?> GetJobStatusAsync(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }

        public Task<int> CleanupCompletedJobsAsync(TimeSpan retentionPeriod)
        {
            var cutoffTime = DateTime.UtcNow - retentionPeriod;
            var cleanedCount = 0;

            lock (_lockObject)
            {
                var jobsToRemove = _jobs.Values
                    .Where(j => j.IsCompleted && j.CompletedAt.HasValue && j.CompletedAt.Value < cutoffTime)
                    .Select(j => j.JobId)
                    .ToList();

                foreach (var jobId in jobsToRemove)
                {
                    if (_jobs.TryRemove(jobId, out var removedJob))
                    {
                        removedJob.CancellationTokenSource?.Dispose();
                        cleanedCount++;
                        _logger.LogDebug("Cleaned up completed job {JobId} from memory", jobId);
                    }
                }
            }

            return Task.FromResult(cleanedCount);
        }

        public async Task<string> StartAllFilesCheckJobAsync(int? userId = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApiContext>();

            var allFileIds = await dbContext.Files
                .Select(f => f.Id)
                .ToArrayAsync();

            return await StartFileCheckJobAsync(allFileIds, userId);
        }

        public async Task<string> StartFileCheckJobAsync(long[] fileIds, int? userId = null)
        {
            var jobId = Guid.NewGuid().ToString();
            var job = new FileCheckJobStatus
            {
                JobId = jobId,
                StartedAt = DateTime.UtcNow,
                StartedByUserId = userId,
                TotalFiles = fileIds.Length,
                Status = JobStatus.Queued,
                CancellationTokenSource = new CancellationTokenSource()
            };

            _jobs[jobId] = job;

            await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await ExecuteFileCheckJob(job, fileIds, token);
            });

            _logger.LogInformation("Queued file check job {JobId} for {FileCount} files by user {UserId}",
                jobId, fileIds.Length, userId);

            return jobId;
        }

        public async Task<string> StartFilesNeedingCheckJobAsync(int? userId = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var fileChecker = scope.ServiceProvider.GetRequiredService<IFileCheckerService>();

            var filesNeedingCheck = await fileChecker.GetFilesNeedingCheckAsync();
            var fileIds = filesNeedingCheck.Select(f => f.Id).ToArray();

            return await StartFileCheckJobAsync(fileIds, userId);
        }

        private async Task ExecuteFileCheckJob(FileCheckJobStatus job, long[] fileIds, CancellationToken globalToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var fileChecker = scope.ServiceProvider.GetRequiredService<IFileCheckerService>();

            // Combine global cancellation token with job-specific token
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                globalToken, job.CancellationTokenSource?.Token ?? CancellationToken.None);
            var cancellationToken = combinedTokenSource.Token;

            try
            {
                job.Status = JobStatus.Running;
                _logger.LogInformation("Started executing file check job {JobId} for {FileCount} files",
                    job.JobId, fileIds.Length);

                // Process files in configurable batches
                var batchSize = _options.BatchSize;

                for (int i = 0; i < fileIds.Length; i += batchSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Job {JobId} was cancelled during execution", job.JobId);
                        job.Status = JobStatus.Cancelled;
                        job.CompletedAt = DateTime.UtcNow;
                        return;
                    }

                    var batch = fileIds.Skip(i).Take(batchSize).ToArray();
                    _logger.LogDebug("Job {JobId}: Processing batch {BatchNumber} with {BatchSize} files",
                        job.JobId, (i / batchSize) + 1, batch.Length);

                    var results = await fileChecker.CheckMultipleFilesAsync(batch, job.StartedByUserId);

                    job.ProcessedFiles += results.Count;
                    job.AvailableFiles += results.Count(r => r.IsAccessible);
                    job.UnavailableFiles += results.Count(r => !r.IsAccessible);

                    _logger.LogDebug("Job {JobId}: Processed batch {BatchNumber}, Progress: {Progress}%",
                        job.JobId, (i / batchSize) + 1, job.ProgressPercentage);

                    // Configurable delay between batches to be respectful to the server
                    if (i + batchSize < fileIds.Length)
                    {
                        await Task.Delay(_options.DelayBetweenBatches, cancellationToken);
                    }
                }

                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Completed file check job {JobId}. Processed: {Processed}, Available: {Available}, Unavailable: {Unavailable}",
                    job.JobId, job.ProcessedFiles, job.AvailableFiles, job.UnavailableFiles);
            }
            catch (OperationCanceledException)
            {
                job.Status = JobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("File check job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "File check job {JobId} failed with error: {Error}", job.JobId, ex.Message);
            }
            // NOTE: Cleanup is now handled by JobCleanupService instead of fire-and-forget Task.Run
        }
    }
}
