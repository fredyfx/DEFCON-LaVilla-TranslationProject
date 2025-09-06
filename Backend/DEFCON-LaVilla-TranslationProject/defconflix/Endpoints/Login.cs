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
                // Check if already authenticated
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    return Results.Redirect("/profile");
                }

                // Check for error parameters
                var error = context.Request.Query["error"].FirstOrDefault();
                if (!string.IsNullOrEmpty(error))
                {
                    return Results.Json(new
                    {
                        error = error,
                        message = "Authentication failed. Please try again.",
                        retry_url = "/login"
                    });
                }

                await context.ChallengeAsync("GitHub", new AuthenticationProperties
                {
                    RedirectUri = "/profile"
                });

                return Results.Empty;
            });
        }
    }
}
