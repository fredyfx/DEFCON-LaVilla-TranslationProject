using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace defconflix.Models
{
    public class CrawlerJob
    {
        [Key]
        public int Id { get; set; }
        public string StartUrl { get; set; } = string.Empty;

        /// <summary>
        /// Job status stored as string for backward compatibility with existing data.
        /// Use StatusEnum property for type-safe access.
        /// </summary>
        public string Status { get; set; } = "Pending";

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int FilesFound { get; set; } = 0;
        public int FilesProcessed { get; set; } = 0;
        public int FilesSuccessful { get; set; } = 0;
        public int FilesWithErrors { get; set; } = 0;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.UtcNow - StartTime;

        // User tracking
        [Required]
        public int StartedByUserId { get; set; }

        [ForeignKey("StartedByUserId")]
        public virtual User? StartedByUser { get; set; }

        // Cancellation tracking
        public bool IsCancellationRequested { get; set; } = false;
        public DateTime? CancellationRequestedAt { get; set; }
        public int? CancelledByUserId { get; set; }

        [ForeignKey("CancelledByUserId")]
        public virtual User? CancelledByUser { get; set; }

        public string? CancellationReason { get; set; }

        /// <summary>
        /// Type-safe access to job status. Maps string Status to/from JobStatus enum.
        /// </summary>
        [NotMapped]
        public JobStatus StatusEnum
        {
            get => Status switch
            {
                "Not Started" or "Pending" => JobStatus.Pending,
                "Queued" => JobStatus.Queued,
                "Running" => JobStatus.Running,
                "Completed" => JobStatus.Completed,
                "Failed" => JobStatus.Failed,
                "Cancelled" => JobStatus.Cancelled,
                _ => JobStatus.Pending
            };
            set => Status = value.ToString();
        }

        [NotMapped]
        public bool CanBeCancelled => StatusEnum.CanBeCancelled();

        [NotMapped]
        public bool IsCancelled => StatusEnum == JobStatus.Cancelled;

        [NotMapped]
        public bool IsActive => StatusEnum.IsActive();

        [NotMapped]
        public bool IsTerminal => StatusEnum.IsTerminal();

        [NotMapped]
        public double SuccessRate => FilesFound > 0 ? (double)FilesSuccessful / FilesFound * 100 : 0;

        [NotMapped]
        public double ErrorRate => FilesFound > 0 ? (double)FilesWithErrors / FilesFound * 100 : 0;
    }
}
