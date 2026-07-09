using ChatBot.BlazorServerOnly.Authentication.Auth0;
using ChatBot.BlazorServerOnly.Authentication.EntraId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ChatBot.BlazorServerOnly.Authentication.Login;

internal static class LoginEndpointExtensions
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapGet(LoginAuthenticationConstants.ChooseLoginMethodPath, (string? returnUrl) =>
        {
            string localReturnUrl = GetLocalRedirectUri(returnUrl);
            string encodedReturnUrl = Uri.EscapeDataString(localReturnUrl);

            return Results.Content($$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Choose login method</title>
                <style>
                    body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: system-ui, sans-serif; background: #f6f7f9; color: #1f2933; }
                    main { width: min(420px, calc(100vw - 32px)); padding: 24px; background: #fff; border: 1px solid #d9dee7; border-radius: 8px; box-shadow: 0 12px 30px rgba(15, 23, 42, .08); }
                    h1 { margin: 0 0 8px; font-size: 24px; line-height: 1.2; }
                    p { margin: 0 0 20px; line-height: 1.5; color: #52606d; }
                    nav { display: grid; gap: 12px; }
                    a { display: flex; align-items: center; justify-content: center; min-height: 44px; padding: 0 14px; border-radius: 6px; background: #1f2933; color: #fff; text-decoration: none; font-weight: 600; }
                    a.secondary { background: #fff; color: #1f2933; border: 1px solid #9aa5b1; }
                    a:focus-visible { outline: 3px solid #7cc4fa; outline-offset: 2px; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Choose login method</h1>
                    <p>Select the identity provider you want to use for this session.</p>
                    <nav aria-label="Login methods">
                        <a href="/login/auth0?returnUrl={{encodedReturnUrl}}">Continue with Auth0</a>
                        <a class="secondary" href="/login/entra?returnUrl={{encodedReturnUrl}}">Continue with Entra ID</a>
                    </nav>
                </main>
            </body>
            </html>
            """, "text/html");
        }).AllowAnonymous();

        endpointRouteBuilder.MapGet("/login", () => Results.Redirect(LoginAuthenticationConstants.ChooseLoginMethodPath)).AllowAnonymous();
        endpointRouteBuilder.MapGet("/login/auth0", (string? returnUrl) => ChallengeExternalLogin(Auth0AuthenticationExtensions.AuthenticationScheme, returnUrl)).AllowAnonymous();
        endpointRouteBuilder.MapGet("/login/entra", (string? returnUrl) => ChallengeExternalLogin(EntraIdAuthenticationExtensions.AuthenticationScheme, returnUrl)).AllowAnonymous();
        endpointRouteBuilder.MapGet(LoginAuthenticationConstants.LoginErrorPath, () => Results.Content(
            """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Authentication cancelled</title>
                <style>
                    body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: system-ui, sans-serif; background: #f6f7f9; color: #1f2933; }
                    main { width: min(420px, calc(100vw - 32px)); padding: 24px; background: #fff; border: 1px solid #d9dee7; border-radius: 8px; box-shadow: 0 12px 30px rgba(15, 23, 42, .08); }
                    h1 { margin: 0 0 8px; font-size: 24px; line-height: 1.2; }
                    p { margin: 0 0 20px; line-height: 1.5; color: #52606d; }
                    a { display: inline-flex; align-items: center; min-height: 40px; padding: 0 14px; border-radius: 6px; background: #1f2933; color: #fff; text-decoration: none; font-weight: 600; }
                    a:focus-visible { outline: 3px solid #7cc4fa; outline-offset: 2px; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Authentication cancelled</h1>
                    <p>You did not authorize the sign-in request. Try again when you are ready.</p>
                    <a href="/chooseLoginMethod">Try again</a>
                </main>
            </body>
            </html>
            """,
            "text/html")).AllowAnonymous();

        endpointRouteBuilder.MapGet("/logout", async (HttpContext httpContext) =>
        {
            string? authenticationScheme = httpContext.User.FindFirst(LoginAuthenticationConstants.AuthenticationSchemeClaimType)?.Value;
            AuthenticationProperties authenticationProperties = new()
            {
                RedirectUri = "/"
            };

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authenticationScheme is Auth0AuthenticationExtensions.AuthenticationScheme or EntraIdAuthenticationExtensions.AuthenticationScheme)
            {
                await httpContext.SignOutAsync(authenticationScheme, authenticationProperties);
                return Results.Empty;
            }

            return Results.Redirect("/");
        }).RequireAuthorization();

        return endpointRouteBuilder;
    }

    private static IResult ChallengeExternalLogin(string authenticationScheme, string? returnUrl)
    {
        AuthenticationProperties authenticationProperties = new()
        {
            RedirectUri = GetLocalRedirectUri(returnUrl)
        };

        return Results.Challenge(authenticationProperties, [authenticationScheme]);
    }

    private static string GetLocalRedirectUri(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return Uri.TryCreate(returnUrl, UriKind.Relative, out Uri? _)
            ? returnUrl
            : "/";
    }
}
