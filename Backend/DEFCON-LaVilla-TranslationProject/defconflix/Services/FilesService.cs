using defconflix.Data;
using defconflix.Models;
using defconflix.WebAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text;

namespace defconflix.WebAPI.Services
{
    public class FilesService : IFilesService
    {
        private readonly ApiContext _dbContext;
        private readonly ILogger<FilesService> _logger;

        private readonly HashSet<string> _validFileTypes = new() { "all", "mp4", "pdf", "srt", "txt" };
        private const int MaxBulkDownloadIds = 100;
        private const int MaxQueryDownloadIds = 20;
        private const int MaxPageSize = 100;

        public FilesService(ApiContext dbContext, ILogger<FilesService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<byte[]> GenerateBulkDownloadAsync(long[] ids)
        {
            _logger.LogInformation("Generating bulk download for {IdCount} IDs", ids?.Length ?? 0);

            try
            {
                ValidateBulkDownloadIds(ids);

                var files = await GetFilesByIdsAsync(ids);

                if (!files.Any())
                {
                    _logger.LogWarning("No files found for the provided IDs");
                    throw new InvalidOperationException("No files found for the provided ids.");
                }

                var result = GenerateDownloadContent(files, ids);

                _logger.LogInformation("Successfully generated bulk download for {FoundFiles} files",
                    files.Count);

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Error occurred while generating bulk download");
                throw;
            }
        }

        public async Task<byte[]> GenerateDownloadFromQueryAsync(string idsQuery)
        {
            _logger.LogInformation("Generating download from query: {IdsQuery}", idsQuery);

            try
            {
                var idsArray = ParseIdsQuery(idsQuery);
                ValidateQueryDownloadIds(idsArray);

                var files = await GetFilesByIdsAsync(idsArray);

                if (!files.Any())
                {
                    _logger.LogWarning("No files found for the provided query IDs");
                    throw new InvalidOperationException("No files found for the provided ids.");
                }

                var result = GenerateDownloadContent(files, idsArray);

                _logger.LogInformation("Successfully generated download from query for {FoundFiles}/{RequestedFiles} files",
                    files.Count, idsArray.Length);

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Error occurred while generating download from query");
                throw;
            }
        }

        public async Task<string?> GetFileLocationAsync(int id)
        {
            _logger.LogInformation("Getting file location for ID: {FileId}", id);

            try
            {
                if (id <= 0)
                {
                    _logger.LogWarning("Invalid file ID provided: {FileId}", id);
                    throw new ArgumentException("File ID must be a positive integer.");
                }

                var filePath = await _dbContext.Files
                    .Where(f => f.Id == id)
                    .Select(u => u.File_Path)
                    .SingleOrDefaultAsync();

                if (filePath == null)
                {
                    _logger.LogWarning("File not found with ID: {FileId}", id);
                    return null;
                }

                _logger.LogInformation("Successfully retrieved location for file ID: {FileId}", id);
                return filePath;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _logger.LogError(ex, "Error occurred while getting file location for ID: {FileId}", id);
                throw;
            }
        }

        public async Task<(IEnumerable<FileDTO> Files, PaginationInfo Pagination)> GetFilesByTypeAsync(string fileType, int page = 1, int pageSize = 10)
        {
            _logger.LogInformation("Getting files by type: {FileType}, Page: {Page}, PageSize: {PageSize}",
               fileType, page, pageSize);

            try
            {
                ValidateFileType(fileType);
                ValidatePaginationParameters(ref page, ref pageSize);

                var filter = GetFileFilterByType(fileType.ToLower());

                var totalFiles = await _dbContext.Files.CountAsync(filter);
                var totalPages = CalculateTotalPages(totalFiles, pageSize);

                var files = await GetPaginatedFilesAsync(filter, page, pageSize);

                var pagination = CreatePaginationInfo(page, pageSize, totalPages, totalFiles);

                _logger.LogInformation("Successfully retrieved {FileCount} files of type {FileType}",
                    files.Count(), fileType);

                return (files, pagination);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting files by type: {FileType}", fileType);
                throw;
            }
        }

        public async Task<(IEnumerable<FileDTO> Files, PaginationInfo Pagination)> SearchFilesByTypeConferenceAndTerm(
            string fileTypeRequested,
            string conference,
            string term,
            int page = 1,
            int pageSize = 10)
        {
            try
            {
                ValidateFileType(fileTypeRequested);
                ValidatePaginationParameters(ref page, ref pageSize);

                var filter = GetSearchFilter(fileTypeRequested.ToLower(), conference, term);

                var totalFiles = await _dbContext.Files.CountAsync(filter);
                var totalPages = CalculateTotalPages(totalFiles, pageSize);

                var files = await GetPaginatedFilesAsync(filter, page, pageSize);

                var pagination = CreatePaginationInfo(page, pageSize, totalPages, totalFiles);

                return (files, pagination);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting files by type: {0}, conference: {1}, term: {2}, page: {3}, pageSize: {4}",
                    fileTypeRequested, conference, term, page, pageSize);
                throw;
            }
        }

