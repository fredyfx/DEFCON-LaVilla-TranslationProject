using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text;

namespace defconflix.Endpoints
{
    public class FilesEndpoint : IEndpoint
    {
        public record BulkDownloadRequest(string[] Hashes);
        public record FileDTO(int Id, string FileName, string Hash, string Status);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            //api/files/txt?page=5&pagesize=20
            app.MapGet("/api/files/{type}", async (ApiContext db, string type, int page = 1, int pageSize = 10) =>
            {
                // Validation for type parameter
                var fileTypeRequested = type.ToLower();

                // type should be either mp4 or pdf or srt or txt:
                if (fileTypeRequested != "mp4" && fileTypeRequested != "pdf" && fileTypeRequested != "srt" && fileTypeRequested != "txt")
                {
                    return Results.BadRequest("Invalid file type requested. Only 'mp4' and 'pdf' are supported.");
                }

                // Validate pagination parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

                Expression<Func<Files, bool>> filterFiles = f => true;
                Expression<Func<Files, bool>> filterVideos = f => f.Extension.ToLower() == ".mp4" && (f.File_Path.Contains("presentation") || f.File_Path.Contains("video"));
                Expression<Func<Files, bool>> filterPdfs = f => f.Extension.ToLower() == ".pdf" && (f.File_Path.Contains("presentation") || f.File_Path.Contains("video"));
                Expression<Func<Files, bool>> filterSRTs = f => f.Extension.ToLower() == ".srt" && (f.File_Path.Contains("presentation") || f.File_Path.Contains("video"));
                Expression<Func<Files, bool>> filterTxts = f => f.Extension.ToLower() == ".txt" && (f.File_Path.Contains("presentation") || f.File_Path.Contains("video"));

                // Apply the appropriate filter based on the requested file type using switch expression
                var filter = type.ToLower() switch
                {
                    "mp4" => filterVideos,
                    "pdf" => filterPdfs,
                    "srt" => filterSRTs,
                    "txt" => filterTxts,
                    _ => filterFiles // Default case, should not be hit due to earlier validation
                };

                var totalFiles = await db.Files.CountAsync(filter);
                var totalPages = (int)Math.Ceiling((double)totalFiles / pageSize);


                var files = await db.Files
                    .Where(filter)
                    .OrderBy(u => u.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new FileDTO(u.Id, u.File_Name, u.Hash, u.Status))
                    .ToListAsync();

                return Results.Json(new
                {
                    Files = files,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = totalPages,
                        TotalFiles = totalFiles,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages
                    }
                });
            });

            app.MapGet("/api/file/{hash}", async (ApiContext db, string hash) =>
            {
                var file = await db.Files
                    .Where(f => f.Hash == hash)
                    .Select(u => GetFTPLocation(u.File_Path))
                    .SingleOrDefaultAsync();

                return Results.Json(new
                {
                    File = file
                });
            });

            app.MapPost("/api/files/download", async (ApiContext db, BulkDownloadRequest request) =>
            {
                // Validate input
                if (request.Hashes == null || request.Hashes.Length == 0)
                {
                    return Results.BadRequest("No hashes provided.");
                }

                // Limit the number of hashes to prevent abuse
                if (request.Hashes.Length > 100)
                {
                    return Results.BadRequest("Maximum 100 hashes allowed per request.");
                }

                try
                {
                    // Get all files matching the provided hashes
                    var files = await db.Files
                        .Where(f => request.Hashes.Contains(f.Hash))
                        .Select(f => new { f.Hash, f.File_Path })
                        .ToListAsync();

                    if (!files.Any())
                    {
                        return Results.NotFound("No files found for the provided hashes.");
                    }

                    // Generate URLs and create the text content
                    var urlBuilder = new StringBuilder();

                    // Process hashes in the order they were provided
                    foreach (var hash in request.Hashes)
                    {
                        var file = files.FirstOrDefault(f => f.Hash == hash);
                        if (file != null)
                        {
                            var url = GetFTPLocation(file.File_Path);
                            urlBuilder.AppendLine(url);
                        }
                    }

                    var textContent = urlBuilder.ToString();
                    var fileBytes = Encoding.UTF8.GetBytes(textContent);

                    // Return as downloadable text file
                    return Results.File(
                        fileBytes,
                        contentType: "text/plain",
                        fileDownloadName: $"defcon_urls_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"
                    );
                }
                catch (Exception ex)
                {
                    // Log the exception if you have logging configured
                    return Results.Problem("An error occurred while processing the request.");
                }
            });

