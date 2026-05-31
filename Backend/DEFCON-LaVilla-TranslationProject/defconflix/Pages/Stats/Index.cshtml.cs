using defconflix.Data;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.Stats
{
    public class IndexModel : BasePageModel
    {
        private readonly ApiContext _context;

        public IndexModel(ApiContext context)
        {
            _context = context;
        }

        public int TotalFiles { get; set; }
        public int TotalVideos { get; set; }
        public int TotalPdfs { get; set; }
        public int TotalSubtitles { get; set; }
        public int TotalConferences { get; set; }
        public int TotalTranslations { get; set; }
        public int CompletedFiles { get; set; }
        public int InProgressFiles { get; set; }
        public int TotalContributors { get; set; }
        public List<TopContributor> TopContributors { get; set; } = new();
        public List<ConferenceProgressItem> ConferenceProgressList { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Batch file counts into single query
            var fileCounts = await _context.Files
                .AsNoTracking()
                .Where(f => f.LastCheckAccessible == true)
                .GroupBy(f => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Videos = g.Count(f => EF.Functions.ILike(f.Extension, ".mp4")),
                    Pdfs = g.Count(f => EF.Functions.ILike(f.Extension, ".pdf")),
                    Subtitles = g.Count(f => EF.Functions.ILike(f.Extension, ".srt")
                                          || EF.Functions.ILike(f.Extension, ".vtt")),
                    Completed = g.Count(f => EF.Functions.ILike(f.Status, "Completed")),
                    InProgress = g.Count(f => EF.Functions.ILike(f.Status, "In Progress"))
                })
                .FirstOrDefaultAsync();

            TotalFiles = fileCounts?.Total ?? 0;
            TotalVideos = fileCounts?.Videos ?? 0;
            TotalPdfs = fileCounts?.Pdfs ?? 0;
            TotalSubtitles = fileCounts?.Subtitles ?? 0;
            CompletedFiles = fileCounts?.Completed ?? 0;
            InProgressFiles = fileCounts?.InProgress ?? 0;

            TotalConferences = await _context.Conferences.AsNoTracking().CountAsync();
            TotalTranslations = await _context.VttFiles.AsNoTracking().CountAsync();

            // Top contributors (by files processed)
            TopContributors = await _context.Files
                .AsNoTracking()
                .Where(f => f.ProcessedBy.HasValue)
                .GroupBy(f => f.ProcessedBy)
                .Select(g => new TopContributor
                {
                    UserId = g.Key!.Value,
                    FilesProcessed = g.Count(),
                    CompletedCount = g.Count(f => EF.Functions.ILike(f.Status, "Completed"))
                })
                .OrderByDescending(c => c.FilesProcessed)
                .Take(10)
                .ToListAsync();

            // Conference progress - single query with GROUP BY (fixes N+1)
            ConferenceProgressList = await _context.Files
                .AsNoTracking()
                .Where(f => f.LastCheckAccessible == true)
                .GroupBy(f => f.Conference)
                .Select(g => new ConferenceProgressItem
                {
                    Name = g.Key,
                    TotalFiles = g.Count(),
                    CompletedFiles = g.Count(f => EF.Functions.ILike(f.Status, "Completed"))
                })
                .Where(c => c.TotalFiles > 0)
                .OrderByDescending(c => c.TotalFiles)
                .Take(15)
                .ToListAsync();

            // Get user names for contributors
            var userIds = TopContributors.Select(c => c.UserId).ToList();
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => (long)u.Id, u => u.Username ?? "User #" + u.Id);

            foreach (var contributor in TopContributors)
            {
                contributor.Name = users.GetValueOrDefault(contributor.UserId, "Unknown");
            }

            TotalContributors = TopContributors.Count > 0
                ? await _context.Files
                    .AsNoTracking()
                    .Where(f => f.ProcessedBy.HasValue)
                    .Select(f => f.ProcessedBy)
                    .Distinct()
                    .CountAsync()
                : 0;
        }

        public class TopContributor
        {
            public long UserId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int FilesProcessed { get; set; }
            public int CompletedCount { get; set; }
        }

        public class ConferenceProgressItem
        {
            public string Name { get; set; } = string.Empty;
            public int TotalFiles { get; set; }
            public int CompletedFiles { get; set; }
            public double ProgressPercentage => TotalFiles > 0 ? (CompletedFiles * 100.0 / TotalFiles) : 0;
        }
    }
}
