using defconflix.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace defconflix.Endpoints
{
    public class Login : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/login", async (HttpContext context, string? error = null) =>
            {
                // If user is already authenticated but there was an error, force logout first
                if (context.User.Identity.IsAuthenticated && !string.IsNullOrEmpty(error))
                {
                    if (error == "user_not_found")
                    {
                        // Clear authentication and force re-login
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return Results.Redirect("/login");
                    }
                }

                // If already authenticated and no errors, go to profile
                if (context.User.Identity.IsAuthenticated)
                {
                    return Results.Redirect("/profile");
                }

                // Check for error parameters
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
                    RedirectUri = "/profile"
                });

                return Results.Empty;
            });
        }
    }
}
