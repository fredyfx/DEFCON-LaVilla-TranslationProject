using defconflix.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace defconflix.Pages.FileSearch
{
    public class FileSearchModel : BasePageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ApiContext _dbContext;
        private readonly ILogger<FileSearchModel> _logger;

        public FileSearchModel(
            HttpClient httpClient,
            ApiContext dbContext,
            ILogger<FileSearchModel> logger)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string? FileType { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Conference { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        // Results properties
        public List<FileDto> Files { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalFiles { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        public List<ConferenceInfo> AvailableConferences { get; set; } = new();

        public class FileDto
        {
            public int Id { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public class PaginationInfo
        {
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
            public int TotalFiles { get; set; }
            public bool HasPreviousPage { get; set; }
            public bool HasNextPage { get; set; }
        }

        public class ConferenceInfo
        {
            public long Id { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }

        public class ApiFilesResponse
        {
            public List<FileDto> Files { get; set; } = new();
            public PaginationInfo Pagination { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            AvailableConferences = await _dbContext.Conferences
                .OrderBy(c => c.Name)
                .Select(c => new ConferenceInfo
                {
                    Id = c.Id,
                    DisplayName = c.Name
                })
                .ToListAsync();

            if (string.IsNullOrEmpty(FileType) && string.IsNullOrEmpty(SearchTerm))
            {
                return Page();
            }

            try
            {
                await SearchFilesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching files");
                ModelState.AddModelError(string.Empty, "An error occurred while searching files. Please try again.");
            }

            return Page();
        }
        
        private async Task SearchFilesAsync()
        {
            if (string.IsNullOrEmpty(FileType))
            {
                ModelState.AddModelError(nameof(FileType), "File type is required.");
                return;
            }

            // Build the API URL
            var baseUrl = $"/api/files/{FileType}";

            // Add conference and search term if provided
            if (!string.IsNullOrEmpty(Conference) && !string.IsNullOrEmpty(SearchTerm))
            {
                baseUrl = $"/api/files/{FileType}/conference/{Conference}/search/{Uri.EscapeDataString(SearchTerm)}";
            }

            // Add pagination parameters
            var queryParams = new List<string>
            {
                $"page={Page}",
                $"pagesize={PageSize}"
            };

            var fullUrl = $"{baseUrl}?{string.Join("&", queryParams)}";

            _logger.LogInformation("Searching files with URL: {Url}", fullUrl);

            var response = await _httpClient.GetAsync(fullUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiFilesResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse != null)
                {
                    Files = apiResponse.Files;
                    CurrentPage = apiResponse.Pagination.CurrentPage;
                    TotalPages = apiResponse.Pagination.TotalPages;
                    TotalFiles = apiResponse.Pagination.TotalFiles;
                    HasPreviousPage = apiResponse.Pagination.HasPreviousPage;
                    HasNextPage = apiResponse.Pagination.HasNextPage;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API call failed with status {StatusCode}: {Content}", response.StatusCode, errorContent);

                ModelState.AddModelError(string.Empty,
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest
                        ? "Invalid search parameters."
                        : "Unable to search files at this time. Please try again later.");
            }
        }
    }
}