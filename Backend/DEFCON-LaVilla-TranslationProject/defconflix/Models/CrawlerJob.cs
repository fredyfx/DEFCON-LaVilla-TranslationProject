using System.ComponentModel.DataAnnotations;

namespace defconflix.Models
{
    public class CrawlerJob
    {
        [Key]
        public int Id { get; set; }
        public string StartUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "Not Started"; // Not Started, Running, Completed, Failed
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int FilesFound { get; set; }
        public int FilesProcessed { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.UtcNow - StartTime;
    }
}
