using defconflix.Data;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class ConferencesEndpoints : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/conferences", GetAllConferences);
        }

        private async Task<IResult> GetAllConferences(ApiContext db)
        {
            try
            {
                var conferences = await db.Conferences.ToListAsync();

                return Results.Json(new { Conferences = conferences });
            }
            catch (Exception ex)
            {
                return Results.Problem("An error occurred while retrieving conferences.");
            }
        }
    }
}
