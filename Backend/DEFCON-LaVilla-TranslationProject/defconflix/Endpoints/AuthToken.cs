using defconflix.Data;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class AuthToken : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/auth/token", async (HttpContext context, ApiContext db, IJwtTokenService jwtService) =>
            {
                var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

                if (string.IsNullOrEmpty(apiKey))
                {
                    return Results.BadRequest("API Key is required");
                }

                var user = await db.Users.FirstOrDefaultAsync(u => u.ApiKey == apiKey && u.IsActive);

                if (user == null)
                {
                    return Results.Unauthorized();
                }

                var token = jwtService.GenerateToken(user);

                return Results.Json(new { Token = token, ExpiresIn = 3600 });
            }).RequireRateLimiting("AuthPolicy");
        }
    }
}
