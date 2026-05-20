using QuickBite.API.Models;

namespace QuickBite.API.Services;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user);
}
