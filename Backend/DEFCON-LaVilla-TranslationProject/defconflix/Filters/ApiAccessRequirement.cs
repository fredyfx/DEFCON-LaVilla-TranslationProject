using Microsoft.AspNetCore.Authorization;

namespace defconflix.Filters
{
    public class ApiAccessRequirement : IAuthorizationRequirement
    {
    }

    public class ApiAccessAuthorizationHandler : AuthorizationHandler<ApiAccessRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ApiAccessRequirement requirement)
        {
            var httpContext = context.Resource as HttpContext;

            // Accept Bearer token auth
            if (context.User.Identity?.AuthenticationType == "Bearer")
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Accept Cookie auth (from GitHub OAuth login)
            if (context.User.Identity?.IsAuthenticated == true &&
                context.User.Identity?.AuthenticationType == "Cookies")
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Accept API key auth (from HttpContext items set by ApiKeyAuthenticationHandler)
            if (httpContext?.Items.ContainsKey("UserId") == true &&
                httpContext?.Items.ContainsKey("User") == true)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Accept API key claim
            var apiKeyClaim = context.User.FindFirst("apiKey")?.Value;
            if (!string.IsNullOrEmpty(apiKeyClaim))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // If none of the above, fail the requirement
            return Task.CompletedTask;
        }
    }
}