        public Task<string?> GetFileLocationAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task<FileDTO> GetFileDetailsByExactName(string fileName)
        {
            throw new NotImplementedException();
        }

        private void ValidateFileType(string fileType)
        {
            if (string.IsNullOrWhiteSpace(fileType))
            {
                throw new ArgumentException("File type cannot be null or empty.");
            }

            if (!_validFileTypes.Contains(fileType.ToLower()))
            {
                _logger.LogWarning("Invalid file type requested: {FileType}", fileType);
                throw new ArgumentException($"Invalid file type requested. Only {string.Join(", ", _validFileTypes)} are supported.");
            }
        }

        private static Expression<Func<Files, bool>> GetFileFilterByType(string fileType)
        {
            return fileType switch
            {
                "all" => f => (f.Extension.ToLower() == ".mp4" || f.Extension.ToLower() == ".pdf" || f.Extension.ToLower() == ".srt" || f.Extension.ToLower() == ".txt") &&
                    f.LastCheckAccessible == true && !string.IsNullOrEmpty(f.Conference),
                "mp4" => f => f.Extension.ToLower() == ".mp4" && f.LastCheckAccessible == true && !string.IsNullOrEmpty(f.Conference),
                "pdf" => f => f.Extension.ToLower() == ".pdf" && f.LastCheckAccessible == true && !string.IsNullOrEmpty(f.Conference),
                "srt" => f => f.Extension.ToLower() == ".srt" && f.LastCheckAccessible == true && !string.IsNullOrEmpty(f.Conference),
                "txt" => f => f.Extension.ToLower() == ".txt" && f.LastCheckAccessible == true && !string.IsNullOrEmpty(f.Conference),
                _ => f => true // This should never be reached due to validation
            };
        }

        private static Expression<Func<Files, bool>> GetSearchFilter(string fileType, string conference, string searchTerm)
        {
            return fileType switch
            {
                "all" => f => (f.Extension.ToLower() == ".mp4" || f.Extension.ToLower() == ".pdf" || f.Extension.ToLower() == ".srt" || f.Extension.ToLower() == ".txt") &&
                    f.LastCheckAccessible == true &&
                    !string.IsNullOrEmpty(f.Conference) &&
                    (string.IsNullOrEmpty(conference) || conference.ToLower() == "all" || f.Conference!.ToLower().Contains(conference.ToLower())) &&
                    (string.IsNullOrEmpty(searchTerm) || EF.Functions.ILike(f.File_Name, $"%{searchTerm}%")),
                "mp4" => f => f.Extension.ToLower() == ".mp4" &&
                    f.LastCheckAccessible == true &&
                    !string.IsNullOrEmpty(f.Conference) &&
                    (string.IsNullOrEmpty(conference) || conference.ToLower() == "all" || f.Conference!.ToLower().Contains(conference.ToLower())) &&
                    (string.IsNullOrEmpty(searchTerm) || EF.Functions.ILike(f.File_Name, $"%{searchTerm}%")),
                "pdf" => f => f.Extension.ToLower() == ".pdf" &&
                    f.LastCheckAccessible == true &&
                    !string.IsNullOrEmpty(f.Conference) &&
                    (string.IsNullOrEmpty(conference) || conference.ToLower() == "all" || f.Conference!.ToLower().Contains(conference.ToLower())) &&
                    (string.IsNullOrEmpty(searchTerm) || EF.Functions.ILike(f.File_Name, $"%{searchTerm}%")),
                "srt" => f => f.Extension.ToLower() == ".srt" &&
                    f.LastCheckAccessible == true &&
                    !string.IsNullOrEmpty(f.Conference) &&
                    (string.IsNullOrEmpty(conference) || conference.ToLower() == "all" || f.Conference!.ToLower().Contains(conference.ToLower())) &&
                    (string.IsNullOrEmpty(searchTerm) || EF.Functions.ILike(f.File_Name, $"%{searchTerm}%")),
                "txt" => f => f.Extension.ToLower() == ".txt" &&
                    f.LastCheckAccessible == true &&
                    !string.IsNullOrEmpty(f.Conference) &&
                    (string.IsNullOrEmpty(conference) || conference.ToLower() == "all" || f.Conference!.ToLower().Contains(conference.ToLower())) &&
                    (string.IsNullOrEmpty(searchTerm) || EF.Functions.ILike(f.File_Name, $"%{searchTerm}%")),
                _ => f => true
            };
        }