            app.MapGet("/api/files/download", async (ApiContext db, string hashes) =>
            {
                // Parse comma-separated hashes from query parameter
                if (string.IsNullOrEmpty(hashes))
                {
                    return Results.BadRequest("No hashes provided.");
                }

                var hashArray = hashes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(h => h.Trim())
                                     .ToArray();

                if (hashArray.Length == 0)
                {
                    return Results.BadRequest("No valid hashes provided.");
                }

                if (hashArray.Length > 20) // Lower limit for GET requests
                {
                    return Results.BadRequest("Maximum 20 hashes allowed for GET request. Use POST for larger requests.");
                }

                try
                {
                    // Get all files matching the provided hashes
                    var files = await db.Files
                        .Where(f => hashArray.Contains(f.Hash))
                        .Select(f => new { f.Hash, f.File_Path })
                        .ToListAsync();

                    if (!files.Any())
                    {
                        return Results.NotFound("No files found for the provided hashes.");
                    }

                    // Generate URLs and create the text content
                    var urlBuilder = new StringBuilder();

                    // Process hashes in the order they were provided
                    foreach (var hash in hashArray)
                    {
                        var file = files.FirstOrDefault(f => f.Hash == hash);
                        if (file != null)
                        {
                            var url = GetFTPLocation(file.File_Path);
                            urlBuilder.AppendLine(url);
                        }
                    }

                    var textContent = urlBuilder.ToString();
                    var fileBytes = Encoding.UTF8.GetBytes(textContent);

                    // Return as downloadable text file
                    return Results.File(
                        fileBytes,
                        contentType: "text/plain",
                        fileDownloadName: $"defcon_urls_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"
                    );
                }
                catch (Exception ex)
                {
                    // Log the exception if you have logging configured
                    return Results.Problem("An error occurred while processing the request.");
                }
            });
        }

        static string GetFTPLocation(string filePath)
        {
            // From:
            // \\SERVER\usbshare1-1\cons\DEF CON\DEF CON 27\voting-village-report-defcon27.pdf
            // Target:
            // cons\\DEF CON\\DEF CON 27\\DEF CON 27 presentations\\DEFCON-27-albinowax-HTTP-Desync-Attacks-demo.mp4
            // https://media.defcon.org/DEF%20CON%2027//DEF%20CON%2027%20presentations//DEFCON-27-albinowax-HTTP-Desync-Attacks-demo.mp4

            var removePrefixUntil = filePath.IndexOf("cons");
            filePath = filePath.Remove(0, removePrefixUntil);

            // Remove the "cons\\" prefix if it exists
            string cleanPath = filePath;
            if (cleanPath.StartsWith("cons\\", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = cleanPath.Substring(5); // Remove "cons\\"
            }

            if (cleanPath.StartsWith("DEF CON", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = cleanPath.Substring(7); // Remove "DEF CON"
            }

            // Replace backslashes with forward slashes
            cleanPath = cleanPath.Replace('\\', '/');

            // URL encode the path components while preserving forward slashes
            string[] pathParts = cleanPath.Split('/');
            for (int i = 0; i < pathParts.Length; i++)
            {
                pathParts[i] = Uri.EscapeDataString(pathParts[i]);
            }

            // Reconstruct the path with forward slashes
            string urlEncodedPath = string.Join("/", pathParts);

            // Construct the full FTP URL
            string ftpUrl = "https://media.defcon.org." + urlEncodedPath;

            return ftpUrl;
        }
    }
}
