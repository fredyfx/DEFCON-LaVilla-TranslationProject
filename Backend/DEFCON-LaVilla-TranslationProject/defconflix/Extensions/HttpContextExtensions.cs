using defconflix.Data;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<User?> GetCurrentUserAsync(this HttpContext context, ApiContext db)
        {
            // First try to get from middleware context items (API Key auth)
            if (context.Items.TryGetValue("User", out var userObj) && userObj is User user)
            {
                return user;
            }

            // Fallback to JWT claims (RequireAuthorization)
            if (context.User.Identity?.IsAuthenticated ?? false)
            {
                var userIdClaim = context.User.FindFirst("userId")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
                {
                    return await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
                }
            }

            return null;
        }
        public static int? GetCurrentUserId(this HttpContext context)
        {
            // Try context items first
            if (context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
            {
                return userId;
            }

            // Try JWT claims
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var id))
            {
                return id;
            }

            return null;
        }
    }
}
