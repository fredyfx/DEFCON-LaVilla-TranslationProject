using defconflix.Data;
using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace defconflix.Pages.Profile
{
    [Authorize] // Require authentication for this page
    public class IndexModel : BasePageModel
    {
        private readonly ApiContext _context;
        private readonly IJwtTokenService _jwtService;

        public IndexModel(ApiContext context, IJwtTokenService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        public User CurrentUser { get; set; }
        public string JwtToken { get; set; }
        public bool ShowApiKey { get; set; } = false;
        public bool ShowJwtToken { get; set; } = false;
        public int TranslationsCompleted { get; set; }
        public int TranslationsInProgress { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            SetPageTitle("Profile");

            var githubId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(githubId))
            {
                return RedirectToPage("/Login");
            }

            CurrentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.GitHubId == githubId);

            if (CurrentUser == null)
            {
                return RedirectToPage("/Login");
            }

            // Generate fresh JWT token for display
            JwtToken = _jwtService.GenerateToken(CurrentUser);

            // Get user statistics
            TranslationsCompleted = await _context.Files
                .CountAsync(f => f.ProcessedBy == CurrentUser.Id && f.Status == "Completed");

            TranslationsInProgress = await _context.Files
                .CountAsync(f => f.ProcessedBy == CurrentUser.Id && f.Status == "In Progress");

            return Page();
        }

        public async Task<IActionResult> OnPostResetApiKeyAsync()
        {
            var githubId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            user.ApiKey = Guid.NewGuid().ToString();
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            AddSuccessMessage("API Key reset successfully!");
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostGenerateNewTokenAsync()
        {
            var githubId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            AddSuccessMessage("New JWT token generated!");
            return RedirectToPage();
        }
    }
}
