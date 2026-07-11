using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

namespace ChatBot.Api.Authentication;

internal static class AuthenticationExtensions
{
    private const string Auth0Scheme = "Auth0";
    private const string EntraIdScheme = "EntraId";
    private const string CookieScheme = "ReactCookies";
    private const string AuthenticationSchemeClaimType = "chatbot_authentication_scheme";
    private const string ChooseLoginMethodPath = "/api/auth/login";
    private const string LoginErrorPath = "/api/auth/login-error";

    public static IReadOnlyList<LoginProvider> AddChatBotAuthentication(this WebApplicationBuilder builder)
    {
        AuthenticationBuilder authenticationBuilder = builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieScheme;
                options.DefaultChallengeScheme = CookieScheme;
                options.DefaultSignInScheme = CookieScheme;
            });

        bool auth0Enabled = builder.Configuration.IsAuthenticationEnabled("Auth0:ClientId");
        bool entraIdEnabled = builder.Configuration.IsAuthenticationEnabled("AzureAd:ClientId");
        if (auth0Enabled)
        {
            authenticationBuilder.AddAuth0Authentication(builder.Configuration);
        }

        if (entraIdEnabled)
        {
            authenticationBuilder.AddEntraIdAuthentication(builder.Configuration);
        }
        else
        {
            authenticationBuilder.AddCookie(CookieScheme);
        }

        builder.Services.Configure<CookieAuthenticationOptions>(CookieScheme, options =>
        {
            options.Cookie.Name = ".ChatBot.React.Authentication";
            options.LoginPath = ChooseLoginMethodPath;
            options.ExpireTimeSpan = TimeSpan.FromDays(90);
            options.SlidingExpiration = true;
            options.Events.OnSigningIn = context =>
            {
                context.Properties.IsPersistent = true;
                context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(options.ExpireTimeSpan);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        List<LoginProvider> loginProviders =
        [
            new("Auth0", "/api/auth/login/auth0", Auth0Scheme, auth0Enabled),
            new("Entra ID", "/api/auth/login/entra", EntraIdScheme, entraIdEnabled)
        ];
        if (!loginProviders.Any(loginProvider => loginProvider.IsEnabled))
        {
            throw new InvalidOperationException("At least one login provider must be enabled. Set Auth0-ClientId or EntraId-ClientId to a real value instead of 'None'.");
        }

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return loginProviders;
    }

    public static IEndpointRouteBuilder MapChatBotAuthentication(this IEndpointRouteBuilder endpoints, IReadOnlyList<LoginProvider> loginProviders)
    {
        endpoints.MapGet(ChooseLoginMethodPath, (string? returnUrl) =>
        {
            string localReturnUrl = GetLocalRedirectUri(returnUrl);
            string encodedReturnUrl = Uri.EscapeDataString(localReturnUrl);
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
                        body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: "Segoe UI", system-ui, sans-serif; background: #f5f5f5; color: #242424; }
                        main { width: min(420px, calc(100vw - 32px)); padding: 28px; background: #fff; border: 1px solid #d1d1d1; border-radius: 8px; box-shadow: 0 12px 30px rgba(0, 0, 0, .08); }
                        h1 { margin: 0 0 8px; font-size: 24px; }
                        p { margin: 0 0 20px; color: #616161; line-height: 1.5; }
                        nav { display: grid; gap: 12px; }
                        a { display: flex; align-items: center; justify-content: center; min-height: 44px; padding: 0 14px; border-radius: 4px; background: #0f6cbd; color: #fff; text-decoration: none; font-weight: 600; }
                        a:focus-visible { outline: 3px solid #479ef5; outline-offset: 2px; }
                    </style>
                </head>
                <body>
                    <main>
                        <h1>Choose login method</h1>
                        <p>Select the identity provider you want to use for the React chatbot.</p>
                        <nav aria-label="Login methods">{{loginLinks}}</nav>
                    </main>
                </body>
                </html>
                """, "text/html");
        }).AllowAnonymous();

        endpoints.MapGet("/api/auth/login/auth0", (string? returnUrl) => ChallengeExternalLogin(Auth0Scheme, loginProviders, returnUrl)).AllowAnonymous();
        endpoints.MapGet("/api/auth/login/entra", (string? returnUrl) => ChallengeExternalLogin(EntraIdScheme, loginProviders, returnUrl)).AllowAnonymous();
        endpoints.MapGet(LoginErrorPath, () => Results.Content(
            """
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Authentication cancelled</title></head>
            <body style="margin:0;min-height:100vh;display:grid;place-items:center;font-family:Segoe UI,system-ui,sans-serif;background:#f5f5f5;color:#242424">
                <main style="width:min(420px,calc(100vw - 32px));padding:28px;background:#fff;border:1px solid #d1d1d1;border-radius:8px">
                    <h1>Authentication cancelled</h1><p>You did not authorize the sign-in request.</p><a href="/">Return to the chatbot</a>
                </main>
            </body>
            </html>
            """, "text/html")).AllowAnonymous();

        endpoints.MapGet("/api/auth/logout", async (HttpContext httpContext) =>
        {
            string? authenticationScheme = httpContext.User.FindFirst(AuthenticationSchemeClaimType)?.Value;
            AuthenticationProperties authenticationProperties = new() { RedirectUri = "/" };

            await httpContext.SignOutAsync(CookieScheme);
            if (loginProviders.Any(loginProvider => loginProvider.AuthenticationScheme == authenticationScheme && loginProvider.IsEnabled))
            {
                await httpContext.SignOutAsync(authenticationScheme!, authenticationProperties);
                return Results.Empty;
            }

            return Results.Redirect("/");
        }).RequireAuthorization();

        return endpoints;
    }

    private static AuthenticationBuilder AddAuth0Authentication(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        string authority = GetRequiredConfigurationValue(configuration, "Auth0:Domain").Trim().TrimEnd('/');
        if (!authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            authority = $"https://{authority}";
        }

        string clientId = GetRequiredConfigurationValue(configuration, "Auth0:ClientId");
        string clientSecret = GetRequiredConfigurationValue(configuration, "Auth0:ClientSecret");
        PathString callbackPath = new(GetRequiredConfigurationValue(configuration, "Auth0:CallbackPath"));

        authenticationBuilder.AddOpenIdConnect(Auth0Scheme, options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.CallbackPath = callbackPath;
            options.ResponseType = "code";
            options.SaveTokens = false;
            options.SignInScheme = CookieScheme;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters { NameClaimType = "name" };
            options.Events.OnTokenValidated = context =>
            {
                AddAuthenticationSchemeClaim(context.Principal, Auth0Scheme);
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect(LoginErrorPath);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                string logoutUri = $"{authority}/v2/logout?client_id={Uri.EscapeDataString(clientId)}";
                string? postLogoutUri = context.Properties.RedirectUri;
                if (!string.IsNullOrWhiteSpace(postLogoutUri))
                {
                    if (postLogoutUri.StartsWith("/", StringComparison.Ordinal))
                    {
                        HttpRequest request = context.Request;
                        postLogoutUri = $"{request.Scheme}://{request.Host}{request.PathBase}{postLogoutUri}";
                    }

                    logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
                }

                context.Response.Redirect(logoutUri);
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });

        return authenticationBuilder;
    }

    private static AuthenticationBuilder AddEntraIdAuthentication(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        authenticationBuilder.AddMicrosoftIdentityWebApp(configuration.GetSection("AzureAd"), EntraIdScheme, CookieScheme);
        authenticationBuilder.Services.PostConfigure<OpenIdConnectOptions>(EntraIdScheme, options =>
        {
            Func<TokenValidatedContext, Task> existingTokenValidated = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                await existingTokenValidated(context);
                AddAuthenticationSchemeClaim(context.Principal, EntraIdScheme);
            };

            Func<RemoteFailureContext, Task> existingRemoteFailure = options.Events.OnRemoteFailure;
            options.Events.OnRemoteFailure = async context =>
            {
                await existingRemoteFailure(context);
                context.HandleResponse();
                context.Response.Redirect(LoginErrorPath);
            };
        });

        return authenticationBuilder;
    }

    private static IResult ChallengeExternalLogin(string authenticationScheme, IReadOnlyList<LoginProvider> loginProviders, string? returnUrl)
    {
        if (!loginProviders.Any(loginProvider => loginProvider.AuthenticationScheme == authenticationScheme && loginProvider.IsEnabled))
        {
            return Results.NotFound();
        }

        AuthenticationProperties authenticationProperties = new() { RedirectUri = GetLocalRedirectUri(returnUrl) };
        return Results.Challenge(authenticationProperties, [authenticationScheme]);
    }

    private static string GetLocalRedirectUri(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return Uri.TryCreate(returnUrl, UriKind.Relative, out Uri? _) ? returnUrl : "/";
    }

    private static void AddAuthenticationSchemeClaim(ClaimsPrincipal? principal, string authenticationScheme)
    {
        if (principal?.Identity is ClaimsIdentity claimsIdentity)
        {
            claimsIdentity.AddClaim(new Claim(AuthenticationSchemeClaimType, authenticationScheme));
        }
    }

    private static bool IsAuthenticationEnabled(this IConfiguration configuration, string clientIdKey)
    {
        string? clientId = configuration[clientIdKey];
        return !string.IsNullOrWhiteSpace(clientId) && !clientId.Equals("None", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"Missing required configuration value '{key}'.") : value;
    }
}

internal sealed record LoginProvider(string DisplayName, string LoginPath, string AuthenticationScheme, bool IsEnabled);
