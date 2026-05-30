namespace defconflix.Models
{
    /// <summary>
    /// Unified status enum for all background jobs (file checks, crawler, etc.)
    /// </summary>
    public enum JobStatus
    {
        /// <summary>Job created but not yet started (for crawler: "Not Started")</summary>
        Pending,

        /// <summary>Job is in the queue waiting to be processed</summary>
        Queued,

        /// <summary>Job is currently being executed</summary>
        Running,

        /// <summary>Job completed successfully</summary>
        Completed,

        /// <summary>Job failed with an error</summary>
        Failed,

        /// <summary>Job was cancelled by user or system</summary>
        Cancelled
    }

    public static class JobStatusExtensions
    {
        public static bool IsTerminal(this JobStatus status) =>
            status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;

        public static bool IsActive(this JobStatus status) =>
            status is JobStatus.Pending or JobStatus.Queued or JobStatus.Running;

        public static bool CanBeCancelled(this JobStatus status) =>
            status is JobStatus.Pending or JobStatus.Queued or JobStatus.Running;
    }
}
