using defconflix.Data;
using defconflix.Extensions;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace defconflix.Services
{
    public class WebCrawlerService : IWebCrawlerService
    {
        private readonly ApiContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebCrawlerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ICrawlerCancellationService _cancellationService;

        public WebCrawlerService(
            ApiContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<WebCrawlerService> logger,
            IServiceScopeFactory scopeFactory,
            ICrawlerCancellationService cancellationService)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _cancellationService = cancellationService;
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

        public async Task<int> StartCrawlAsync(string baseUrl, int userId)
        {
            var job = new CrawlerJob
            {
                StartUrl = baseUrl,
                Status = "Running",
                StartTime = DateTime.UtcNow,
                StartedByUserId = userId,
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
            var cancellationService = scope.ServiceProvider.GetRequiredService<ICrawlerCancellationService>();
            var httpClient = _httpClientFactory.CreateClient();

            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "DefconFlix-Crawler/1.0 (La Villa Hacker)");

            var job = await context.CrawlerJobs.FindAsync(jobId);
            if (job == null) return;

            // Initialize all counters to zero at start of crawl
            job.FilesFound = 0;
            job.FilesSuccessful = 0;
            job.FilesWithErrors = 0;
            job.FilesProcessed = 0;

            try
            {
                var visitedUrls = new HashSet<string>();
                var urlsToVisit = new Queue<string>();
                urlsToVisit.Enqueue(baseUrl);
                int filesFound = 0;
                while (urlsToVisit.Count > 0)
                {
                    // Check for cancellation request at the start of each iteration
                    if (await cancellationService.IsCancellationRequestedAsync(jobId))
                    {
                        _logger.LogInformation($"Cancellation detected for job {jobId}. Stopping crawl gracefully.");
                        await cancellationService.MarkJobAsCancelledAsync(jobId);
                        return;
                    }

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
                            // Check cancellation again before database update
                            if (await cancellationService.IsCancellationRequestedAsync(jobId))
                            {
                                _logger.LogInformation($"Cancellation detected during progress update for job {jobId}");
                                await cancellationService.MarkJobAsCancelledAsync(jobId);
                                return;
                            }

                            // Update job progress with detailed statistics in database
                            var currentJob = await context.CrawlerJobs.FindAsync(jobId);
                            if (currentJob != null)
                            {
                                // Sync the in-memory counters with database
                                currentJob.FilesFound = job.FilesFound;
                                currentJob.FilesSuccessful = job.FilesSuccessful;
                                currentJob.FilesWithErrors = job.FilesWithErrors;
                                currentJob.FilesProcessed = job.FilesProcessed;
                                await context.SaveChangesAsync();

                                _logger.LogInformation($"Progress Update - Job {jobId}: Found={job.FilesFound}, " +
                                    $"Successful={job.FilesSuccessful}, Errors={job.FilesWithErrors}, " +
                                    $"Success Rate={job.SuccessRate:F1}%");
                            }
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
                job.FilesSuccessful = job.FilesSuccessful;
                job.FilesWithErrors = job.FilesWithErrors;
                job.FilesProcessed = job.FilesProcessed;
                await context.SaveChangesAsync();

                _logger.LogInformation($"Crawling completed for job {jobId}. " +
                    $"Total Found: {job.FilesFound}, Successful: {job.FilesSuccessful}, " +
                    $"Errors: {job.FilesWithErrors}, Success Rate: {job.SuccessRate:F1}%");
                }
            catch (Exception ex)
            {
                _logger.LogError($"Crawling failed: {ex.Message}");
                job.Status = "Failed";
                job.ErrorMessage = ex.Message;
                job.EndTime = DateTime.UtcNow;                
                job.FilesProcessed = job.FilesFound;
                job.FilesSuccessful = job.FilesSuccessful;
                job.FilesWithErrors = job.FilesWithErrors;
                job.FilesProcessed = job.FilesProcessed;
                await context.SaveChangesAsync();
            }
        }

        private async Task ProcessFileAsync(ApiContext context, HttpClient httpClient, string fileUrl, CrawlerJob job)
        {
            bool wasSuccessful = false;

            try
            {
                _logger.LogDebug($"Processing file URL: {fileUrl}");

                var uri = new Uri(fileUrl);
                var rawFileName = Path.GetFileName(uri.LocalPath);
                var fileName = rawFileName.SafeUrlDecode(_logger, "fileName");
                var extension = Path.GetExtension(fileName);

                if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(extension))
                {
                    await RecordProblematicUri(context, fileUrl, rawFileName, extension,
                        "INVALID_FILENAME", "Invalid filename or extension after processing", null, job.Id);
                    return;
                }

                // Check for problematic characters BEFORE sanitization
                var hasProblems = await CheckAndRecordProblematicCharacters(context, fileUrl, fileName, extension, job.Id);

                if (hasProblems)
                {
                    _logger.LogWarning($"File has problematic characters but attempting to process: {fileName}");
                }

                // Sanitize the file URL
                var sanitizedFileUrl = fileUrl.SanitizeForDatabase(_logger, "fileUrl");
                var sanitizedFileName = fileName.SanitizeForDatabase(_logger, "fileName");
                var sanitizedExtension = extension.ToLower().SanitizeForDatabase(_logger, "extension");


                // Check if file already exists in database
                var existingFile = await context.Files
                    .FirstOrDefaultAsync(f => f.File_Path == sanitizedFileUrl);

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
                    System.Text.Encoding.UTF8.GetBytes(sanitizedFileUrl)));

                var file = new Files
                {
                    File_Path = sanitizedFileUrl,
                    File_Name = sanitizedFileName,
                    Extension = sanitizedExtension,
                    Size_Bytes = fileSize > int.MaxValue ? int.MaxValue : (int)fileSize,
                    Hash = hash,
                    LastCheckAccessible = true,
                    LastCheckedAt = DateTime.UtcNow,
                    Status = "Not started",
                    Created_At = DateTime.UtcNow
                };

                context.Files.Add(file);

                try
                {
                    await context.SaveChangesAsync();

                    // Successfully saved
                    wasSuccessful = true;

                    var sizeFormatted = fileSize > 0 ? $"{fileSize / (1024.0 * 1024.0):F2} MB" : "unknown size";
                    _logger.LogInformation($"Cataloged: {fileName} ({extension}) - {sizeFormatted}");
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "22021")
                {
                    _logger.LogError($"PostgreSQL encoding error for file: {sanitizedFileName}");

                    // Record the problematic URI with full details
                    await RecordProblematicUri(context, fileUrl, fileName, extension,
                        "POSTGRESQL_ENCODING_ERROR",
                        $"PostgreSQL encoding error: {fileUrl.AnalyzeProblematicCharacters()}",
                        pgEx.MessageText, job.Id);

                    // Remove the problematic entity from tracking
                    context.Entry(file).State = EntityState.Detached;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unexpected error saving file {sanitizedFileName}: {ex}");

                    await RecordProblematicUri(context, fileUrl, fileName, extension,
                        "DATABASE_SAVE_ERROR",
                        $"Unexpected database error: {ex.Message}",
                        ex.Message, job.Id);

                    context.Entry(file).State = EntityState.Detached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cataloging file {fileUrl}: {ex.Message}");
                await RecordProblematicUri(context, fileUrl, "UNKNOWN", "UNKNOWN",
                    "PROCESSING_ERROR",
                    $"Error during file processing: {ex.Message}",
                    ex.Message, job.Id);
            }
            finally
            {
                // Update counters regardless of success/failure
                lock (job) // Thread-safe counter updates
                {
                    job.FilesFound++;
                    if (wasSuccessful)
                    {
                        job.FilesSuccessful++;
                    }
                    else
                    {
                        job.FilesWithErrors++;
                    }
                    job.FilesProcessed = job.FilesSuccessful + job.FilesWithErrors;
                }
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
                        var sanitizedUrl = absoluteUri.ToString().SanitizeForDatabase();
                        if (!string.IsNullOrEmpty(sanitizedUrl))
                        {
                            links.Add(sanitizedUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Skipping invalid URL: {href} - {ex.Message}");
                }
            }

            return links.Distinct()
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();
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

        private async Task<bool> CheckAndRecordProblematicCharacters(
            ApiContext context,
            string fileUrl,
            string fileName,
            string extension,
            int jobId)
        {
            var problems = new List<string>();

            // Check for null bytes
            if (fileUrl.Contains('\0') || fileName.Contains('\0') || extension.Contains('\0'))
            {
                problems.Add("Contains null bytes");
            }

            // Check for control characters
            if (fileUrl.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') ||
                fileName.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') ||
                extension.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
            {
                problems.Add("Contains control characters");
            }

            // Check for extended ASCII
            if (fileUrl.Any(c => (int)c > 127 && (int)c < 160) ||
                fileName.Any(c => (int)c > 127 && (int)c < 160) ||
                extension.Any(c => (int)c > 127 && (int)c < 160))
            {
                problems.Add("Contains extended ASCII control characters");
            }

            if (problems.Any())
            {
                var errorDetails = $"URL Analysis: {fileUrl.AnalyzeProblematicCharacters()}\n" +
                                  $"FileName Analysis: {fileName.AnalyzeProblematicCharacters()}\n" +
                                  $"Extension Analysis: {extension.AnalyzeProblematicCharacters()}";

                await RecordProblematicUri(context, fileUrl, fileName, extension,
                    "PROBLEMATIC_CHARACTERS",
                    errorDetails,
                    string.Join(", ", problems), jobId);

                return true;
            }

            return false;
        }

        private async Task RecordProblematicUri(
            ApiContext context,
            string originalUri,
            string fileName,
            string extension,
            string errorType,
            string errorDetails,
            string? postgresqlError,
            int jobId)
        {
            try
            {
                // Sanitize the data being saved to avoid recursive issues
                var sanitizedOriginalUri = originalUri.Length > 2000 ? originalUri.Substring(0, 2000) : originalUri;
                var sanitizedFileName = fileName.Length > 500 ? fileName.Substring(0, 500) : fileName;
                var sanitizedExtension = extension?.Length > 50 ? extension.Substring(0, 50) : extension;
                var sanitizedErrorDetails = errorDetails?.Length > 2000 ? errorDetails.Substring(0, 2000) : errorDetails;
                var sanitizedPostgresqlError = postgresqlError?.Length > 1000 ? postgresqlError.Substring(0, 1000) : postgresqlError;

                // Remove null bytes from all fields to prevent the same error
                sanitizedOriginalUri = sanitizedOriginalUri.Replace('\0', '?');
                sanitizedFileName = sanitizedFileName.Replace('\0', '?');
                sanitizedExtension = sanitizedExtension?.Replace('\0', '?');
                sanitizedErrorDetails = sanitizedErrorDetails?.Replace('\0', '?');
                sanitizedPostgresqlError = sanitizedPostgresqlError?.Replace('\0', '?');

                var problematicUri = new ProblematicUri
                {
                    OriginalUri = sanitizedOriginalUri,
                    SanitizedUri = originalUri.SanitizeForDatabase(),
                    FileName = sanitizedFileName,
                    Extension = sanitizedExtension,
                    ErrorType = errorType,
                    ErrorDetails = sanitizedErrorDetails,
                    PostgresqlError = sanitizedPostgresqlError,
                    CrawlerJobId = jobId,
                    CreatedAt = DateTime.UtcNow,
                    IsResolved = false
                };

                context.ProblematicUris.Add(problematicUri);
                await context.SaveChangesAsync();

                _logger.LogWarning($"Recorded problematic URI: {errorType} - {sanitizedFileName} (ID: {problematicUri.Id})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to record problematic URI: {originalUri}. Error: {ex.Message}");
            }
        }
    }
}
