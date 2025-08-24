using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Data
{
    public class ApiContext : DbContext
    {
        public ApiContext(DbContextOptions<ApiContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Files> Files { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Files>()
                .ToTable("files");

            modelBuilder.Entity<Files>()
                .HasKey(f => f.Id);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.GitHubId)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ApiKey)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username);

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
