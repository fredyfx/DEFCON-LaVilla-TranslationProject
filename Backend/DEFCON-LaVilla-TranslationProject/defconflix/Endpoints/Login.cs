using defconflix.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace defconflix.Endpoints
{
    public class Login : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/login", async (HttpContext context) =>
            {
                await context.ChallengeAsync("GitHub", new AuthenticationProperties
                {
                    RedirectUri = "/profile"
                });
            }).RequireRateLimiting("AuthPolicy");
        }
    }
}
