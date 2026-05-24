using System.Security.Claims;

namespace Koot.Api.Services;

internal static class CurrentUser
{
    /// <summary>
    /// Extract the authenticated user id from the principal's claims.
    /// JwtService writes the id into "userId" and ClaimTypes.NameIdentifier (and sub).
    /// </summary>
    public static bool TryGetId(ClaimsPrincipal principal, out int userId)
    {
        var raw = principal.FindFirstValue("userId")
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        return int.TryParse(raw, out userId);
    }
}
