using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace defconflix.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly TimeSpan UserCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LastAccessUpdateInterval = TimeSpan.FromMinutes(1);

        public AuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IMemoryCache cache)
        {
            if (context.Request.Path.StartsWithSegments("/api/protected"))
            {
                var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                User? user = null;

                // Try to get user from OAuth claims first (no DB hit if already authenticated)
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var githubId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(githubId))
                    {
                        user = await GetUserByGitHubIdCachedAsync(context, cache, githubId);
                    }
                }

                // Try Bearer token
                if (user == null && !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    using var scope = context.RequestServices.CreateScope();
                    var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
                    var principal = jwtService.ValidateToken(token);

                    if (principal != null)
                    {
                        var userId = principal.FindFirst("userId")?.Value;
                        if (int.TryParse(userId, out var id))
                        {
                            user = await GetUserByIdCachedAsync(context, cache, id);
                            if (user != null)
                            {
                                SetUserClaimsPrincipal(context, user, "Bearer");
                            }
                        }
                    }
                }
                // Fallback to API Key
                else if (user == null && !string.IsNullOrEmpty(apiKey))
                {
                    user = await GetUserByApiKeyCachedAsync(context, cache, apiKey);
                    if (user != null)
                    {
                        SetUserClaimsPrincipal(context, user, "ApiKey");
                    }
                }

                if (user == null)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Authentication required. Use Bearer token or X-API-Key header.");
                    return;
                }

                // Update last accessed (throttled to avoid DB write on every request)
                await UpdateLastAccessedThrottledAsync(context, cache, user);

                // Add user info to context
                context.Items["UserId"] = user.Id;
                context.Items["Username"] = user.Username;
                context.Items["User"] = user;
                context.Items["UserRole"] = user.Role;
            }

            await _next(context);
        }

        private static void SetUserClaimsPrincipal(HttpContext context, User user, string authType)
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

            var identity = new ClaimsIdentity(claims, authType);
            context.User = new ClaimsPrincipal(identity);
        }

        private static async Task<User?> GetUserByGitHubIdCachedAsync(HttpContext context, IMemoryCache cache, string githubId)
        {
            var cacheKey = $"user_github_{githubId}";

            if (cache.TryGetValue(cacheKey, out User? cachedUser))
            {
                return cachedUser;
            }

            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiContext>();

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.GitHubId == githubId && u.IsActive);

            if (user != null)
            {
                cache.Set(cacheKey, user, UserCacheDuration);
            }

            return user;
        }

        private static async Task<User?> GetUserByIdCachedAsync(HttpContext context, IMemoryCache cache, int userId)
        {
            var cacheKey = $"user_id_{userId}";

            if (cache.TryGetValue(cacheKey, out User? cachedUser))
            {
                return cachedUser;
            }

            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiContext>();

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user != null)
            {
                cache.Set(cacheKey, user, UserCacheDuration);
            }

            return user;
        }

        private static async Task<User?> GetUserByApiKeyCachedAsync(HttpContext context, IMemoryCache cache, string apiKey)
        {
            var cacheKey = $"user_apikey_{apiKey}";

            if (cache.TryGetValue(cacheKey, out User? cachedUser))
            {
                return cachedUser;
            }

            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiContext>();

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ApiKey == apiKey && u.IsActive);

            if (user != null)
            {
                cache.Set(cacheKey, user, UserCacheDuration);
            }

            return user;
        }

        private static async Task UpdateLastAccessedThrottledAsync(HttpContext context, IMemoryCache cache, User user)
        {
            var throttleKey = $"user_lastaccess_{user.Id}";

            // Only update if not updated recently (throttle to once per minute)
            if (!cache.TryGetValue(throttleKey, out _))
            {
                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApiContext>();

                // Update LastAccessedAt in background (fire-and-forget for non-critical update)
                var userEntity = await db.Users.FindAsync(user.Id);
                if (userEntity != null)
                {
                    userEntity.LastAccessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                // Mark as updated for the throttle interval
                cache.Set(throttleKey, true, LastAccessUpdateInterval);
            }
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
