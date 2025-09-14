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
        Task<bool> CancelJobAsync(string jobId);
    }

}
