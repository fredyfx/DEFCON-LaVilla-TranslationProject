using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using defconflix.WebAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text;

namespace defconflix.Endpoints
{
    public class FilesEndpoint : IEndpoint
    {
        public record BulkDownloadRequest(long[] Ids);
        public record FileDTO(long Id, string FileName, string conference, string Status);
        public record ConferenceDto(string name);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {

            async Task<IResult> GetFilesByType(ApiContext db, string type, int page = 1, int pageSize = 10)
            {
                // Validation for type parameter
                var fileTypeRequested = type.ToLower();

                // type should be either mp4 or pdf or srt or txt:
                if (fileTypeRequested != "mp4" && fileTypeRequested != "pdf" && fileTypeRequested != "srt" && fileTypeRequested != "txt")
                {
                    return Results.BadRequest("Invalid file type requested. Only 'mp4', 'pdf', 'srt' and 'txt' are supported.");
                }

                // Validate pagination parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

                Expression<Func<Files, bool>> filter = f =>
                f.LastCheckAccessible == true &&
                f.Extension.ToLower() == $".{fileTypeRequested}" &&
                !string.IsNullOrEmpty(f.Conference);

                var totalFiles = await db.Files.CountAsync(filter);
                var totalPages = (int)Math.Ceiling((double)totalFiles / pageSize);

                var files = await db.Files
                    .Where(filter)
                    .OrderBy(u => u.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new FileDTO(u.Id, u.File_Name, u.Conference, u.Status))
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
            }

            async Task<IResult> GetFileById(ApiContext db, int id)
            {
                var file = await db.Files
                    .Where(f => f.Id == id)
                    .Select(u => u.File_Path)
                    .SingleOrDefaultAsync();

                return Results.Json(new
                {
                    File = file
                });
            }

            async Task<IResult> GetSearchExactFileName(ApiContext db, string filename)
            {
                if (string.IsNullOrWhiteSpace(filename))
                {
                    return Results.BadRequest("Search file name cannot be empty");
                }

                var file = await db.Files
                    .Where(f => f.File_Name == filename)
                    .Select(u => new FileDTO(u.Id, u.File_Name, u.Conference, u.Status))
                    .SingleOrDefaultAsync();

                if (file == null)
                {
                    return Results.NotFound($"File Name: '{filename}' not found");
                }

                return Results.Json(new
                {
                    File = file
                });
            }

            async Task<IResult> GetSearchFilesByTypeConferenceAndTerm(IFilesService _filesService, string fileTypeRequested, string conference, string term, int page = 1, int pageSize = 10)
            {
                var result = await _filesService.SearchFilesByTypeConferenceAndTerm(
                    fileTypeRequested,
                    conference,
                    term,
                    page,
                    pageSize);

                return Results.Json(new
                {
                    Conference = conference,
                    FileType = fileTypeRequested,
                    SearchTerm = term,
                    Files = result.Files,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = result.Pagination.TotalPages,
                        TotalFiles = result.Pagination.TotalFiles,
                        HasPreviousPage = page > 1,
                        HasNextPage = result.Pagination.HasNextPage
                    }
                });
            }

