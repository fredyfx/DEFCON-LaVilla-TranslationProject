using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Endpoints
{
    public class Profile : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/profile", async (HttpContext context, ApiContext db, IJwtTokenService jwtService) =>
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    return Results.Redirect("/login");
                }

                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(githubId))
                {
                    return Results.Redirect("/login?error=invalid_session");
                }

                var user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

                if (user == null)
                {
                    return Results.Redirect("/login?error=user_not_found");
                }

                // Generate JWT token
                var token = jwtService.GenerateToken(user);

                return Results.Json(new
                {
                    Username = user.Username,
                    Email = user.Email,
                    ApiKey = user.ApiKey,
                    JwtToken = token,
                    CreatedAt = user.CreatedAt,
                    LastAccessed = user.LastAccessedAt
                });
            }).RequireRateLimiting("AuthPolicy");
        }
    }
}
