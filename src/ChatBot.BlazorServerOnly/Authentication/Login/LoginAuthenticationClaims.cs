using System.Security.Claims;

namespace ChatBot.BlazorServerOnly.Authentication.Login;

internal static class LoginAuthenticationClaims
{
    public static void AddAuthenticationSchemeClaim(ClaimsPrincipal? principal, string authenticationScheme)
    {
        if (principal?.Identity is ClaimsIdentity claimsIdentity)
        {
            claimsIdentity.AddClaim(new Claim(LoginAuthenticationConstants.AuthenticationSchemeClaimType, authenticationScheme));
        }
    }
}
