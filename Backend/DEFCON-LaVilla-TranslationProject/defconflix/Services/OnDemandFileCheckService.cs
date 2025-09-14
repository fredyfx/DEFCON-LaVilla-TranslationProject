using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace defconflix.Services
{
    public class OnDemandFileCheckService : IOnDemandFileCheckService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OnDemandFileCheckService> _logger;
        private readonly ConcurrentDictionary<string, FileCheckJobStatus> _jobs = new();
        private readonly object _lockObject = new object();
        public OnDemandFileCheckService(
            IBackgroundTaskQueue taskQueue,
            IServiceProvider serviceProvider,
            ILogger<OnDemandFileCheckService> logger)
        {
            _taskQueue = taskQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task<bool> CancelJobAsync(string jobId)
        {
            lock (_lockObject)
            {
                if (_jobs.TryGetValue(jobId, out var job) && !job.IsCompleted)
                {
                    job.CancellationTokenSource?.Cancel();
                    job.Status = "Cancelled";
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

        public Task<FileCheckJobStatus?> GetJobStatusAsync(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }

        public async Task<string> StartAllFilesCheckJobAsync(int? userId = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<defconflix.Data.ApiContext>();

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
                Status = "Queued",
                CancellationTokenSource = new CancellationTokenSource()
            };

            _jobs[jobId] = job;
            var listOfJobs = _jobs.Values;

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
                job.Status = "Running";
                _logger.LogInformation("Started executing file check job {JobId} for {FileCount} files",
                    job.JobId, fileIds.Length);

                // Process files in batches
                const int batchSize = 5;
                
                for (int i = 0; i < fileIds.Length; i += batchSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Job {JobId} was cancelled during execution", job.JobId);
                        job.Status = "Cancelled";
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

                    // Small delay between batches to be respectful to the server
                    if (i + batchSize < fileIds.Length)
                    {
                        await Task.Delay(3000, cancellationToken);
                    }
                }

                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Completed file check job {JobId}. Processed: {Processed}, Available: {Available}, Unavailable: {Unavailable}",
                    job.JobId, job.ProcessedFiles, job.AvailableFiles, job.UnavailableFiles);
            }
            catch (OperationCanceledException)
            {
                job.Status = "Cancelled";
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("File check job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "File check job {JobId} failed with error: {Error}", job.JobId, ex.Message);
            }
            finally
            {
                job.CancellationTokenSource?.Dispose();

                // Clean up completed jobs after 1 hour to prevent memory leaks
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    _jobs.TryRemove(job.JobId, out _);
                    _logger.LogDebug("Cleaned up completed job {JobId} from memory", job.JobId);
                });
            }
        }
    }
}
