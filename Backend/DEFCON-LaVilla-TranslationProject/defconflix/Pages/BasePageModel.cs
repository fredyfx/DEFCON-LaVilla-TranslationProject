using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
namespace defconflix.Pages
{
    public class BasePageModel : PageModel
    {
        public bool IsAuthenticated => User.Identity?.IsAuthenticated ?? false;
        public string Username => User.FindFirst(ClaimTypes.Name)?.Value ?? "Guest";
        public string GitHubId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        protected void SetPageTitle(string title)
        {
            ViewData["Title"] = title;
        }

        protected void AddSuccessMessage(string message)
        {
            TempData["SuccessMessage"] = message;
        }

        protected void AddErrorMessage(string message)
        {
            TempData["ErrorMessage"] = message;
        }
    }
}
