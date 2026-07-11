using System.Security.Claims;
using ChatBot.BlazorServerOnly.Authentication.Auth0;
using ChatBot.BlazorServerOnly.Authentication.EntraId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ChatBot.BlazorServerOnly.Authentication.Login;

internal static class LoginEndpointExtensions
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder endpointRouteBuilder, IReadOnlyList<LoginProvider> loginProviders)
    {
        endpointRouteBuilder.MapGet(LoginAuthenticationConstants.ChooseLoginMethodPath, (string? returnUrl) =>
        {
            string localReturnUrl = GetLocalRedirectUri(returnUrl);
            string encodedReturnUrl = Uri.EscapeDataString(localReturnUrl);
            if (!loginProviders.Any(loginProvider => loginProvider.IsEnabled))
            {
                return Results.Content(CreateGuestLoginPage(encodedReturnUrl), "text/html");
            }

            string loginLinks = string.Join(Environment.NewLine, loginProviders
                .Where(loginProvider => loginProvider.IsEnabled)
                .Select(loginProvider => $"""<a href="{loginProvider.LoginPath}?returnUrl={encodedReturnUrl}">Continue with {loginProvider.DisplayName}</a>"""));

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
                    a:focus-visible { outline: 3px solid #7cc4fa; outline-offset: 2px; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Choose login method</h1>
                    <p>Select the identity provider you want to use for this session.</p>
                    <nav aria-label="Login methods">
                        {{loginLinks}}
                    </nav>
                </main>
            </body>
            </html>
            """, "text/html");
        }).AllowAnonymous();

        endpointRouteBuilder.MapGet("/login", () => Results.Redirect(LoginAuthenticationConstants.ChooseLoginMethodPath)).AllowAnonymous();
        endpointRouteBuilder.MapGet("/login/guest", async (HttpContext httpContext, string? userId, string? returnUrl) =>
        {
            if (loginProviders.Any(loginProvider => loginProvider.IsEnabled))
            {
                return Results.NotFound();
            }

            if (!Guid.TryParseExact(userId, "D", out Guid guestUserId))
            {
                return Results.BadRequest();
            }

            Claim[] claims =
            [
                new(ClaimTypes.NameIdentifier, guestUserId.ToString("D")),
                new(ClaimTypes.Name, "Guest"),
                new(LoginAuthenticationConstants.AuthenticationSchemeClaimType, LoginAuthenticationConstants.GuestAuthenticationScheme)
            ];
            ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal principal = new(identity);

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Results.Redirect(GetLocalRedirectUri(returnUrl));
        }).AllowAnonymous();
        endpointRouteBuilder.MapGet("/login/auth0", (string? returnUrl) => ChallengeExternalLogin(Auth0AuthenticationExtensions.AuthenticationScheme, loginProviders, returnUrl)).AllowAnonymous();
        endpointRouteBuilder.MapGet("/login/entra", (string? returnUrl) => ChallengeExternalLogin(EntraIdAuthenticationExtensions.AuthenticationScheme, loginProviders, returnUrl)).AllowAnonymous();
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
            if (loginProviders.Any(loginProvider => loginProvider.AuthenticationScheme == authenticationScheme && loginProvider.IsEnabled))
            {
                await httpContext.SignOutAsync(authenticationScheme!, authenticationProperties);
                return Results.Empty;
            }

            return Results.Redirect("/");
        }).RequireAuthorization();

        return endpointRouteBuilder;
    }

    private static string CreateGuestLoginPage(string encodedReturnUrl)
    {
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Starting guest session</title>
                <style>
                    body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: system-ui, sans-serif; background: #f6f7f9; color: #1f2933; }
                    main { width: min(420px, calc(100vw - 32px)); padding: 24px; background: #fff; border: 1px solid #d9dee7; border-radius: 8px; box-shadow: 0 12px 30px rgba(15, 23, 42, .08); }
                    h1 { margin: 0 0 8px; font-size: 24px; line-height: 1.2; }
                    p { margin: 0; line-height: 1.5; color: #52606d; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Starting guest session</h1>
                    <p id="status">Preparing your browser profile...</p>
                    <noscript>Guest mode requires JavaScript so the browser can retain your profile.</noscript>
                </main>
                <script>
                    const storageKey = '{{LocalStorageKeys.UserId}}';
                    const userIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

                    try {
                        let userId = window.localStorage.getItem(storageKey);
                        if (!userIdPattern.test(userId)) {
                            userId = window.crypto.randomUUID();
                            window.localStorage.setItem(storageKey, userId);
                        }

                        window.location.replace(`/login/guest?userId=${encodeURIComponent(userId)}&returnUrl={{encodedReturnUrl}}`);
                    } catch {
                        document.getElementById('status').textContent = 'Guest mode needs access to browser local storage. Enable it and reload this page.';
                    }
                </script>
            </body>
            </html>
            """;
    }

    private static IResult ChallengeExternalLogin(string authenticationScheme, IReadOnlyList<LoginProvider> loginProviders, string? returnUrl)
    {
        if (!loginProviders.Any(loginProvider => loginProvider.AuthenticationScheme == authenticationScheme && loginProvider.IsEnabled))
        {
            return Results.NotFound();
        }

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
