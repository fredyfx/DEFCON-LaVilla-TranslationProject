using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints;

public class SummaryEndpoint : IEndpoint
{
    public record SummaryRequest(long FileId, string ShortSummary, List<string>? KeyTopics, List<string>? Keywords, string? FullSummary, string? GeneratedBy);
    public record SummaryResponse(long Id, long FileId, string ShortSummary, List<string> KeyTopics, List<string> Keywords, string? FullSummary, string GeneratedBy, DateTime CreatedAt);
    public record SearchResultDto(long FileId, string FileName, string? Conference, string ShortSummary, List<string> Keywords, float Rank);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/summary/search?q=hacking+wifi&limit=20
        app.MapGet("/api/summary/search", async (ApiContext db, string q, int limit = 20) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest("Query parameter 'q' is required");

            limit = Math.Clamp(limit, 1, 100);

            // Convert query to tsquery (websearch format handles phrases and operators)
            var results = await db.Summaries
                .Include(s => s.File)
                .Where(s => s.SearchVector!.Matches(EF.Functions.WebSearchToTsQuery("english", q)))
                .OrderByDescending(s => s.SearchVector!.Rank(EF.Functions.WebSearchToTsQuery("english", q)))
                .Take(limit)
                .Select(s => new SearchResultDto(
                    s.FileId,
                    s.File.File_Name,
                    s.File.Conference,
                    s.ShortSummary,
                    s.Keywords,
                    s.SearchVector!.Rank(EF.Functions.WebSearchToTsQuery("english", q))
                ))
                .ToListAsync();

            return Results.Ok(new { Query = q, Count = results.Count, Results = results });
        })
        .RequireAuthorization("ApiAccess")
        .RequireRateLimiting("AuthenticatedPolicy");

        // POST /api/summary - Create or update
        app.MapPost("/api/summary", async (ApiContext db, SummaryRequest req) =>
        {
            if (!await db.Files.AnyAsync(f => f.Id == req.FileId))
                return Results.NotFound($"File {req.FileId} not found");

            var existing = await db.Summaries.FirstOrDefaultAsync(s => s.FileId == req.FileId);
            if (existing != null)
            {
                existing.ShortSummary = req.ShortSummary;
                existing.KeyTopics = req.KeyTopics ?? new();
                existing.Keywords = req.Keywords ?? new();
                existing.FullSummary = req.FullSummary;
                existing.GeneratedBy = req.GeneratedBy ?? "ollama";
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await db.Summaries.AddAsync(new Summary
                {
                    FileId = req.FileId,
                    ShortSummary = req.ShortSummary,
                    KeyTopics = req.KeyTopics ?? new(),
                    Keywords = req.Keywords ?? new(),
                    FullSummary = req.FullSummary,
                    GeneratedBy = req.GeneratedBy ?? "ollama"
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        })
        .RequireAuthorization("ApiAccess")
        .RequireRateLimiting("AuthenticatedPolicy");

        // GET /api/summary/{fileId}
        app.MapGet("/api/summary/{fileId}", async (ApiContext db, long fileId) =>
        {
            var s = await db.Summaries.FirstOrDefaultAsync(x => x.FileId == fileId);
            return s == null ? Results.NotFound() : Results.Ok(new SummaryResponse(s.Id, s.FileId, s.ShortSummary, s.KeyTopics, s.Keywords, s.FullSummary, s.GeneratedBy, s.CreatedAt));
        })
        .RequireAuthorization("ApiAccess")
        .RequireRateLimiting("AuthenticatedPolicy");

        // DELETE /api/summary/{fileId}
        app.MapDelete("/api/summary/{fileId}", async (ApiContext db, long fileId) =>
        {
            var s = await db.Summaries.FirstOrDefaultAsync(x => x.FileId == fileId);
            if (s == null) return Results.NotFound();
            db.Summaries.Remove(s);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization("ApiAccess")
        .RequireRateLimiting("AuthenticatedPolicy");
    }
}
