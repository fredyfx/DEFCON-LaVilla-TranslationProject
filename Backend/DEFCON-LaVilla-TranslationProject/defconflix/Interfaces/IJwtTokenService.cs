using defconflix.Models;
using System.Security.Claims;

namespace defconflix.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
        ClaimsPrincipal ValidateToken(string token);
    }

}
