using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IWebCrawlerService
    {
        Task<int> StartCrawlAsync(string baseUrl, int userId);
        Task<CrawlerJob?> GetCrawlerJobAsync(int jobId);
        Task<List<CrawlerJob>> GetAllJobsAsync();
    }
}
