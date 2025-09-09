using defconflix.Data;
using defconflix.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Endpoints
{
    public class Users : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/users", async (ApiContext db, int page = 1, int pageSize = 10) =>
            {
                // Validate pagination parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

                var totalUsers = await db.Users.CountAsync(u => u.IsActive);
                var totalPages = (int)Math.Ceiling((double)totalUsers / pageSize);

                var users = await db.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Username)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => u.Username)
                    .ToListAsync();

                return Results.Json(new
                {
                    Users = users,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = totalPages,
                        TotalUsers = totalUsers,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages
                    }
                });
            }).RequireAuthorization();
        }
    }
}
