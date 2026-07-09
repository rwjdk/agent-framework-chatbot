using ChatBot.BlazorServerOnly.Authentication.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ChatBot.BlazorServerOnly.Authentication.Auth0;

internal static class Auth0AuthenticationExtensions
{
    public const string AuthenticationScheme = "Auth0";

    public static AuthenticationBuilder AddAuth0Authentication(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        string authority = GetAuthority(configuration);
        string clientId = GetRequiredConfigurationValue(configuration, "Auth0:ClientId");
        string clientSecret = GetRequiredConfigurationValue(configuration, "Auth0:ClientSecret");
        PathString callbackPath = new(GetRequiredConfigurationValue(configuration, "Auth0:CallbackPath"));

        authenticationBuilder.AddOpenIdConnect(AuthenticationScheme, options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.CallbackPath = callbackPath;
            options.ResponseType = "code";
            options.SaveTokens = false;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name"
            };
            options.Events.OnTokenValidated = context =>
            {
                LoginAuthenticationClaims.AddAuthenticationSchemeClaim(context.Principal, AuthenticationScheme);

                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect(LoginAuthenticationConstants.LoginErrorPath);

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

    private static string GetAuthority(IConfiguration configuration)
    {
        string domain = GetRequiredConfigurationValue(configuration, "Auth0:Domain").Trim().TrimEnd('/');
        if (!domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            domain = $"https://{domain}";
        }

        return domain;
    }

    private static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required configuration value '{key}'.");
        }

        return value;
    }
}
