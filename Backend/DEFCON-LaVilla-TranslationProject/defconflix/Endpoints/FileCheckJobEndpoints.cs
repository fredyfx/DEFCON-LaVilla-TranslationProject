using defconflix.Interfaces;

namespace defconflix.Endpoints
{
    public class FileCheckJobEndpoints : IEndpoint
    {
        public record StartFileCheckJobRequest(long[] FileIds);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            // Start a file check job for specific files
            app.MapPost("/api/protected/files/jobs/check-files", async (
                HttpContext context,
                IOnDemandFileCheckService jobService,
                StartFileCheckJobRequest request) =>
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
                        StatusUrl = $"/api/protected/files/jobs/{jobId}/status"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error starting file check job: {ex.Message}");
                }
            }).RequireAuthorization("AdminApiAccess");
            //  .RequireRateLimiting("AuthenticatedPolicy");

            // Start a job to check all files
            app.MapPost("/api/protected/files/jobs/check-all", async (
                HttpContext context,
                IOnDemandFileCheckService jobService) =>
            {
                var userId = GetUserIdFromContext(context);

                try
                {
                    var jobId = await jobService.StartAllFilesCheckJobAsync(userId);
                    return Results.Json(new
                    {
                        JobId = jobId,
                        Message = "Job started to check all files",
                        StatusUrl = $"/api/files/jobs/{jobId}/status",
                        Warning = "This may take a very long time depending on the number of files"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error starting check-all job: {ex.Message}");
                }
            }).RequireAuthorization("AdminApiAccess");
            //  .RequireRateLimiting("AuthPolicy"); // More restrictive for bulk operations

            // Start a job to check files that need checking (not checked in 24h)
            app.MapPost("/api/protected/files/jobs/check-needed", async (
                HttpContext context,
                IOnDemandFileCheckService jobService) =>
            {
                var userId = GetUserIdFromContext(context);

                try
                {
                    var jobId = await jobService.StartFilesNeedingCheckJobAsync(userId);
                    return Results.Json(new
                    {
                        JobId = jobId,
                        Message = "Job started to check files that need checking",
                        StatusUrl = $"/api/protected/files/jobs/{jobId}/status"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error starting check-needed job: {ex.Message}");
                }
            }).RequireAuthorization("AdminApiAccess");
            //  .RequireRateLimiting("AuthenticatedPolicy");

            // Get job status
            app.MapGet("/api/protected/files/jobs/{jobId}/status", async (
                string jobId,
                IOnDemandFileCheckService jobService) =>
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
            }).RequireAuthorization("AdminApiAccess");

            // Get all active jobs
            app.MapGet("/api/protected/files/jobs/active", async (IOnDemandFileCheckService jobService) =>
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
            }).RequireAuthorization("AdminApiAccess");
            //  .RequireRateLimiting("AuthenticatedPolicy");

            // Cancel a job
            app.MapPost("/api/protected/files/jobs/{jobId}/cancel", async (
                string jobId,
                IOnDemandFileCheckService jobService) =>
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
            }).RequireAuthorization("AdminApiAccess");
            //  .RequireRateLimiting("AuthenticatedPolicy");

            // Get queue status
            app.MapGet("/api/protected/files/jobs/queue/status", (IBackgroundTaskQueue taskQueue) =>
            {
                return Results.Json(new
                {
                    QueuedJobs = taskQueue.Count,
                    IsEmpty = taskQueue.IsEmpty,
                    CheckedAt = DateTime.UtcNow
                });
            }).RequireAuthorization("AdminApiAccess");
            //  .RequireRateLimiting("AuthenticatedPolicy");
        }
        private static int? GetUserIdFromContext(HttpContext context)
        {
            return context.Items["UserId"] as int?;
        }
    }
}
