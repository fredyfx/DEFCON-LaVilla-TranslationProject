using defconflix.Data;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Services
{
    public class CrawlerCancellationService : ICrawlerCancellationService
    {
        private readonly ApiContext _context;
        private readonly ILogger<CrawlerCancellationService> _logger;

        public CrawlerCancellationService(ApiContext context, ILogger<CrawlerCancellationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<int>> GetCancellationRequestedJobsAsync()
        {
            return await _context.CrawlerJobs
                .Where(j => j.IsCancellationRequested && j.Status == "Cancelling")
                .Select(j => j.Id)
                .ToListAsync();
        }

        public async Task<bool> IsCancellationRequestedAsync(int jobId)
        {
            var job = await _context.CrawlerJobs.FindAsync(jobId);
            return job?.IsCancellationRequested ?? false;
        }

        public async Task MarkJobAsCancelledAsync(int jobId)
        {
            var job = await _context.CrawlerJobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = "Cancelled";
                job.EndTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Job {jobId} marked as cancelled");
            }
        }

        public async Task<bool> RequestCancellationAsync(int jobId, int userId, string? reason = null)
        {
            var job = await _context.CrawlerJobs.FindAsync(jobId);

            if (job == null)
            {
                _logger.LogWarning($"Attempted to cancel non-existent job: {jobId}");
                return false;
            }

            if (!job.CanBeCancelled)
            {
                _logger.LogWarning($"Job {jobId} cannot be cancelled. Current status: {job.Status}");
                return false;
            }

            job.IsCancellationRequested = true;
            job.CancellationRequestedAt = DateTime.UtcNow;
            job.CancelledByUserId = userId;
            job.CancellationReason = reason;
            job.Status = "Cancelling";

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Cancellation requested for job {jobId} by user {userId}. Reason: {reason ?? "No reason provided"}");
            return true;
        }
    }
}
