using defconflix.Data;
using defconflix.Exceptions;
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
});

// Add PostgreSQL database
builder.Services.AddPersistenceFX(Configuration);

// Add Authentication
builder.Services.AddAuthFX(Configuration);

// Add Rate Limiting
builder.Services.AddRateLimiterFX(Configuration);
builder.Services.AddAuthorization();

// Add HttpClient for file checking (register as singleton, not scoped)
builder.Services.AddHttpClient();

// Add services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IFileTextService, FileTextService>();
builder.Services.AddScoped<IFileCheckerService, FileCheckerService>();
builder.Services.AddScoped<IWebCrawlerService, WebCrawlerService>();
builder.Services.AddRazorPages();
// Add Background Services
builder.Services.AddFileCheckBackgroundService();
// Add Custom Exception Handler
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
// Adding Endpoints
builder.Services.AddEndpoints();

// Add ProblemDetails for better error handling
builder.Services.AddProblemDetails();

var app = builder.Build();

// Database migration
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApiContext>();
    await context.Database.MigrateAsync();
}

app.UseForwardedHeaders();

if (builder.Environment.IsProduction())
{
    app.UseExceptionHandler();
}

// Middleware
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Enhanced API Key and JWT authentication middleware
app.UseAuthenticationMiddleware();

app.MapEndpoints();
app.MapRazorPages();
app.Run();
