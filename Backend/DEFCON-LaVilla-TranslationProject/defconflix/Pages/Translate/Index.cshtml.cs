using defconflix.Data;
using defconflix.Models;
using defconflix.WebAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace defconflix.Pages.Translate
{
    [Authorize]
    public class IndexModel : BasePageModel
    {
        private readonly ApiContext _context;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public IndexModel(ApiContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [BindProperty(SupportsGet = true)]
        public string? Conference { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        public List<TranslatableFileView> Files { get; set; } = new();
        public List<Conference> AvailableConferences { get; set; } = new();
        public int TotalFiles { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadConferences();
            await LoadTranslatableFiles();
            return Page();
        }

        private async Task LoadConferences()
        {
            const string cacheKey = "conference_list";

            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                AvailableConferences = (List<Conference>)cached!;
                return;
            }

            AvailableConferences = await _context.Conferences
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            _cache.Set(cacheKey, AvailableConferences, _cacheExpiration);
        }

        private async Task LoadTranslatableFiles()
        {
            // Files that have English VTT files
            var query = _context.VttFiles
                .AsNoTracking()
                .Where(v => v.Language == "en" || v.Language == "english")
                .Join(_context.Files, v => v.FileId, f => f.Id, (v, f) => new { VttFile = v, File = f });

            if (!string.IsNullOrEmpty(Conference))
            {
                query = query.Where(x => x.File.Conference == Conference);
            }

            TotalFiles = await query.CountAsync();
            TotalPages = (int)Math.Ceiling((double)TotalFiles / PageSize);

            var files = await query
                .OrderBy(x => x.File.File_Name)
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new
                {
                    x.File.Id,
                    x.File.File_Name,
                    x.File.Conference,
                    SourceVttFileId = x.VttFile.Id
                })
                .ToListAsync();

            var fileIds = files.Select(f => f.Id).ToList();
            var sourceVttIds = files.Select(f => f.SourceVttFileId).ToList();

            // Get cue counts for source files
            var sourceCueCounts = await _context.VttCues
                .Where(c => sourceVttIds.Contains(c.VttFileId))
                .GroupBy(c => c.VttFileId)
                .Select(g => new { VttFileId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VttFileId, x => x.Count);

            // Get existing translations (non-English VTT files)
            var translations = await _context.VttFiles
                .AsNoTracking()
                .Where(v => fileIds.Contains(v.FileId) && v.Language != "en" && v.Language != "english")
                .Include(v => v.Cues)
                .ToListAsync();

            var translationsByFileId = translations
                .GroupBy(v => v.FileId)
                .ToDictionary(g => g.Key, g => g.ToList());

            Files = files.Select(f => new TranslatableFileView
            {
                FileId = f.Id,
                FileName = f.File_Name,
                Conference = f.Conference,
                SourceCueCount = sourceCueCounts.GetValueOrDefault(f.SourceVttFileId, 0),
                SourceVttFileId = f.SourceVttFileId,
                ExistingTranslations = translationsByFileId.GetValueOrDefault(f.Id, new List<VttFile>())
                    .Select(t => new TranslationInfoView
                    {
                        VttFileId = t.Id,
                        Language = t.Language,
                        CueCount = t.CueCount,
                        UpdatedAt = t.UpdatedAt
                    })
                    .ToList()
            }).ToList();
        }

        public class TranslatableFileView
        {
            public long FileId { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string? Conference { get; set; }
            public int SourceCueCount { get; set; }
            public long SourceVttFileId { get; set; }
            public List<TranslationInfoView> ExistingTranslations { get; set; } = new();
        }

        public class TranslationInfoView
        {
            public long VttFileId { get; set; }
            public string Language { get; set; } = string.Empty;
            public int CueCount { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
