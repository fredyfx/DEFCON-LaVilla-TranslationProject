using defconflix.Data;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.Conferences
{
    public class IndexModel : BasePageModel
    {
        private readonly ApiContext _context;

        public IndexModel(ApiContext context)
        {
            _context = context;
        }

        public List<ConferenceStats> Conferences { get; set; } = new();
        public int TotalConferences { get; set; }
        public int TotalFiles { get; set; }

        public async Task OnGetAsync()
        {
            var stats = await _context.Files
                .AsNoTracking()
                .Where(f => !string.IsNullOrEmpty(f.Conference) && f.LastCheckAccessible == true)
                .GroupBy(f => f.Conference)
                .Select(g => new ConferenceStats
                {
                    Name = g.Key!,
                    TotalFiles = g.Count(),
                    Mp4Count = g.Count(f => EF.Functions.ILike(f.Extension, ".mp4")),
                    PdfCount = g.Count(f => EF.Functions.ILike(f.Extension, ".pdf")),
                    TxtCount = g.Count(f => EF.Functions.ILike(f.Extension, ".txt")),
                    SrtCount = g.Count(f => EF.Functions.ILike(f.Extension, ".srt"))
                })
                .OrderByDescending(c => c.TotalFiles)
                .ToListAsync();

            Conferences = stats;
            TotalConferences = stats.Count;
            TotalFiles = stats.Sum(c => c.TotalFiles);
        }

        public class ConferenceStats
        {
            public string Name { get; set; } = string.Empty;
            public int TotalFiles { get; set; }
            public int Mp4Count { get; set; }
            public int PdfCount { get; set; }
            public int TxtCount { get; set; }
            public int SrtCount { get; set; }
        }
    }
}
