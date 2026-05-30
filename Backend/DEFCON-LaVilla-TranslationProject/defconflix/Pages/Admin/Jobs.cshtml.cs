using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.Admin
{
    [Authorize(Policy = "AdminApiAccess")]
    public class JobsModel : BasePageModel
    {
        private readonly ApiContext _context;
        private readonly IOnDemandFileCheckService _fileCheckService;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IWebCrawlerService _crawlerService;
        private readonly ICrawlerCancellationService _cancellationService;

        public JobsModel(
            ApiContext context,
            IOnDemandFileCheckService fileCheckService,
            IBackgroundTaskQueue taskQueue,
            IWebCrawlerService crawlerService,
            ICrawlerCancellationService cancellationService)
        {
            _context = context;
            _fileCheckService = fileCheckService;
            _taskQueue = taskQueue;
            _crawlerService = crawlerService;
            _cancellationService = cancellationService;
        }

        public List<FileCheckJobStatus> FileCheckJobs { get; set; } = new();
        public List<CrawlerJob> CrawlerJobs { get; set; } = new();
        public int QueueDepth { get; set; }

        [TempData]
        public string? Message { get; set; }

        [TempData]
        public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            FileCheckJobs = await _fileCheckService.GetAllJobsAsync();
            CrawlerJobs = await _crawlerService.GetAllJobsAsync();
            QueueDepth = _taskQueue.Count;
        }

        public async Task<IActionResult> OnPostCancelFileCheckAsync(string jobId)
        {
            var cancelled = await _fileCheckService.CancelJobAsync(jobId);
            if (cancelled)
            {
                Message = $"Job {jobId[..8]}... cancelled successfully";
            }
            else
            {
                Error = $"Failed to cancel job {jobId[..8]}... (may already be completed)";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCancelCrawlerAsync(int jobId)
        {
            var userId = GetCurrentUserId();
            await _cancellationService.RequestCancellationAsync(jobId, userId, "Cancelled via admin panel");
            Message = $"Cancellation requested for crawler job #{jobId}";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostStartFileCheckAsync()
        {
            var userId = GetCurrentUserId();
            var jobId = await _fileCheckService.StartFilesNeedingCheckJobAsync(userId);
            Message = $"Started file check job: {jobId[..8]}...";
            return RedirectToPage();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }
    }
}
