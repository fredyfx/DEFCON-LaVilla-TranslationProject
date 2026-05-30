using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IOnDemandFileCheckService
    {
        Task<string> StartFileCheckJobAsync(long[] fileIds, int? userId = null);
        Task<string> StartAllFilesCheckJobAsync(int? userId = null);
        Task<string> StartFilesNeedingCheckJobAsync(int? userId = null);
        Task<FileCheckJobStatus?> GetJobStatusAsync(string jobId);
        Task<List<FileCheckJobStatus>> GetActiveJobsAsync();
        Task<List<FileCheckJobStatus>> GetAllJobsAsync();
        Task<bool> CancelJobAsync(string jobId);

        /// <summary>
        /// Removes completed jobs older than the specified retention period.
        /// Returns the number of jobs cleaned up.
        /// </summary>
        Task<int> CleanupCompletedJobsAsync(TimeSpan retentionPeriod);
    }
}
