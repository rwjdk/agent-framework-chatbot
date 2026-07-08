using System.Security.Claims;

namespace ServiceDefaults.Extensions;

public static class ClaimsPrincipalExtensions
{
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public static string GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ObjectIdentifierClaimType)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.Identity?.Name
            ?? string.Empty;
    }
}
