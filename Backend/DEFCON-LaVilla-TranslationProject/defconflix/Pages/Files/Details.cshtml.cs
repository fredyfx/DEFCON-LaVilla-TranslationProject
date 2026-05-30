using defconflix.Data;
using defconflix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.Files
{
    [Authorize(Policy = "ApiAccess")]
    public class DetailsModel : BasePageModel
    {
        private readonly ApiContext _context;

        public DetailsModel(ApiContext context)
        {
            _context = context;
        }

        public Models.Files? File { get; set; }
        public List<VttFile> Translations { get; set; } = new();
        public List<FileStatusCheck> StatusHistory { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(long id)
        {
            File = await _context.Files
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (File == null)
            {
                return NotFound();
            }

            // Get translations for this file
            Translations = await _context.VttFiles
                .AsNoTracking()
                .Where(v => v.FileName.Contains(File.File_Name.Replace(File.Extension, "")))
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            // Get status check history
            StatusHistory = await _context.FileStatusChecks
                .AsNoTracking()
                .Where(s => s.FileId == id)
                .OrderByDescending(s => s.CheckedAt)
                .Take(10)
                .ToListAsync();

            return Page();
        }

        public string GetFileSizeFormatted()
        {
            if (File == null) return "Unknown";
            var bytes = File.Size_Bytes;
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
