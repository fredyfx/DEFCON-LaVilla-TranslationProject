namespace defconflix.Interfaces
{
    public interface ICrawlerCancellationService
    {
        Task<bool> RequestCancellationAsync(int jobId, int userId, string? reason = null);
        Task<bool> IsCancellationRequestedAsync(int jobId);
        Task MarkJobAsCancelledAsync(int jobId);
        Task<List<int>> GetCancellationRequestedJobsAsync();
    }
}
