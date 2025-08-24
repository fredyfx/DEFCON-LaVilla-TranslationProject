using defconflix.Data;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class Admin : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/admin/users", async (ApiContext db) =>
            {
                var users = await db.Users
                    .Select(u => new { 
                        u.Id, 
                        u.Username, 
                        u.Email, 
                        u.CreatedAt, 
                        u.IsActive, 
                        u.LastAccessedAt })
                    .ToListAsync();
                return Results.Json(users);
            }).RequireAuthorization()
            .RequireRateLimiting("AuthenticatedPolicy");
        }
    }
}
