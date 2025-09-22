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
            app.MapGet("/api/profile", async (HttpContext context, ApiContext db, IJwtTokenService jwtService, ILogger<Profile> logger) =>
            {
                // Check if request is from a web browser (Accept header contains text/html)
                var acceptHeader = context.Request.Headers["Accept"].ToString();
                if (acceptHeader.Contains("text/html"))
                {
                    // Redirect to Razor Page
                    return Results.Redirect("/Profile");
                }

                if (!context.User.Identity.IsAuthenticated)
                {
                    logger.LogWarning("Unauthenticated user attempted to access profile");
                    return Results.Redirect("/login");
                }

                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = context.User.FindFirst(ClaimTypes.Name)?.Value;
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value;

                logger.LogInformation("Profile access attempt - GitHubId: {GitHubId}, Username: {Username}, Email: {Email}",
                    githubId, username, email);

                if (string.IsNullOrEmpty(githubId))
                {
                    logger.LogError("GitHub ID claim is null or empty");
                    return Results.Redirect("/logout"); // Force re-authentication
                }

                var existingUser = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

                if (existingUser == null)
                {
                    logger.LogWarning("User not found in database for GitHubId: {GitHubId}", githubId);

                    // Check if this is a creation failure vs missing user
                    var userCount = await db.Users.CountAsync(u => u.Username == username);
                    if (userCount > 0)
                    {
                        logger.LogError("Username {Username} exists but with different GitHubId", username);
                        return Results.Problem("Account mismatch detected. Please contact support.");
                    }

                    // Attempt to create user
                    try
                    {
                        var newUser = new User
                        {
                            GitHubId = githubId,
                            Username = username ?? "unknown",
                            Email = email ?? "",
                            ApiKey = Guid.NewGuid().ToString(),
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };

                        db.Users.Add(newUser);
                        await db.SaveChangesAsync();

                        logger.LogInformation("Created new user: {GitHubId}, {Username}", githubId, username);
                        existingUser = newUser;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create user for GitHubId: {GitHubId}", githubId);
                        return Results.Problem("Failed to create user account. Please try again.");
                    }
                }

                // Generate JWT token
                var token = jwtService.GenerateToken(existingUser);
                var loggedUser = new LoggedUser(
                    existingUser.Username,
                    existingUser.Email,
                    existingUser.ApiKey,
                    token,
                    existingUser.CreatedAt,
                    existingUser.LastAccessedAt
                );
                existingUser.LastAccessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Json(loggedUser);
            }).RequireRateLimiting("AuthPolicy");
        }
    }

    internal record LoggedUser(string Username, string Email, string ApiKey, string JwtToken, DateTime CreatedAt, DateTime? LastAccessed);
}
