namespace defconflix.Models
{
    /// <summary>
    /// Configuration options for background job processing
    /// </summary>
    public class BackgroundJobOptions
    {
        public const string SectionName = "BackgroundJobs";

        /// <summary>
        /// Number of files to process per batch in file check jobs
        /// </summary>
        public int BatchSize { get; set; } = 5;

        /// <summary>
        /// Number of concurrent workers processing the background queue
        /// </summary>
        public int MaxConcurrentWorkers { get; set; } = 2;

        /// <summary>
        /// Delay between batches to be respectful to external servers
        /// </summary>
        public TimeSpan DelayBetweenBatches { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Delay between requests in crawler
        /// </summary>
        public TimeSpan CrawlerRequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// How long to keep completed jobs in memory before cleanup
        /// </summary>
        public TimeSpan JobRetentionPeriod { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// How often the cleanup service runs
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// HTTP timeout for file check and crawler requests
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum queue size (0 = unlimited)
        /// </summary>
        public int MaxQueueSize { get; set; } = 1000;
    }
}