        private static void ValidatePaginationParameters(ref int page, ref int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        }

        private void ValidateBulkDownloadIds(long[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                _logger.LogWarning("No IDs provided for bulk download");
                throw new ArgumentException("No IDs provided.");
            }

            if (ids.Length > MaxBulkDownloadIds)
            {
                _logger.LogWarning("Too many IDs provided for bulk download: {IdCount}", ids.Length);
                throw new ArgumentException($"Maximum {MaxBulkDownloadIds} Ids allowed per request.");
            }

            if (ids.Any(id => id <= 0))
            {
                _logger.LogWarning("Invalid IDs provided for bulk download");
                throw new ArgumentException("All IDs must be positive numbers.");
            }
        }

        private void ValidateQueryDownloadIds(long[] idsArray)
        {
            if (idsArray.Length == 0)
            {
                _logger.LogWarning("No valid IDs provided in query");
                throw new ArgumentException("No valid Ids provided.");
            }

            if (idsArray.Length > MaxQueryDownloadIds)
            {
                _logger.LogWarning("Too many IDs provided in GET request: {IdCount}", idsArray.Length);
                throw new ArgumentException($"Maximum {MaxQueryDownloadIds} ids allowed for GET request. Use POST for larger requests.");
            }
        }

        private long[] ParseIdsQuery(string idsQuery)
        {
            if (string.IsNullOrWhiteSpace(idsQuery))
            {
                _logger.LogWarning("No ID provided in query");
                throw new ArgumentException("No Id provided.");
            }

            try
            {
                return idsQuery.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(h => long.Parse(h.Trim()))
                             .Where(id => id > 0)
                             .ToArray();
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Invalid ID format in query: {IdsQuery}", idsQuery);
                throw new ArgumentException("Invalid ID format in query. All IDs must be positive integers.");
            }
        }

        private static int CalculateTotalPages(int totalFiles, int pageSize)
        {
            return (int)Math.Ceiling((double)totalFiles / pageSize);
        }

        private async Task<IEnumerable<FileDTO>> GetPaginatedFilesAsync(
            Expression<Func<Files, bool>> filter,
            int page,
            int pageSize)
        {
            return await _dbContext.Files
                .Where(filter)
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new FileDTO(u.Id, u.File_Name, u.Conference!, u.Status))
                .ToListAsync();
        }

        private static PaginationInfo CreatePaginationInfo(int page, int pageSize, int totalPages, int totalFiles)
        {
            return new PaginationInfo(
                CurrentPage: page,
                PageSize: pageSize,
                TotalPages: totalPages,
                TotalFiles: totalFiles,
                HasPreviousPage: page > 1,
                HasNextPage: page < totalPages
            );
        }

        private async Task<List<dynamic>> GetFilesByIdsAsync(long[] ids)
        {
            return await _dbContext.Files
                .Where(f => ids.Contains(f.Id))
                .Select(f => new { f.Id, f.File_Path })
                .ToListAsync<dynamic>();
        }

        private byte[] GenerateDownloadContent(IEnumerable<dynamic> files, long[] ids)
        {
            var urlBuilder = new StringBuilder();

            // Process the IDs in the order they were provided
            foreach (var currentId in ids)
            {
                var file = files.FirstOrDefault(f => f.Id == currentId);
                if (file != null)
                {
                    urlBuilder.AppendLine(file.File_Path);
                }
            }

            var textContent = urlBuilder.ToString();
            return Encoding.UTF8.GetBytes(textContent);
        }
    }
}