            async Task<IResult> GetLargeFilesLocationsToDownload(ApiContext db, BulkDownloadRequest request)
            {
                // Validate input
                if (request.Ids == null || request.Ids.Length == 0)
                {
                    return Results.BadRequest("No IDs provided.");
                }

                // Limit the number of Ids to prevent abuse
                if (request.Ids.Length > 100)
                {
                    return Results.BadRequest("Maximum 100 Ids allowed per request.");
                }

                try
                {
                    // Get all files matching the provided Ids
                    var files = await db.Files
                        .Where(f => request.Ids.Contains(f.Id))
                        .Select(f => new { f.Id, f.File_Path })
                        .ToListAsync();

                    if (!files.Any())
                    {
                        return Results.NotFound("No files found for the provided ids.");
                    }

                    // Generate URLs and create the text content
                    var contentBuilder = new StringBuilder();

                    // Process the IDs in the order they were provided
                    foreach (var currentId in request.Ids)
                    {
                        var file = files.FirstOrDefault(f => f.Id == currentId);
                        if (file != null)
                        {
                            var url = file.File_Path;
                            var content = $"{currentId} {url}";
                            contentBuilder.AppendLine(content);
                        }
                    }

                    var textContent = contentBuilder.ToString();
                    var fileBytes = Encoding.UTF8.GetBytes(textContent);

                    // Return as downloadable text file
                    return Results.File(
                        fileBytes,
                        contentType: "text/plain",
                        fileDownloadName: $"infocon_urls_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"
                    );
                }
                catch (Exception ex)
                {
                    // Log the exception if you have logging configured
                    return Results.Problem("An error occurred while processing the request.");
                }
            }

            async Task<IResult> GetSmallFilesLocationsToDownload(ApiContext db, string ids)
            {
                // Parse comma-separated Ids from query parameter
                if (string.IsNullOrEmpty(ids))
                {
                    return Results.BadRequest("No Id provided.");
                }

                var idsArray = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(h => long.Parse(h.Trim()))
                                     .ToArray();

                if (idsArray.Length == 0)
                {
                    return Results.BadRequest("No valid Ids provided.");
                }

                if (idsArray.Length > 20) // Lower limit for GET requests
                {
                    return Results.BadRequest("Maximum 20 ids allowed for GET request. Use POST for larger requests.");
                }

                try
                {
                    // Get all files matching the provided Ids
                    var files = await db.Files
                        .Where(f => idsArray.Contains(f.Id) && f.LastCheckAccessible == true)
                        .Select(f => new { f.Id, f.File_Path })
                        .ToListAsync();

                    if (!files.Any())
                    {
                        return Results.NotFound("No files found for the provided ids.");
                    }

                    // Generate URLs and create the text content
                    var contentBuilder = new StringBuilder();

                    // Process ids in the order they were provided
                    foreach (var currentId in idsArray)
                    {
                        var file = files.SingleOrDefault(f => f.Id == currentId);
                        if (file != null)
                        {
                            var url = file.File_Path;
                            var content = $"{currentId} {url}";
                            contentBuilder.AppendLine(content);
                        }
                    }

                    var textContent = contentBuilder.ToString();
                    var fileBytes = Encoding.UTF8.GetBytes(textContent);

                    // Return as downloadable text file
                    return Results.File(
                        fileBytes,
                        contentType: "text/plain",
                        fileDownloadName: $"infocon_urls_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"
                    );
                }
                catch (Exception ex)
                {
                    // Log the exception if you have logging configured
                    return Results.Problem("An error occurred while processing the request.");
                }
            }

            //api/files/txt?page=5&pagesize=20
            app.MapGet("/api/files/{type}", GetFilesByType)
                .RequireAuthorization("ApiAccess");

            // api/file/74234
            app.MapGet("/api/file/{id}", GetFileById)
                .RequireAuthorization("ApiAccess");

            // api/file/search/Dirk-jan Mollema - Advanced Active Directory to Entra ID lateral movement techniques-demo sharepoint actor.mp4
            app.MapGet("/api/file/search/{filename}", GetSearchExactFileName)
                .RequireAuthorization("ApiAccess");

            // api/files/mp4/conference/all/search/Launching Shells?page=1&pagesize=20
            app.MapGet("/api/files/{fileTypeRequested}/conference/{conference}/search/{term}", GetSearchFilesByTypeConferenceAndTerm);
            //.RequireAuthorization("ApiAccess");

            // api/files/download?ids=1,2,3,4...
            app.MapGet("/api/files/download", GetSmallFilesLocationsToDownload);
            //.RequireAuthorization("ApiAccess");

            app.MapPost("/api/files/download", GetLargeFilesLocationsToDownload);
                //.RequireAuthorization("ApiAccess");
        }
    }
}
