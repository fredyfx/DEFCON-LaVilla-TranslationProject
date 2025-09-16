using defconflix.Interfaces;

namespace defconflix.Endpoints
{
    public class FileCheckJobEndpoints : IEndpoint
    {
        public record StartFileCheckJobRequest(long[] FileIds);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            async Task<IResult> CheckFiles (
                HttpContext context,
                IOnDemandFileCheckService jobService,
                StartFileCheckJobRequest request) 
            {
                var userId = GetUserIdFromContext(context);

                if (request.FileIds == null || request.FileIds.Length == 0)
                {
                    return Results.BadRequest("No file IDs provided");
                }

                if (request.FileIds.Length > 1000)
                {
                    return Results.BadRequest("Maximum 1000 files can be checked in a single job");
                }

                try
                {
                    var jobId = await jobService.StartFileCheckJobAsync(request.FileIds, userId);
                    return Results.Json(new
                    {
                        JobId = jobId,
                        Message = "File check job started successfully",
                        TotalFiles = request.FileIds.Length,
                        StatusUrl = $"/api/files/jobs/{jobId}/status"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error starting file check job: {ex.Message}");
                }
            }

            async Task<IResult> CheckAll(
                HttpContext context,
                IOnDemandFileCheckService jobService)
            {
                var userId = GetUserIdFromContext(context);

                try
                {
                    var jobId = await jobService.StartAllFilesCheckJobAsync(userId);
                    return Results.Json(new
                    {
                        JobId = jobId,
                        Message = "Job started to check all files",
                        StatusUrl = $"/api/background/jobs/{jobId}/status",
                        Warning = "This may take a very long time depending on the number of files"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error starting check-all job: {ex.Message}");
                }
            }

            async Task<IResult> CheckNeeded(
                HttpContext context,
                IOnDemandFileCheckService jobService)
            {
                var userId = GetUserIdFromContext(context);

                try
                {
                    var jobId = await jobService.StartFilesNeedingCheckJobAsync(userId);
                    return Results.Json(new
                    {
                        JobId = jobId,
                        Message = "Job started to check files that need checking",
                        StatusUrl = $"/api/background/jobs/{jobId}/status"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error starting check-needed job: {ex.Message}");
                }
            }

            async Task<IResult> GetJobStatus(
                string jobId,
                IOnDemandFileCheckService jobService)
            {
                var job = await jobService.GetJobStatusAsync(jobId);
                if (job == null)
                {
                    return Results.NotFound($"Job {jobId} not found");
                }

                return Results.Json(new
                {
                    job.JobId,
                    job.Status,
                    job.StartedAt,
                    job.CompletedAt,
                    job.TotalFiles,
                    job.ProcessedFiles,
                    job.AvailableFiles,
                    job.UnavailableFiles,
                    job.ErrorMessage,
                    ProgressPercentage = Math.Round(job.ProgressPercentage, 2),
                    Duration = job.Duration?.ToString(@"hh\:mm\:ss"),
                    IsCompleted = job.IsCompleted,
                    StartedByUserId = job.StartedByUserId
                });
            }

            async Task<IResult> GetAllActiveJobs(IOnDemandFileCheckService jobService)
            {
                var activeJobs = await jobService.GetActiveJobsAsync();
                var response = activeJobs.Select(job => new
                {
                    job.JobId,
                    job.Status,
                    job.StartedAt,
                    job.TotalFiles,
                    job.ProcessedFiles,
                    ProgressPercentage = Math.Round(job.ProgressPercentage, 2),
                    Duration = job.Duration?.ToString(@"hh\:mm\:ss"),
                    job.StartedByUserId
                }).ToList();

                return Results.Json(new
                {
                    ActiveJobs = response.Count,
                    Jobs = response
                });
            }

            async Task<IResult> CancelJob(
                string jobId,
                IOnDemandFileCheckService jobService)
            {
                var cancelled = await jobService.CancelJobAsync(jobId);
                if (!cancelled)
                {
                    return Results.NotFound($"Job {jobId} not found or already completed");
                }

                return Results.Json(new
                {
                    Message = $"Job {jobId} has been cancelled",
                    JobId = jobId,
                    CancelledAt = DateTime.UtcNow
                });
            }

            IResult GetQueueStatus (IBackgroundTaskQueue taskQueue)
            {
                return Results.Json(new
                {
                    QueuedJobs = taskQueue.Count,
                    IsEmpty = taskQueue.IsEmpty,
                    CheckedAt = DateTime.UtcNow
                });
            }

            // Start a file check job for specific files
            app.MapPost("/api/background/jobs/check-files", CheckFiles)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthPolicy");

            // Start a job to check all files
            app.MapPost("/api/background/jobs/check-all", CheckAll)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthPolicy");

            // Start a job to check files that need checking (not checked in 24h)
            app.MapPost("/api/background/jobs/check-needed", CheckNeeded)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // Get job status
            app.MapGet("/api/background/jobs/{jobId}/status", GetJobStatus)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // Get all active jobs
            app.MapGet("/api/background/jobs/active", GetAllActiveJobs)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // Cancel a job
            app.MapPost("/api/background/jobs/{jobId}/cancel", CancelJob)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // Get queue status
            app.MapGet("/api/background/jobs/queue/status", GetQueueStatus)
                .RequireAuthorization("AdminApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");
        }
        private static int? GetUserIdFromContext(HttpContext context)
        {
            return context.Items["UserId"] as int?;
        }
    }
}
