using defconflix.Data;
using defconflix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Pages.Translations
{
    [Authorize]
    public class IndexModel : BasePageModel
    {
        private readonly ApiContext _context;

        public IndexModel(ApiContext context)
        {
            _context = context;
        }

        public List<TranslationView> Translations { get; set; } = new();
        public int TotalTranslations { get; set; }
        public int TotalCues { get; set; }

        public async Task OnGetAsync()
        {
            // Resolve the signed-in user from claims (cookie and bearer compatible)
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst("userId")?.Value;
            var userId = long.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : (long?)null;

            if (!userId.HasValue)
            {
                return;
            }

            // Get VTT files created by this user (matched via FileId to Files.ProcessedBy)
            var userFiles = await _context.Files
                .AsNoTracking()
                .Where(f => f.ProcessedBy == userId)
                .Select(f => f.Id)
                .ToListAsync();

            var vttFiles = await _context.VttFiles
                .AsNoTracking()
                .Include(v => v.Cues)
                .Where(v => userFiles.Contains(v.FileId))
                .OrderByDescending(v => v.UpdatedAt)
                .ToListAsync();

            Translations = vttFiles.Select(v => new TranslationView
            {
                Id = v.Id,
                FileName = v.FileName,
                Language = v.Language,
                CueCount = v.CueCount,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();

            TotalTranslations = Translations.Count;
            TotalCues = Translations.Sum(t => t.CueCount);
        }

        public class TranslationView
        {
            public long Id { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
            public int CueCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
