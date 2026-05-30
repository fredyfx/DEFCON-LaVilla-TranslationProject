namespace defconflix.Models
{
    public class FileCheckJobStatus
    {
        public string JobId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? StartedByUserId { get; set; }
        public JobStatus Status { get; set; } = JobStatus.Queued;
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int AvailableFiles { get; set; }
        public int UnavailableFiles { get; set; }
        public string? ErrorMessage { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
        public bool IsCompleted => Status.IsTerminal();
        public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : DateTime.UtcNow - StartedAt;

        /// <summary>
        /// Status as string for API responses (backward compatibility)
        /// </summary>
        public string StatusText => Status.ToString();
    }
}
