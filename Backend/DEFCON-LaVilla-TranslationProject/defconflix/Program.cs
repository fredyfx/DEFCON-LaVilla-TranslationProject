using defconflix.Data;
using defconflix.Extensions;
using defconflix.Interfaces;
using defconflix.Middleware;
using defconflix.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var Configuration = builder.Configuration;

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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApiContext>();
    await context.Database.MigrateAsync();
}

// Middleware
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Enhanced API Key and JWT authentication middleware
app.UseAuthenticationMiddleware();

app.MapEndpoints();

app.Run();
