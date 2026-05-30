using defconflix.Data;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Extensions
{
    public static class PersistenceExtension
    {
        public static void AddPersistenceFX(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApiContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(30); // 30 second timeout for queries
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }));
        }
    }
}
