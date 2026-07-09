using System.Security.Claims;

namespace ServiceDefaults.Extensions;

public static class ClaimsPrincipalExtensions
{
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string SubjectClaimType = "sub";

    public static string GetUserId(this ClaimsPrincipal user)
    {
        string? auth0Subject = user.FindFirstValue(SubjectClaimType);
        if (!string.IsNullOrWhiteSpace(auth0Subject))
        {
            return ToSafeUserId($"auth0_{auth0Subject}");
        }

        string? microsoftObjectId = user.FindFirstValue(ObjectIdentifierClaimType);
        if (!string.IsNullOrWhiteSpace(microsoftObjectId))
        {
            return ToSafeUserId(microsoftObjectId);
        }

        string? nameIdentifier = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(nameIdentifier))
        {
            return ToSafeUserId(nameIdentifier);
        }

        return ToSafeUserId(user.Identity?.Name ?? string.Empty);
    }

    private static string ToSafeUserId(string userId)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();

        return string.Concat(userId.Select(character => invalidCharacters.Contains(character) || character is '/' or '\\' ? '_' : character));
    }
}
