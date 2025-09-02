using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Services
{
    public class FileTextService : IFileTextService
    {
        private readonly ApiContext _context;

        public FileTextService(ApiContext context)
        {
            _context = context;
        }

        public async Task<string?> GetPureTextAsync(int vttFileId, VttTextExtractionOptions? options = null)
        {
            options ??= new VttTextExtractionOptions();

            var vttFile = await _context.VttFiles
                .Include(v => v.Cues)
                .FirstOrDefaultAsync(v => v.Id == vttFileId);

            if (vttFile == null)
                return null;

            return vttFile.ExtractPureText(options);
        }

        public async Task<string?> GetPureTextByIdAsync(int id, VttTextExtractionOptions? options = null)
        {
            options ??= new VttTextExtractionOptions();

            var vttFile = await _context.VttFiles
                .Include(v => v.Cues)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vttFile == null)
                return null;

            return vttFile.ExtractPureText(options);
        }
    }
}
