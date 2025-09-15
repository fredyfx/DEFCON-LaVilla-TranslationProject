using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class VttCueEndpoint : IEndpoint
    {
        public record VttCueRequest(int VttFileId, TimeSpan StartTime, TimeSpan EndTime, string Text, int SequenceOrder);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/vttcue", async (ApiContext db, VttCueRequest request) =>
            {
                var alreadyExist = await db.VttCues
                .Where(x => x.VttFileId == request.VttFileId &&
                    x.StartTime == request.StartTime &&
                    x.EndTime == request.EndTime)
                .AnyAsync();

                if (alreadyExist)
                {
                    return Results.Conflict("Cue already exists for the given VttFileId and time range.");
                }

                var vttcue = new VttCue
                {
                    VttFileId = request.VttFileId,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Text = request.Text,
                    SequenceOrder = request.SequenceOrder
                };
                await db.VttCues.AddAsync(vttcue);
                await db.SaveChangesAsync();
                return Results.Ok();
            }).RequireAuthorization("ApiAccess")
             .RequireRateLimiting("AuthenticatedPolicy");
        }
    }
}
