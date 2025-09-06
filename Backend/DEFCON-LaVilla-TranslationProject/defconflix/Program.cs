using defconflix.Data;
using defconflix.Extensions;
using defconflix.Interfaces;
using defconflix.Middleware;
using defconflix.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var Configuration = builder.Configuration;

// Configure forwarded headers for reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;

    // Clear known networks and proxies to allow Cloudflare IPs
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust all proxies when using Cloudflare (they use many IP ranges)
    options.RequireHeaderSymmetry = false;
    options.ForwardLimit = null; // Allow unlimited forwarded IPs
});

// Add PostgreSQL database
builder.Services.AddPersistenceFX(Configuration);

// Add Authentication
builder.Services.AddAuthFX(Configuration);

// Add Rate Limiting
builder.Services.AddRateLimiterFX(Configuration);
builder.Services.AddAuthorization();

// Add services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IFileTextService, FileTextService>();

// Adding Endpoints
builder.Services.AddEndpoints();

var app = builder.Build();

// Database migration
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApiContext>();
    await context.Database.MigrateAsync();
}

app.UseForwardedHeaders();


// Middleware
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Enhanced API Key and JWT authentication middleware
app.UseAuthenticationMiddleware();

app.MapEndpoints();

app.Run();
