using defconflix.Data;
using defconflix.WebAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace defconflix.Pages.FileSearch
{
    public class FileSearchModel : BasePageModel
    {
        private readonly ApiContext _dbContext;
        private readonly IFilesService _filesService;
        private readonly ILogger<FileSearchModel> _logger;

        public FileSearchModel(
            ApiContext dbContext,
            IFilesService filesService,
            ILogger<FileSearchModel> logger)
        {
            _dbContext = dbContext;
            _filesService = filesService;
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
        public List<FileDTO> Files { get; set; } = new();
        public int TotalPages { get; set; }
        public int TotalFiles { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        public List<ConferenceInfo> AvailableConferences { get; set; } = new();

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

        public async Task<IActionResult> OnGetAsync([FromQuery] string? fileType, [FromQuery] string? conference, [FromQuery] string? searchTerm, [FromQuery] int? page, [FromQuery] int? pageSize)
        {
            FileType = fileType;
            Conference = conference;
            SearchTerm = searchTerm;
            Page = page ?? 1;
            PageSize = pageSize ?? 10;

            await GetConferences();

            if (string.IsNullOrEmpty(FileType))
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

        private async Task GetConferences()
        {
            AvailableConferences = await _dbContext.Conferences
                .OrderBy(c => c.Name)
                .Select(c => new ConferenceInfo
                {
                    Id = c.Id,
                    DisplayName = c.Name
                })
                .ToListAsync();
        }

        private async Task SearchFilesAsync()
        {
            var result = await _filesService.SearchFilesByTypeConferenceAndTerm(FileType, Conference, SearchTerm, Page, PageSize);
            Files = result.Files.ToList();
            Page = result.Pagination.CurrentPage;
            TotalPages = result.Pagination.TotalPages;
            TotalFiles = result.Pagination.TotalFiles;
            HasPreviousPage = result.Pagination.HasPreviousPage;
            HasNextPage = result.Pagination.HasNextPage;
            PaginationInfo pagination = new()
            {
                CurrentPage = Page,
                PageSize = PageSize,
                TotalPages = TotalPages,
                TotalFiles = TotalFiles,
                HasPreviousPage = HasPreviousPage,
                HasNextPage = HasNextPage
            };
        }
    }
}