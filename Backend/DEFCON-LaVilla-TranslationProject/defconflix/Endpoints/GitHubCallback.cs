using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Endpoints
{
    public class GitHubCallback : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/signin-github", async (HttpContext context, ApiContext db, IJwtTokenService jwtService) =>
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    return Results.Redirect("/login?error=authentication_failed");
                }
                
                var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = context.User.FindFirst(ClaimTypes.Name)?.Value;
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
                var name = context.User.FindFirst("name")?.Value;

                if (string.IsNullOrEmpty(githubId) || string.IsNullOrEmpty(username))
                {
                    return Results.Redirect("/login?error=missing_user_data");
                }

                try
                {
                    // Check if user exists, if not create them
                    var existingUser = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

                    if (existingUser == null)
                    {
                        var newUser = new User
                        {
                            GitHubId = githubId,
                            Username = username,
                            Email = email ?? "",
                            ApiKey = Guid.NewGuid().ToString(),
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };

                        db.Users.Add(newUser);
                        await db.SaveChangesAsync();
                        existingUser = newUser;
                    }
                    else
                    {
                        // Update existing user info
                        existingUser.Username = username;
                        existingUser.Email = email ?? existingUser.Email;
                        existingUser.LastAccessedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }

                    // Sign the user in with a cookie
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, githubId),
                        new Claim(ClaimTypes.Name, username),
                        new Claim(ClaimTypes.Email, email ?? ""),
                        new Claim("UserId", existingUser.Id.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

                    return Results.Redirect("/profile");
                }
                catch (Exception ex)
                {
                    // Log the exception
                    return Results.Redirect("/login?error=database_error");
                }
            });
        }
    }
}
