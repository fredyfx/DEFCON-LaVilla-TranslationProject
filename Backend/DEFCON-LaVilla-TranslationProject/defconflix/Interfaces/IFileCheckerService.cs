using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IFileCheckerService
    {
        Task<FileStatusCheck> CheckFileAvailabilityAsync(long fileId, int? checkedByUserId = null);
        Task<List<FileStatusCheck>> CheckMultipleFilesAsync(long[] fileIds, int? checkedByUserId = null);
        Task<List<FileStatusCheck>> CheckAllFilesAsync(int? checkedByUserId = null);
        Task<List<Files>> GetUnavailableFilesAsync();
        Task<List<Files>> GetFilesNeedingCheckAsync();
        Task<FileStatusCheck?> GetLatestStatusCheckAsync(long fileId);
        Task<List<FileStatusCheck>> GetStatusHistoryAsync(long fileId, int limit = 10);
    }
}
