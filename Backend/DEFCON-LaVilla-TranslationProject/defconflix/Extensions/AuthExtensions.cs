using defconflix.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;
using System.Buffers.Text;
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
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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

                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";

                // Add scope to request email access
                options.Scope.Add("user:email");

                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.ClaimActions.MapJsonKey("avatar_url", "avatar_url");
                options.ClaimActions.MapJsonKey("name", "name");

                var baseUrl = configuration["BaseUrl"] ?? throw new InvalidOperationException("BaseUrl not configured");

                options.Events = new OAuthEvents
                {
                    OnRedirectToAuthorizationEndpoint = context =>
                    {
                        // Force HTTPS in redirect URI if specified
                        if (baseUrl.StartsWith("https://"))
                        {
                            var redirectUri = context.RedirectUri;
                            if (redirectUri.StartsWith("http://"))
                            {
                                redirectUri = redirectUri.Replace("http://", "https://");
                                context.Response.Redirect(context.Request.Scheme + "://" + context.Request.Host + context.Options.AuthorizationEndpoint +
                                    "?client_id=" + Uri.EscapeDataString(options.ClientId) +
                                    "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                                    "&scope=" + Uri.EscapeDataString(string.Join(" ", options.Scope)) +
                                    "&state=" + Uri.EscapeDataString(context.Properties.Items[".xsrf"]));
                                return Task.CompletedTask;
                            }
                        }
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnCreatingTicket = async context =>
                    {
                        // Get user information
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                        var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        context.RunClaimActions(json.RootElement);

                        // Get user email if not present (some users don't make their email public)
                        if (!context.Principal.Claims.Any(c => c.Type == ClaimTypes.Email && !string.IsNullOrEmpty(c.Value)))
                        {
                            var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                            emailRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                            emailRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                            var emailResponse = await context.Backchannel.SendAsync(emailRequest, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);

                            if (emailResponse.IsSuccessStatusCode)
                            {
                                var emailsJson = JsonDocument.Parse(await emailResponse.Content.ReadAsStringAsync());
                                var primaryEmail = emailsJson.RootElement.EnumerateArray()
                                    .FirstOrDefault(email => email.GetProperty("primary").GetBoolean());

                                if (primaryEmail.ValueKind != JsonValueKind.Undefined)
                                {
                                    var emailAddress = primaryEmail.GetProperty("email").GetString();
                                    if (!string.IsNullOrEmpty(emailAddress))
                                    {
                                        var identity = (ClaimsIdentity)context.Principal.Identity;
                                        identity.AddClaim(new Claim(ClaimTypes.Email, emailAddress));
                                    }
                                }
                            }
                        }
                    }
                };
            });
            return services;
        }
    }
}
