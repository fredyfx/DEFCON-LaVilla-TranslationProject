using defconflix.Models;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Data
{
    public class ApiContext : DbContext
    {
        public ApiContext(DbContextOptions<ApiContext> options) : base(options)
        {
        }

        public DbSet<VttFile> VttFiles { get; set; }
        public DbSet<VttCue> VttCues { get; set; }
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

            // Configure VttFile entity
            modelBuilder.Entity<VttFile>(entity =>
            {
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Header)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("WEBVTT");

                entity.Property(v => v.FileName)
                    .HasMaxLength(255);

                entity.Property(v => v.Notes)
                    .HasMaxLength(1000);

                entity.Property(v => v.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(v => v.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Configure one-to-many relationship
                entity.HasMany(v => v.Cues)
                    .WithOne(c => c.VttFile)
                    .HasForeignKey(c => c.VttFileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Add indexes for performance
                entity.HasIndex(v => v.FileName);
                entity.HasIndex(v => v.CreatedAt);
            });

            // Configure VttCue entity
            modelBuilder.Entity<VttCue>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.CueId)
                    .HasMaxLength(50);

                entity.Property(c => c.Text)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(c => c.Settings)
                    .HasMaxLength(200);

                // Configure TimeSpan properties to be stored as intervals (PostgreSQL native type)
                entity.Property(c => c.StartTime)
                    .HasColumnType("interval")
                    .IsRequired();

                entity.Property(c => c.EndTime)
                    .HasColumnType("interval")
                    .IsRequired();

                // Add indexes for performance
                entity.HasIndex(c => c.VttFileId);
                entity.HasIndex(c => new { c.VttFileId, c.StartTime });
                entity.HasIndex(c => c.StartTime);
                entity.HasIndex(c => c.EndTime);
            });
        }
    }
}
