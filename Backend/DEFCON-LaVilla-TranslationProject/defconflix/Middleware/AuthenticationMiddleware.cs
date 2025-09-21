using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api/protected"))
            {
                var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                User user = null;

                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApiContext>();

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var githubId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(githubId))
                    {
                        user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId && u.IsActive);
                    }
                }

                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
                    var principal = jwtService.ValidateToken(token);

                    if (principal != null)
                    {
                        var userId = principal.FindFirst("userId")?.Value;
                        if (int.TryParse(userId, out var id))
                        {
                            user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
                            if (user != null)
                            {
                                var claims = new List<Claim>
                                {
                                    new Claim("userId", user.Id.ToString()),
                                    new Claim(ClaimTypes.Name, user.Username),
                                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                                    new Claim("apiKey", user.ApiKey),
                                    new Claim("role", ((int)user.Role).ToString()),
                                    new Claim(ClaimTypes.Role, user.Role.ToString())
                                };

                                var identity = new ClaimsIdentity(claims, "Bearer");
                                context.User = new ClaimsPrincipal(identity);
                            }
                        }
                    }
                }
                // Fallback to API Key
                else if (!string.IsNullOrEmpty(apiKey))
                {
                    user = await db.Users.FirstOrDefaultAsync(u => u.ApiKey == apiKey && u.IsActive);
                    if (user != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim("userId", user.Id.ToString()),
                            new Claim(ClaimTypes.Name, user.Username),
                            new Claim(ClaimTypes.Email, user.Email ?? ""),
                            new Claim("apiKey", user.ApiKey),
                            new Claim("role", ((int)user.Role).ToString()),
                            new Claim(ClaimTypes.Role, user.Role.ToString())
                        };

                        var identity = new ClaimsIdentity(claims, "ApiKey");
                        context.User = new ClaimsPrincipal(identity);
                    }
                }

                if (user == null)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Authentication required. Use Bearer token or X-API-Key header.");
                    return;
                }

                // Update last accessed
                user.LastAccessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                // Add user info to context
                context.Items["UserId"] = user.Id;
                context.Items["Username"] = user.Username;
                context.Items["User"] = user;
                context.Items["UserRole"] = user.Role;
            }

            await _next(context);
        }
    }

    public static class AuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthenticationMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>();
        }
    }
}