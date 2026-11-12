using System.Security.Claims;

namespace ML_MOVIE.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    public static string GetTheme(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("theme")?.Value ?? "dark";
    }

    public static bool IsEmailVerified(this ClaimsPrincipal principal)
    {
        return bool.TryParse(principal.FindFirst("email_verified")?.Value, out var isVerified) && isVerified;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return bool.TryParse(principal.FindFirst("is_admin")?.Value, out var isAdmin) && isAdmin;
    }
}
