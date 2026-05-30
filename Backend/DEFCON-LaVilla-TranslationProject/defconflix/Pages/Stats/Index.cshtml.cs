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
            var filesQuery = _context.Files
                .AsNoTracking()
                .Where(f => f.LastCheckAccessible == true);

            TotalFiles = await filesQuery.CountAsync();
            TotalVideos = await filesQuery.CountAsync(f => EF.Functions.ILike(f.Extension, ".mp4"));
            TotalPdfs = await filesQuery.CountAsync(f => EF.Functions.ILike(f.Extension, ".pdf"));
            TotalSubtitles = await filesQuery.CountAsync(f =>
                EF.Functions.ILike(f.Extension, ".srt") || EF.Functions.ILike(f.Extension, ".vtt"));

            TotalConferences = await _context.Conferences.AsNoTracking().CountAsync();

            // File translation status
            TotalTranslations = await _context.VttFiles.AsNoTracking().CountAsync();
            CompletedFiles = await _context.Files
                .AsNoTracking()
                .CountAsync(f => EF.Functions.ILike(f.Status, "Completed"));
            InProgressFiles = await _context.Files
                .AsNoTracking()
                .CountAsync(f => EF.Functions.ILike(f.Status, "In Progress"));

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

            TotalContributors = await _context.Files
                .AsNoTracking()
                .Where(f => f.ProcessedBy.HasValue)
                .Select(f => f.ProcessedBy)
                .Distinct()
                .CountAsync();

            // Conference progress
            ConferenceProgressList = await _context.Conferences
                .AsNoTracking()
                .Select(c => new ConferenceProgressItem
                {
                    Name = c.Name,
                    TotalFiles = _context.Files.Count(f => f.Conference == c.Name && f.LastCheckAccessible == true),
                    CompletedFiles = _context.Files
                        .Count(f => f.Conference == c.Name && EF.Functions.ILike(f.Status, "Completed"))
                })
                .Where(c => c.TotalFiles > 0)
                .OrderByDescending(c => c.TotalFiles)
                .Take(15)
                .ToListAsync();
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
