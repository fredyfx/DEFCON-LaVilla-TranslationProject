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
                    var errorMessage = error switch
                    {
                        "oauth_failed" => "GitHub OAuth authentication failed. Please try again.",
                        "missing_claims" => "Required information missing from GitHub. Please try again.",
                        _ => "Authentication error occurred. Please try again."
                    };

                    return Results.Text($"Login Error: {errorMessage}\n\nClick here to try again: <a href='/login'>Login with GitHub</a>", "text/html");
                }

                await context.ChallengeAsync("GitHub", new AuthenticationProperties
                {
                    RedirectUri = "/profile",
                    Items =
                    {
                        { "scheme", "GitHub" }
                    }
                });

                return Results.Empty;
            });
        }
    }
}
