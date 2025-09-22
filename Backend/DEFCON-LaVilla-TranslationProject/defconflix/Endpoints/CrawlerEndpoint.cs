using defconflix.Data;
using defconflix.Extensions;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class CrawlerEndpoint : IEndpoint
    {
        public record StartCrawlRequest(string BaseUrl);
        public record CancelJobRequest(string? Reason);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/crawler/start", async (HttpContext context, IWebCrawlerService crawlerService, StartCrawlRequest request) =>
            {
                if (string.IsNullOrEmpty(request.BaseUrl) || !Uri.IsWellFormedUriString(request.BaseUrl, UriKind.Absolute))
                {
                    return Results.BadRequest("Invalid URL provided");
                }

                var userId = context.GetCurrentUserId();
                if (userId == null)
                {
                    return Results.Unauthorized();
                }

                var jobId = await crawlerService.StartCrawlAsync(request.BaseUrl, (int)userId);
                return Results.Json(new { JobId = jobId, Message = "Crawling started" });
            }).RequireAuthorization("AdminApiAccess")
              .RequireRateLimiting("AuthenticatedPolicy");

            app.MapPost("/api/crawler/job/{id}/cancel", async (HttpContext context, ICrawlerCancellationService cancellationService, ApiContext db, int id, CancelJobRequest request) =>
            {
                var userId = context.GetCurrentUserId();
                if (userId == null)
                {
                    return Results.Unauthorized();
                }

                // Check if job exists and user has permission to cancel it
                var job = await db.CrawlerJobs
                    .Include(j => j.StartedByUser)
                    .FirstOrDefaultAsync(j => j.Id == id);

                if (job == null)
                {
                    return Results.NotFound("Crawler job not found");
                }

                // Allow cancellation if user started the job or is admin (you can add admin check here)
                if (job.StartedByUserId != userId)
                {
                    return Results.Forbid();
                }

                if (!job.CanBeCancelled)
                {
                    return Results.BadRequest($"Job cannot be cancelled. Current status: {job.Status}");
                }

                var success = await cancellationService.RequestCancellationAsync(id, (int)userId, request.Reason);

                if (success)
                {
                    return Results.Ok(new
                    {
                        Message = "Cancellation requested. Job will stop shortly.",
                        JobId = id,
                        Status = "Cancelling"
                    });
                }
                else
                {
                    return Results.BadRequest("Failed to request cancellation");
                }
            }).RequireAuthorization("AdminApiAccess")
            .RequireRateLimiting("AuthenticatedPolicy");

            app.MapGet("/api/crawler/job/{id}", async (IWebCrawlerService crawlerService, int id) =>
            {
                var job = await crawlerService.GetCrawlerJobAsync(id);
                if (job == null)
                    return Results.NotFound();

                return Results.Json(new
                {
                    job.Id,
                    job.StartUrl,
                    job.Status,
                    job.StartTime,
                    job.EndTime,
                    job.FilesFound,
                    job.FilesProcessed,
                    job.ErrorMessage,
                    Duration = job.Duration?.ToString(@"hh\:mm\:ss"),
                    job.CreatedAt
                });
            }).RequireAuthorization("AdminApiAccess")
            .RequireRateLimiting("AuthenticatedPolicy");

            app.MapGet("/api/crawler/jobs", async (IWebCrawlerService crawlerService) =>
            {
                var jobs = await crawlerService.GetAllJobsAsync();
                return Results.Json(jobs.Select(job => new
                {
                    job.Id,
                    job.StartUrl,
                    job.Status,
                    job.StartTime,
                    job.EndTime,
                    job.FilesFound,
                    job.FilesProcessed,
                    Duration = job.Duration?.ToString(@"hh\:mm\:ss"),
                    job.CreatedAt
                }));
            }).RequireAuthorization("AdminApiAccess")
            .RequireRateLimiting("AuthenticatedPolicy");

            // Endpoint to get file statistics by extension
            app.MapGet("/api/crawler/stats", async (ApiContext db) =>
            {
                var stats = await db.Files
                    .GroupBy(f => f.Extension.ToLower())
                    .Select(g => new { Extension = g.Key, Count = g.Count() })
                    .OrderByDescending(s => s.Count)
                    .ToListAsync();

                var totalFiles = await db.Files.CountAsync();

                return Results.Json(new
                {
                    TotalFiles = totalFiles,
                    ExtensionStats = stats
                });
            }).RequireAuthorization()
            .RequireRateLimiting("AuthenticatedPolicy");
        }
    }
}
