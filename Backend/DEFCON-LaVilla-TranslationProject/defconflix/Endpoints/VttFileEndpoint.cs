using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace defconflix.Endpoints
{
    public class VttFileEndpoint : IEndpoint
    {
        public record VttFileRequest(string FileName, string Hash, string Language);
        public record VttFileResponse(int Id);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {

            app.MapGet("api/vttfile/export/{hash}", async (ApiContext db, IFileTextService fileTextService, string hash) =>
            {
                var currentFile = await db.Files
                .Where(x => x.Hash == hash)
                .SingleAsync();

                var textContent = await fileTextService.GetPureTextByHashAsync(hash);
                var fileBytes = Encoding.UTF8.GetBytes(textContent!);
                return Results.File(
                       fileBytes,
                       contentType: "text/plain",
                       fileDownloadName: $"{currentFile.File_Name}.txt"
                   );
            });

            app.MapPost("/api/vttfile/completed/{hash}", async (HttpContext context, ApiContext db, string hash) =>
            {
                var currentFile = await db.Files
                .Where(x => x.Hash == hash && x.Status == "In Progress")
                .SingleOrDefaultAsync();
                if (currentFile is null)
                {
                    return Results.BadRequest();
                }
                if (currentFile.Status != "In Progress")
                {
                    return Results.BadRequest();
                }
                var vttFile = await db.VttFiles
                    .Where(x => x.Hash == hash)
                    .SingleOrDefaultAsync();
                if (vttFile is null)
                {
                    return Results.BadRequest();
                }
                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var existingUser = await db.Users.SingleAsync(u => u.GitHubId == githubId);
                currentFile.Status = "Completed";
                currentFile.ProcessedBy = existingUser.Id;
                currentFile.Updated_At = DateTime.UtcNow;

                await db.SaveChangesAsync();
                return Results.Ok();
            }).RequireAuthorization()
            .RequireRateLimiting("AuthenticatedPolicy");

            app.MapPost("/api/vttfile/start", async (HttpContext context, ApiContext db, VttFileRequest request) =>
            {
                var currentFile = await db.Files
                .Where(x => x.Hash == request.Hash)
                .SingleOrDefaultAsync();

                if (currentFile is null)
                {
                    return Results.BadRequest();
                }

                // First, check if the hash exists, if so, return the id, otherwise continue
                var exist = await db.VttFiles.SingleOrDefaultAsync(x => x.Hash == request.Hash);
                if (exist != null)
                {
                    var earlyResponse = new VttFileResponse(Id: exist.Id);
                    return Results.Json(earlyResponse);
                }

                if (currentFile.Status != "Not started")
                {
                    return Results.BadRequest();
                }

                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var existingUser = await db.Users.SingleAsync(u => u.GitHubId == githubId);
                currentFile.Status = "In Progress";
                currentFile.ProcessedBy = existingUser.Id;

                var vtt = new VttFile
                {
                    FileName = request.FileName,
                    Hash = request.Hash,
                    Language = request.Language,
                    CreatedAt = DateTime.UtcNow
                };
                await db.VttFiles.AddAsync(vtt);
                await db.SaveChangesAsync();

                var response = new VttFileResponse(Id: vtt.Id);
                return Results.Json(response);
            }).RequireAuthorization()
            .RequireRateLimiting("AuthenticatedPolicy");
        }
    }
}
