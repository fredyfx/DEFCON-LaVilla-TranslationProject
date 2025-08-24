using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class Protected : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/protected/user-info", (HttpContext context) =>
            {
                var user = context.Items["User"] as User;
                return Results.Json(new
                {
                    Message = "This is a protected endpoint",
                    Username = user.Username,
                    Email = user.Email,
                    LastAccessed = user.LastAccessedAt,
                    Timestamp = DateTime.UtcNow
                });
            }).RequireRateLimiting("AuthenticatedPolicy");

            app.MapGet("/api/protected/user-stats", async (HttpContext context, ApiContext db) =>
            {
                var userId = (int)context.Items["UserId"];
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

                return Results.Json(new
                {
                    UserId = user.Id,
                    Username = user.Username,
                    AccountCreated = user.CreatedAt,
                    LastUpdated = user.UpdatedAt,
                    LastAccessed = user.LastAccessedAt,
                    IsActive = user.IsActive
                });
            }).RequireRateLimiting("AuthenticatedPolicy");
        }
    }
}
