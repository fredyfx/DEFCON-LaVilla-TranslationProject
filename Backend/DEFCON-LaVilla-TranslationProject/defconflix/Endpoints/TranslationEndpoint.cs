using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Endpoints
{
    public class TranslationEndpoint : IEndpoint
    {
        // Request/Response DTOs
        public record TranslatableFileDto(
            long FileId,
            string FileName,
            string? Conference,
            int SourceCueCount,
            long? SourceVttFileId,
            List<TranslationInfo> ExistingTranslations
        );

        public record TranslationInfo(long VttFileId, string Language, int CueCount, DateTime UpdatedAt);

        public record CueDto(
            long Id,
            int SequenceOrder,
            string FormattedTimestamp,
            string Text,
            TimeSpan StartTime,
            TimeSpan EndTime
        );

        public record TranslationCueDto(
            long SourceCueId,
            int SequenceOrder,
            string FormattedTimestamp,
            string SourceText,
            long? TargetCueId,
            string? TranslatedText
        );

        public record StartTranslationRequest(string TargetLanguage);
        public record StartTranslationResponse(long TargetVttFileId);

        public record SaveCueRequest(string Text);
        public record BulkSaveCueRequest(List<CueSaveItem> Cues);
        // Support both sourceCueId (DB ID) and sequenceOrder (for pipeline without DB IDs)
        public record CueSaveItem(long? SourceCueId, int? SequenceOrder, string Text);

        public record ProgressResponse(
            int TotalCues,
            int TranslatedCues,
            double PercentComplete
        );

        public record PaginatedCuesResponse(
            List<TranslationCueDto> Cues,
            int Page,
            int PageSize,
            int TotalCues,
            int TotalPages
        );

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            // GET /api/translate/files - List translatable files (have English VTT)
            app.MapGet("/api/translate/files", GetTranslatableFiles)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // GET /api/translate/{fileId}/cues - Get source cues with existing translations
            app.MapGet("/api/translate/{fileId}/cues", GetCuesForTranslation)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // POST /api/translate/{fileId}/start - Start/resume translation
            app.MapPost("/api/translate/{fileId}/start", StartTranslation)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // PUT /api/translate/cue/{targetCueId} - Save single cue
            app.MapPut("/api/translate/cue/{targetCueId}", SaveCue)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // POST /api/translate/{vttFileId}/cues/bulk - Bulk save cues
            app.MapPost("/api/translate/{vttFileId}/cues/bulk", BulkSaveCues)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // GET /api/translate/{vttFileId}/progress - Get translation progress
            app.MapGet("/api/translate/{vttFileId}/progress", GetProgress)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");

            // POST /api/translate/{vttFileId}/complete - Mark translation complete
            app.MapPost("/api/translate/{vttFileId}/complete", MarkComplete)
                .RequireAuthorization("ApiAccess")
                .RequireRateLimiting("AuthenticatedPolicy");
        }

        private static async Task<IResult> GetTranslatableFiles(
            ApiContext db,
            string? conference = null,
            int page = 1,
            int pageSize = 20)
        {
            // Files that have English VTT files
            var query = db.VttFiles
                .AsNoTracking()
                .Where(v => v.Language == "en" || v.Language == "english")
                .Join(db.Files, v => v.FileId, f => f.Id, (v, f) => new { VttFile = v, File = f });

            if (!string.IsNullOrEmpty(conference))
            {
                query = query.Where(x => x.File.Conference == conference);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var files = await query
                .OrderBy(x => x.File.File_Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
            var sourceCueCounts = await db.VttCues
                .Where(c => sourceVttIds.Contains(c.VttFileId))
                .GroupBy(c => c.VttFileId)
                .Select(g => new { VttFileId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VttFileId, x => x.Count);

            // Get existing translations (non-English VTT files for same FileId)
            var translations = await db.VttFiles
                .AsNoTracking()
                .Where(v => fileIds.Contains(v.FileId) && v.Language != "en" && v.Language != "english")
                .Include(v => v.Cues)
                .ToListAsync();

            var translationsByFileId = translations
                .GroupBy(v => v.FileId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = files.Select(f => new TranslatableFileDto(
                FileId: f.Id,
                FileName: f.File_Name,
                Conference: f.Conference,
                SourceCueCount: sourceCueCounts.GetValueOrDefault(f.SourceVttFileId, 0),
                SourceVttFileId: f.SourceVttFileId,
                ExistingTranslations: translationsByFileId.GetValueOrDefault(f.Id, new List<VttFile>())
                    .Select(t => new TranslationInfo(t.Id, t.Language, t.CueCount, t.UpdatedAt))
                    .ToList()
            )).ToList();

            return Results.Ok(new
            {
                Files = result,
                Page = page,
                PageSize = pageSize,
                TotalFiles = totalCount,
                TotalPages = totalPages
            });
        }

        private static async Task<IResult> GetCuesForTranslation(
            ApiContext db,
            long fileId,
            string lang = "es",
            int page = 1,
            int pageSize = 25)
        {
            // Get source VTT file (English)
            var sourceVtt = await db.VttFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FileId == fileId && (v.Language == "en" || v.Language == "english"));

            if (sourceVtt == null)
            {
                return Results.NotFound("No English VTT file found for this file");
            }

            // Get target VTT file if exists
            var targetVtt = await db.VttFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FileId == fileId && v.Language == lang);

            // Get source cues with pagination
            var totalCues = await db.VttCues
                .Where(c => c.VttFileId == sourceVtt.Id)
                .CountAsync();

            var totalPages = (int)Math.Ceiling((double)totalCues / pageSize);

            var sourceCues = await db.VttCues
                .AsNoTracking()
                .Where(c => c.VttFileId == sourceVtt.Id)
                .OrderBy(c => c.SequenceOrder)
                .ThenBy(c => c.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get target cues if translation exists
            Dictionary<int, VttCue> targetCuesBySequence = new();
            if (targetVtt != null)
            {
                var targetCues = await db.VttCues
                    .AsNoTracking()
                    .Where(c => c.VttFileId == targetVtt.Id)
                    .ToListAsync();
                targetCuesBySequence = targetCues.ToDictionary(c => c.SequenceOrder);
            }

            var result = sourceCues.Select(sc => new TranslationCueDto(
                SourceCueId: sc.Id,
                SequenceOrder: sc.SequenceOrder,
                FormattedTimestamp: sc.FormattedTimestamp,
                SourceText: sc.Text,
                TargetCueId: targetCuesBySequence.GetValueOrDefault(sc.SequenceOrder)?.Id,
                TranslatedText: targetCuesBySequence.GetValueOrDefault(sc.SequenceOrder)?.Text
            )).ToList();

            return Results.Ok(new PaginatedCuesResponse(
                Cues: result,
                Page: page,
                PageSize: pageSize,
                TotalCues: totalCues,
                TotalPages: totalPages
            ));
        }

        private static async Task<IResult> StartTranslation(
            HttpContext context,
            ApiContext db,
            long fileId,
            StartTranslationRequest request)
        {
            // Get source file
            var file = await db.Files.FindAsync(fileId);
            if (file == null)
            {
                return Results.NotFound("File not found");
            }

            // Get source VTT
            var sourceVtt = await db.VttFiles
                .Include(v => v.Cues)
                .FirstOrDefaultAsync(v => v.FileId == fileId && (v.Language == "en" || v.Language == "english"));

            if (sourceVtt == null)
            {
                return Results.NotFound("No English VTT file found for this file");
            }

            // Check if translation already exists
            var existingTranslation = await db.VttFiles
                .FirstOrDefaultAsync(v => v.FileId == fileId && v.Language == request.TargetLanguage);

            if (existingTranslation != null)
            {
                return Results.Ok(new StartTranslationResponse(existingTranslation.Id));
            }

            // Create new translation VTT file
            var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

            var targetVtt = new VttFile
            {
                FileName = $"{sourceVtt.FileName}_{request.TargetLanguage}",
                Language = request.TargetLanguage,
                FileId = fileId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.VttFiles.Add(targetVtt);
            await db.SaveChangesAsync();

            return Results.Ok(new StartTranslationResponse(targetVtt.Id));
        }

        private static async Task<IResult> SaveCue(
            ApiContext db,
            long targetCueId,
            SaveCueRequest request)
        {
            var cue = await db.VttCues.FindAsync(targetCueId);
            if (cue == null)
            {
                return Results.NotFound("Cue not found");
            }

            cue.Text = request.Text;

            // Update parent VttFile timestamp
            var vttFile = await db.VttFiles.FindAsync(cue.VttFileId);
            if (vttFile != null)
            {
                vttFile.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        }

        private static async Task<IResult> BulkSaveCues(
            ApiContext db,
            long vttFileId,
            BulkSaveCueRequest request)
        {
            var vttFile = await db.VttFiles
                .Include(v => v.Cues)
                .FirstOrDefaultAsync(v => v.Id == vttFileId);

            if (vttFile == null)
            {
                return Results.NotFound("VTT file not found");
            }

            // Get source VTT to copy timing info
            var sourceVtt = await db.VttFiles
                .Include(v => v.Cues)
                .FirstOrDefaultAsync(v => v.FileId == vttFile.FileId && (v.Language == "en" || v.Language == "english"));

            if (sourceVtt == null)
            {
                return Results.NotFound("Source VTT file not found");
            }

            // Support lookup by ID or sequence order
            var sourceCuesById = sourceVtt.Cues.ToDictionary(c => c.Id);
            var sourceCuesBySequence = sourceVtt.Cues.ToDictionary(c => c.SequenceOrder);
            var existingCuesBySequence = vttFile.Cues.ToDictionary(c => c.SequenceOrder);

            foreach (var item in request.Cues)
            {
                VttCue? sourceCue = null;

                // Try to find source cue by ID first, then by sequence order
                if (item.SourceCueId.HasValue && sourceCuesById.TryGetValue(item.SourceCueId.Value, out sourceCue))
                {
                    // Found by ID
                }
                else if (item.SequenceOrder.HasValue && sourceCuesBySequence.TryGetValue(item.SequenceOrder.Value, out sourceCue))
                {
                    // Found by sequence order
                }
                else
                {
                    continue; // Could not find source cue
                }

                if (existingCuesBySequence.TryGetValue(sourceCue.SequenceOrder, out var existingCue))
                {
                    // Update existing cue
                    existingCue.Text = item.Text;
                }
                else
                {
                    // Create new cue
                    var newCue = new VttCue
                    {
                        VttFileId = vttFileId,
                        SequenceOrder = sourceCue.SequenceOrder,
                        StartTime = sourceCue.StartTime,
                        EndTime = sourceCue.EndTime,
                        Text = item.Text,
                        CueId = sourceCue.CueId
                    };
                    db.VttCues.Add(newCue);
                }
            }

            vttFile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok();
        }

        private static async Task<IResult> GetProgress(ApiContext db, long vttFileId)
        {
            var vttFile = await db.VttFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vttFileId);

            if (vttFile == null)
            {
                return Results.NotFound("VTT file not found");
            }

            // Get source VTT to know total cues
            var sourceVtt = await db.VttFiles
                .AsNoTracking()
                .Include(v => v.Cues)
                .FirstOrDefaultAsync(v => v.FileId == vttFile.FileId && (v.Language == "en" || v.Language == "english"));

            if (sourceVtt == null)
            {
                return Results.NotFound("Source VTT file not found");
            }

            var totalCues = sourceVtt.CueCount;
            var translatedCues = await db.VttCues
                .Where(c => c.VttFileId == vttFileId && !string.IsNullOrEmpty(c.Text))
                .CountAsync();

            var percentComplete = totalCues > 0 ? (double)translatedCues / totalCues * 100 : 0;

            return Results.Ok(new ProgressResponse(
                TotalCues: totalCues,
                TranslatedCues: translatedCues,
                PercentComplete: Math.Round(percentComplete, 1)
            ));
        }

        private static async Task<IResult> MarkComplete(
            HttpContext context,
            ApiContext db,
            long vttFileId)
        {
            var vttFile = await db.VttFiles.FindAsync(vttFileId);
            if (vttFile == null)
            {
                return Results.NotFound("VTT file not found");
            }

            // Update the source file status
            var file = await db.Files.FindAsync(vttFile.FileId);
            if (file != null)
            {
                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

                if (user != null)
                {
                    file.ProcessedBy = user.Id;
                }
                file.Updated_At = DateTime.UtcNow;
            }

            vttFile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok();
        }
    }
}
