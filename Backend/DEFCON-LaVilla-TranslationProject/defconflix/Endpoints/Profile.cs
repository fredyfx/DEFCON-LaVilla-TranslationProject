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
                var username = context.User.FindFirst(ClaimTypes.Name)?.Value;
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value;

                // Check if user exists, if not create them
                var existingUser = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);
                if (existingUser == null)
                {
                    var newUser = new User
                    {
                        GitHubId = githubId,
                        Username = username,
                        Email = email,
                        ApiKey = Guid.NewGuid().ToString(),
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    db.Users.Add(newUser);
                    await db.SaveChangesAsync();
                    existingUser = newUser;
                }

                // Generate JWT token
                var token = jwtService.GenerateToken(existingUser);

                return Results.Json(new
                {
                    Username = existingUser.Username,
                    Email = existingUser.Email,
                    ApiKey = existingUser.ApiKey,
                    JwtToken = token,
                    CreatedAt = existingUser.CreatedAt
                });
            }).RequireRateLimiting("AuthPolicy");
        }
    }
}
