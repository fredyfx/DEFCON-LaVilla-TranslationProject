using defconflix.Data;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Endpoints
{
    public class ResetKey : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/reset-key", async (HttpContext context, ApiContext db) =>
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    return Results.Unauthorized();
                }

                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

                if (user == null)
                {
                    return Results.NotFound("User not found");
                }

                user.ApiKey = Guid.NewGuid().ToString();
                user.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                return Results.Json(new { ApiKey = user.ApiKey });
            }).RequireRateLimiting("AuthPolicy");
        }
    }
}
