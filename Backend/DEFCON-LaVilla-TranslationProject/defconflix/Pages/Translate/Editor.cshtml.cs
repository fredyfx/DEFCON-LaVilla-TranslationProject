using defconflix.Data;
using defconflix.Models;
using FileModel = defconflix.Models.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.Translate
{
    [Authorize]
    public class EditorModel : BasePageModel
    {
        private readonly ApiContext _context;

        public EditorModel(ApiContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public long FileId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Lang { get; set; } = "es";

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        public FileModel? SourceFile { get; set; }
        public VttFile? SourceVtt { get; set; }
        public VttFile? TargetVtt { get; set; }
        public List<TranslationCueView> Cues { get; set; } = new();
        public int TotalCues { get; set; }
        public int TotalPages { get; set; }
        public int TranslatedCues { get; set; }
        public double ProgressPercent { get; set; }
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        public async Task<IActionResult> OnGetAsync(long fileId)
        {
            FileId = fileId;

            // Get source file
            SourceFile = await _context.Files.FindAsync(FileId);
            if (SourceFile == null)
            {
                return NotFound("File not found");
            }

            // Get source VTT (English)
            SourceVtt = await _context.VttFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FileId == FileId && (v.Language == "en" || v.Language == "english"));

            if (SourceVtt == null)
            {
                return NotFound("No English VTT file found");
            }

            // Get or create target VTT
            TargetVtt = await _context.VttFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FileId == FileId && v.Language == Lang);

            // Load source cues with pagination
            TotalCues = await _context.VttCues
                .Where(c => c.VttFileId == SourceVtt.Id)
                .CountAsync();

            TotalPages = (int)Math.Ceiling((double)TotalCues / PageSize);

            var sourceCues = await _context.VttCues
                .AsNoTracking()
                .Where(c => c.VttFileId == SourceVtt.Id)
                .OrderBy(c => c.SequenceOrder)
                .ThenBy(c => c.StartTime)
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Get target cues if translation exists
            Dictionary<int, VttCue> targetCuesBySequence = new();
            if (TargetVtt != null)
            {
                var targetCues = await _context.VttCues
                    .AsNoTracking()
                    .Where(c => c.VttFileId == TargetVtt.Id)
                    .ToListAsync();
                targetCuesBySequence = targetCues.ToDictionary(c => c.SequenceOrder);

                TranslatedCues = targetCues.Count(c => !string.IsNullOrWhiteSpace(c.Text));
            }

            ProgressPercent = TotalCues > 0 ? Math.Round((double)TranslatedCues / TotalCues * 100, 1) : 0;

            Cues = sourceCues.Select(sc => new TranslationCueView
            {
                SourceCueId = sc.Id,
                SequenceOrder = sc.SequenceOrder,
                FormattedTimestamp = sc.FormattedTimestamp,
                SourceText = sc.Text,
                TargetCueId = targetCuesBySequence.GetValueOrDefault(sc.SequenceOrder)?.Id,
                TranslatedText = targetCuesBySequence.GetValueOrDefault(sc.SequenceOrder)?.Text ?? ""
            }).ToList();

            return Page();
        }

        public class TranslationCueView
        {
            public long SourceCueId { get; set; }
            public int SequenceOrder { get; set; }
            public string FormattedTimestamp { get; set; } = string.Empty;
            public string SourceText { get; set; } = string.Empty;
            public long? TargetCueId { get; set; }
            public string TranslatedText { get; set; } = string.Empty;
        }
    }
}
