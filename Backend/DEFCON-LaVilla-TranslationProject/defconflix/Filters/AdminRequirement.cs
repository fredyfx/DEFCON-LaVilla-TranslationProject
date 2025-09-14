using defconflix.Enums;
using Microsoft.AspNetCore.Authorization;

namespace defconflix.Filters
{
    public class AdminRequirement : IAuthorizationRequirement
    {
    }

    public class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminRequirement requirement)
        {
            // Check if user has admin role claim
            var roleClaim = context.User.FindFirst("role")?.Value;

            if (roleClaim != null && int.TryParse(roleClaim, out int roleValue))
            {
                if (roleValue == (int)UserRole.Admin)
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
