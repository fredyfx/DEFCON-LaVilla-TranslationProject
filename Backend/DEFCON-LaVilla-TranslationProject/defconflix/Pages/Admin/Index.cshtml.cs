using defconflix.Data;
using defconflix.Enums;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.Admin
{
    [Authorize(Policy = "AdminApiAccess")]
    public class IndexModel : BasePageModel
    {
        private readonly ApiContext _context;
        private readonly IOnDemandFileCheckService _fileCheckService;
        private readonly IBackgroundTaskQueue _taskQueue;

        public IndexModel(
            ApiContext context,
            IOnDemandFileCheckService fileCheckService,
            IBackgroundTaskQueue taskQueue)
        {
            _context = context;
            _fileCheckService = fileCheckService;
            _taskQueue = taskQueue;
        }

        // Stats
        public int TotalFiles { get; set; }
        public int TotalUsers { get; set; }
        public int TotalTranslations { get; set; }
        public int AccessibleFiles { get; set; }
        public int InaccessibleFiles { get; set; }

        // File breakdown
        public int Mp4Count { get; set; }
        public int PdfCount { get; set; }
        public int TxtCount { get; set; }
        public int SrtCount { get; set; }

        // Jobs
        public List<FileCheckJobStatus> ActiveJobs { get; set; } = new();
        public int QueueDepth { get; set; }
        public bool QueueEmpty { get; set; }

        // Recent users
        public List<RecentUserInfo> RecentUsers { get; set; } = new();

        // Crawler jobs
        public List<CrawlerJob> RecentCrawlerJobs { get; set; } = new();

        public async Task OnGetAsync()
        {
            // File stats
            TotalFiles = await _context.Files.CountAsync();
            AccessibleFiles = await _context.Files.CountAsync(f => f.LastCheckAccessible == true);
            InaccessibleFiles = TotalFiles - AccessibleFiles;

            // File type breakdown
            Mp4Count = await _context.Files.CountAsync(f => EF.Functions.ILike(f.Extension, ".mp4"));
            PdfCount = await _context.Files.CountAsync(f => EF.Functions.ILike(f.Extension, ".pdf"));
            TxtCount = await _context.Files.CountAsync(f => EF.Functions.ILike(f.Extension, ".txt"));
            SrtCount = await _context.Files.CountAsync(f => EF.Functions.ILike(f.Extension, ".srt"));

            // User stats
            TotalUsers = await _context.Users.CountAsync(u => u.IsActive);

            // Translation stats (completed files)
            TotalTranslations = await _context.Files.CountAsync(f => EF.Functions.ILike(f.Status, "Completed"));

            // Active jobs
            ActiveJobs = await _fileCheckService.GetActiveJobsAsync();

            // Queue status
            QueueDepth = _taskQueue.Count;
            QueueEmpty = _taskQueue.IsEmpty;

            // Recent users
            RecentUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.LastAccessedAt.HasValue)
                .OrderByDescending(u => u.LastAccessedAt)
                .Take(10)
                .Select(u => new RecentUserInfo
                {
                    Username = u.Username,
                    LastAccessed = u.LastAccessedAt!.Value,
                    Role = u.Role
                })
                .ToListAsync();

            // Recent crawler jobs
            RecentCrawlerJobs = await _context.CrawlerJobs
                .AsNoTracking()
                .OrderByDescending(j => j.CreatedAt)
                .Take(5)
                .ToListAsync();
        }

        public class RecentUserInfo
        {
            public string Username { get; set; } = string.Empty;
            public DateTime LastAccessed { get; set; }
            public UserRole Role { get; set; }

            public string TimeAgo
            {
                get
                {
                    var diff = DateTime.UtcNow - LastAccessed;
                    if (diff.TotalMinutes < 1) return "just now";
                    if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
                    if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hr ago";
                    return $"{(int)diff.TotalDays} days ago";
                }
            }
        }
    }
}
