using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IWebCrawlerService
    {
        Task<int> StartCrawlAsync(string baseUrl);
        Task<CrawlerJob?> GetCrawlerJobAsync(int jobId);
        Task<List<CrawlerJob>> GetAllJobsAsync();
    }
}
