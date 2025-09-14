using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading;

namespace defconflix.Services
{
    public class FileCheckerService : IFileCheckerService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<FileCheckerService> _logger;

        public FileCheckerService(IServiceProvider serviceProvider, HttpClient httpClient, ILogger<FileCheckerService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClient = httpClient;
            _logger = logger;

            // Configure HttpClient for HEAD requests with reasonable timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<FileStatusCheck>> CheckAllFilesAsync(int? checkedByUserId = null)
        {
            // Get all file IDs in a separate scope to avoid holding the context too long
            long[] allFileIds;
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApiContext>();
                allFileIds = await context.Files
                    .Select(f => f.Id)
                    .ToArrayAsync();
            }

            return await CheckMultipleFilesAsync(allFileIds, checkedByUserId);
        }

        public async Task<FileStatusCheck> CheckFileAvailabilityAsync(long fileId, int? checkedByUserId = null)
        {
            // Create a new scope for this operation to avoid DbContext threading issues
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiContext>();

            var file = await context.Files.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null)
                throw new ArgumentException($"File with ID {fileId} not found", nameof(fileId));

            var stopwatch = Stopwatch.StartNew();
            var statusCheck = new FileStatusCheck
            {
                FileId = fileId,
                CheckedAt = DateTime.UtcNow,
                CheckedByUserId = checkedByUserId
            };

            try
            {
                var url = file.HttpUrl;
                _logger.LogInformation("Checking file availability: {Url}", url);

                // Use HEAD request to check if file exists without downloading it
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _httpClient.SendAsync(request);

                stopwatch.Stop();
                statusCheck.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                statusCheck.HttpStatusCode = (int)response.StatusCode;
                statusCheck.IsAccessible = response.IsSuccessStatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    statusCheck.ErrorMessage = $"{response.StatusCode}: {response.ReasonPhrase}";
                    _logger.LogWarning("File not accessible: {Url} - Status: {StatusCode}", url, response.StatusCode);
                }
                else
                {
                    _logger.LogInformation("File accessible: {Url} - Response time: {ResponseTime}ms", url, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                statusCheck.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                statusCheck.HttpStatusCode = 0; // Network error
                statusCheck.IsAccessible = false;
                statusCheck.ErrorMessage = ex.Message;
                _logger.LogError(ex, "HTTP error checking file {FileId}: {Error}", fileId, ex.Message);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                stopwatch.Stop();
                statusCheck.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                statusCheck.HttpStatusCode = 408; // Request Timeout
                statusCheck.IsAccessible = false;
                statusCheck.ErrorMessage = "Request timeout";
                _logger.LogWarning("Timeout checking file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                statusCheck.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                statusCheck.HttpStatusCode = 0;
                statusCheck.IsAccessible = false;
                statusCheck.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Unexpected error checking file {FileId}: {Error}", fileId, ex.Message);
            }

            // Save the status check
            context.FileStatusChecks.Add(statusCheck);

            // Update the file's last check information
            file.LastCheckAccessible = statusCheck.IsAccessible;
            file.LastCheckedAt = statusCheck.CheckedAt;

            await context.SaveChangesAsync();

            return statusCheck;
        }

        public async Task<List<FileStatusCheck>> CheckMultipleFilesAsync(long[] fileIds, int? checkedByUserId = null)
        {
            var results = new List<FileStatusCheck>();

            // Process files in batches to avoid overwhelming the server
            const int batchSize = 5;
            for (int i = 0; i < fileIds.Length; i += batchSize)
            {
                var batch = fileIds.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(id => CheckFileAvailabilityAsync(id, checkedByUserId));
                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);

                // Add delay between batches to be respectful to the server
                if (i + batchSize < fileIds.Length)
                {
                    await Task.Delay(1000); // 1 second delay between batches
                }
            }

            return results;
        }

        public async Task<List<Files>> GetFilesNeedingCheckAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiContext>();

            var oneDayAgo = DateTime.UtcNow.AddDays(-1);
            return await context.Files
                .Where(f => f.LastCheckedAt == null || f.LastCheckedAt < oneDayAgo)
                .OrderBy(f => f.LastCheckedAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<FileStatusCheck?> GetLatestStatusCheckAsync(long fileId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiContext>();

            return await context.FileStatusChecks
                .Where(s => s.FileId == fileId)
                .OrderByDescending(s => s.CheckedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<FileStatusCheck>> GetStatusHistoryAsync(long fileId, int limit = 10)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiContext>();

            return await context.FileStatusChecks
                .Where(s => s.FileId == fileId)
                .OrderByDescending(s => s.CheckedAt)
                .Take(limit)
                .Include(s => s.CheckedByUser)
                .ToListAsync();
        }

        public async Task<List<Files>> GetUnavailableFilesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiContext>();

            return await context.Files
                .Where(f => f.LastCheckAccessible == false)
                .OrderBy(f => f.LastCheckedAt)
                .ToListAsync();
        }
    }
}
