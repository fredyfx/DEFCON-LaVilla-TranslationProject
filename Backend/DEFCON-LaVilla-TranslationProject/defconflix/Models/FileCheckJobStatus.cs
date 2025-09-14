namespace defconflix.Models
{
    public class FileCheckJobStatus
    {
        public string JobId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? StartedByUserId { get; set; }
        public string Status { get; set; } = "Queued"; // Queued, Running, Completed, Failed, Cancelled
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int AvailableFiles { get; set; }
        public int UnavailableFiles { get; set; }
        public string? ErrorMessage { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
        public bool IsCompleted => Status is "Completed" or "Failed" or "Cancelled";
        public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : DateTime.UtcNow - StartedAt;
    }
}
