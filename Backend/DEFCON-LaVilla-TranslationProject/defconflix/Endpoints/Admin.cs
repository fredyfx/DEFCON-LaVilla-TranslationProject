using defconflix.Interfaces;

namespace defconflix.Endpoints
{
    public class Admin : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/admin/dashboard", () =>
            {
                return Results.Json(new { data = "WIP" });
            }).RequireRateLimiting("AuthenticatedPolicy");
        }
    }
}
