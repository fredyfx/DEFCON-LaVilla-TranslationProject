using defconflix.Configurations;
using defconflix.Filters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace defconflix.Extensions
{
    public static class AuthExtensions
    {
        public static IServiceCollection AddAuthFX(this IServiceCollection services, IConfiguration configuration)
        {
            // Add JWT configuration
            var jwtSettings = configuration.GetSection("JWT");
            var secretKey = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"]);

            services.Configure<JwtSettings>(jwtSettings);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "GitHub";
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
                options.SlidingExpiration = true;

                // Important: Prevent redirect loops
                options.Events.OnRedirectToLogin = context =>
                {
                    // If this is an API request, return 401 instead of redirecting
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }

                    // For web requests, allow the redirect
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOAuth("GitHub", options =>
            {
                options.ClientId = configuration["GitHub:ClientId"] ?? throw new InvalidOperationException("GitHub:ClientId not configured");
                options.ClientSecret = configuration["GitHub:ClientSecret"] ?? throw new InvalidOperationException("GitHub:ClientSecret not configured");
                options.CallbackPath = "/signin-github";
                options.Scope.Add("read:user");
                options.SaveTokens = true;
                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        try
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<OAuthEvents>>();
                            logger.LogInformation("OnCreatingTicket started for user: {UserId}",
                                context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                            // Prevent multiple executions by checking if email claim already exists
                            var existingEmailClaim = context.Principal?.FindFirst(ClaimTypes.Email);
                            if (existingEmailClaim != null && !string.IsNullOrEmpty(existingEmailClaim.Value))
                            {
                                logger.LogInformation("Email claim already exists, skipping additional API call");
                                return;
                            }

                            // Get user information
                            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                            var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                            response.EnsureSuccessStatusCode();
                            var responseString = await response.Content.ReadAsStringAsync();
                            logger.LogInformation("User info response: {Response}", responseString);
                            var json = JsonDocument.Parse(responseString);

                            // Check if email is public
                            var emailElement = json.RootElement.GetProperty("email");
                            string? userEmail = null;

                            if (emailElement.ValueKind != JsonValueKind.Null)
                            {
                                userEmail = emailElement.GetString();
                            }

                            // If email is null, fetch from emails endpoint
                            if (string.IsNullOrEmpty(userEmail))
                            {
                                logger.LogInformation("Public email not found, fetching from emails endpoint");

                                var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                                emailRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                                emailRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                                var emailResponse = await context.Backchannel.SendAsync(emailRequest, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);

                                if (emailResponse.IsSuccessStatusCode)
                                {
                                    var emailsJson = JsonDocument.Parse(await emailResponse.Content.ReadAsStringAsync());

                                    // Find primary email or first verified email
                                    foreach (var emailObj in emailsJson.RootElement.EnumerateArray())
                                    {
                                        var isPrimary = emailObj.TryGetProperty("primary", out var primaryProp) && primaryProp.GetBoolean();
                                        var isVerified = emailObj.TryGetProperty("verified", out var verifiedProp) && verifiedProp.GetBoolean();

                                        if (isPrimary && isVerified)
                                        {
                                            userEmail = emailObj.GetProperty("email").GetString();
                                            break;
                                        }
                                    }

                                    // If no primary email found, use first verified email
                                    if (string.IsNullOrEmpty(userEmail))
                                    {
                                        foreach (var emailObj in emailsJson.RootElement.EnumerateArray())
                                        {
                                            var isVerified = emailObj.TryGetProperty("verified", out var verifiedProp) && verifiedProp.GetBoolean();
                                            if (isVerified)
                                            {
                                                userEmail = emailObj.GetProperty("email").GetString();
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("Failed to fetch user emails. Status: {StatusCode}", emailResponse.StatusCode);
                                }
                            }

                            // Run claim actions on the main user info
                            context.RunClaimActions(json.RootElement);

                            // Add email claim if we found one
                            if (!string.IsNullOrEmpty(userEmail) && context.Principal?.Identity is ClaimsIdentity identity)
                            {
                                // Remove any existing email claim to avoid duplicates
                                var existingClaim = identity.FindFirst(ClaimTypes.Email);
                                if (existingClaim != null)
                                {
                                    identity.RemoveClaim(existingClaim);
                                }

                                identity.AddClaim(new Claim(ClaimTypes.Email, userEmail));
                                logger.LogInformation("Added email claim: {Email}", userEmail);
                            }
                            else
                            {
                                logger.LogWarning("No email found for user: {UserId}",
                                    context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                            }

                            logger.LogInformation("OnCreatingTicket completed successfully");
                        }
                        catch (Exception ex)
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<OAuthEvents>>();
                            logger.LogError(ex, "Error in OnCreatingTicket");

                            // Don't throw, allow authentication to continue with basic info
                        }
                    },

                    OnRemoteFailure = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<OAuthEvents>>();
                        logger.LogError("OAuth remote failure: {Error}", context.Failure?.Message);

                        context.Response.Redirect("/");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    },

                    OnAccessDenied = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<OAuthEvents>>();
                        logger.LogWarning("OAuth access denied");

                        context.Response.Redirect("/");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });

            // Add Authorization with Admin policy
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.Requirements.Add(new AdminRequirement()));
            });

            // Register the authorization handler
            services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();
            return services;
        }
    }
}
