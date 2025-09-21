using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace defconflix.Services
{
    public class WebCrawlerService : IWebCrawlerService
    {
        private readonly ApiContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebCrawlerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public WebCrawlerService(
            ApiContext context, 
            IHttpClientFactory httpClientFactory, 
            ILogger<WebCrawlerService> logger, 
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<List<CrawlerJob>> GetAllJobsAsync()
        {
            return await _context.CrawlerJobs
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }

        public async Task<CrawlerJob?> GetCrawlerJobAsync(int jobId)
        {
            return await _context.CrawlerJobs.FindAsync(jobId);
        }

        public async Task<int> StartCrawlAsync(string baseUrl)
        {
            var job = new CrawlerJob
            {
                StartUrl = baseUrl,
                Status = "Running",
                StartTime = DateTime.UtcNow
            };

            _context.CrawlerJobs.Add(job);
            await _context.SaveChangesAsync();

            // Start background crawling
            _ = Task.Run(() => CrawlAsync(job.Id, baseUrl));

            return job.Id;
        }

        private async Task CrawlAsync(int jobId, string baseUrl)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiContext>();
            var httpClient = _httpClientFactory.CreateClient();

            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "DefconFlix-Crawler/1.0 (La Villa Hacker)");

            var job = await context.CrawlerJobs.FindAsync(jobId);
            if (job == null) return;

            try
            {
                var visitedUrls = new HashSet<string>();
                var urlsToVisit = new Queue<string>();
                urlsToVisit.Enqueue(baseUrl);
                int filesFound = 0;
                while (urlsToVisit.Count > 0)
                {
                    var currentUrl = urlsToVisit.Dequeue();

                    if (visitedUrls.Contains(currentUrl))
                        continue;

                    visitedUrls.Add(currentUrl);
                    _logger.LogInformation($"Crawling: {currentUrl}");

                    try
                    {
                        // Check if this is a file link first
                        if (IsFileLink(currentUrl))
                        {
                            await ProcessFileAsync(context, httpClient, currentUrl, job);
                            continue;
                        }

                        // For directories, only get the HTML listing
                        using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning($"Failed to fetch {currentUrl}: {response.StatusCode}");
                            continue;
                        }

                        // Check content type - only process HTML
                        var contentType = response.Content.Headers.ContentType?.MediaType?.ToLower();
                        if (contentType == null || !contentType.Contains("text/html"))
                        {
                            _logger.LogInformation($"Skipping non-HTML content: {currentUrl}");
                            continue;
                        }

                        // Read only a reasonable amount for directory listings
                        using var stream = await response.Content.ReadAsStreamAsync();
                        using var reader = new System.IO.StreamReader(stream);
                        var content = await reader.ReadToEndAsync();

                        var links = ExtractLinks(content, currentUrl);

                        foreach (var link in links)
                        {
                            if (visitedUrls.Contains(link))
                                continue;

                            if (IsFileLink(link))
                            {
                                visitedUrls.Add(link);
                                await ProcessFileAsync(context, httpClient, link, job);
                                filesFound++;
                            }
                            else if (IsDirectoryLink(link, baseUrl))
                            {
                                urlsToVisit.Enqueue(link);
                            }
                        }

                        // Update job progress every 10 pages
                        if (visitedUrls.Count % 10 == 0)
                        {
                            job.FilesFound = filesFound;
                            job.FilesProcessed = job.FilesFound;
                            await context.SaveChangesAsync();
                        }

                        // Add delay to be respectful to the server
                        await Task.Delay(500);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError($"HTTP error crawling {currentUrl}: {ex.Message}");
                    }
                    catch (TaskCanceledException ex)
                    {
                        _logger.LogError($"Timeout crawling {currentUrl}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error crawling {currentUrl}: {ex.Message}");
                    }
                }

                job.Status = "Completed";
                job.EndTime = DateTime.UtcNow;
                job.FilesFound = filesFound;
                job.FilesProcessed = job.FilesFound;
                await context.SaveChangesAsync();

                _logger.LogInformation($"Crawling completed. Found {job.FilesProcessed} files.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Crawling failed: {ex.Message}");
                job.Status = "Failed";
                job.ErrorMessage = ex.Message;
                job.EndTime = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }

        private async Task ProcessFileAsync(ApiContext context, HttpClient httpClient, string fileUrl, CrawlerJob job)
        {
            try
            {
                var uri = new Uri(fileUrl);
                var fileName = Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath));
                var extension = Path.GetExtension(fileName);

                if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(extension))
                    return;

                // Check if file already exists in database
                var existingFile = await context.Files
                    .FirstOrDefaultAsync(f => f.File_Path == fileUrl);

                if (existingFile != null)
                {
                    _logger.LogDebug($"File already cataloged: {fileName}");
                    return;
                }

                // Use HEAD request to verify file exists and get size (NO DOWNLOAD)
                long fileSize = 0;
                bool fileExists = false;

                try
                {
                    using var headRequest = new HttpRequestMessage(HttpMethod.Head, fileUrl);
                    using var headResponse = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

                    if (headResponse.IsSuccessStatusCode)
                    {
                        fileExists = true;
                        fileSize = headResponse.Content.Headers.ContentLength ?? 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not verify file {fileUrl}: {ex.Message}");
                    // Still catalog the file even if HEAD request fails
                    fileExists = true;
                }

                if (!fileExists)
                {
                    _logger.LogWarning($"File not accessible: {fileUrl}");
                    return;
                }

                // Calculate hash from URL for uniqueness
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(fileUrl)));

                var file = new Files
                {
                    File_Path = fileUrl,
                    File_Name = fileName,
                    Extension = extension.ToLower(),
                    Size_Bytes = fileSize > int.MaxValue ? int.MaxValue : (int)fileSize,
                    Hash = hash,
                    LastCheckAccessible = true,
                    LastCheckedAt = DateTime.UtcNow,                    
                    Status = "Not started",
                    Created_At = DateTime.UtcNow,
                    ProcessedBy = 0
                };

                context.Files.Add(file);
                await context.SaveChangesAsync();

                var sizeFormatted = fileSize > 0 ? $"{fileSize / (1024.0 * 1024.0):F2} MB" : "unknown size";
                _logger.LogInformation($"Cataloged: {fileName} ({extension}) - {sizeFormatted}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cataloging file {fileUrl}: {ex.Message}");
            }
        }

        private List<string> ExtractLinks(string html, string baseUrl)
        {
            var links = new List<string>();
            var baseUri = new Uri(baseUrl);

            // Extract href attributes from anchor tags
            var linkPattern = @"<a\s+[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>";
            var matches = Regex.Matches(html, linkPattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var href = match.Groups[1].Value;

                try
                {
                    Uri absoluteUri;
                    if (Uri.TryCreate(baseUri, href, out absoluteUri))
                    {
                        links.Add(absoluteUri.ToString());
                    }
                }
                catch
                {
                    // Skip invalid URLs
                }
            }

            return links.Distinct().ToList();
        }

        private bool IsFileLink(string url)
        {
            var fileExtensions = new[] {
                ".mp4", ".pdf", ".srt", ".txt", ".avi", ".mkv", ".mov",
                ".zip", ".tar", ".gz", ".rar", ".7z", ".bz2", ".xz",
                ".mp3", ".flac", ".wav", ".ogg", ".m4a",
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
                ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
                ".iso", ".dmg", ".exe", ".sh", ".msi", ".deb", ".rpm"
            };

            try
            {
                var uri = new Uri(url);
                var path = uri.LocalPath.ToLower();
                return fileExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private bool IsDirectoryLink(string url, string baseUrl)
        {
            return url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase) &&
                   !IsFileLink(url) &&
                   !url.Contains("?") &&
                   !url.Contains("#");
        }
    }
}
