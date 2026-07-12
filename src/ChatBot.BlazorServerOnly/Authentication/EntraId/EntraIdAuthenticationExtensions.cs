using ChatBot.BlazorServerOnly.Authentication.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using ServiceDefaults.Models;

namespace ChatBot.BlazorServerOnly.Authentication.EntraId;

internal static class EntraIdAuthenticationExtensions
{
    public const string AuthenticationScheme = "EntraId";

    public static bool IsEntraIdAuthenticationEnabled(this IConfiguration configuration)
    {
        return IsClientIdEnabled(configuration[$"{EntraIdSettings.SectionName}:{nameof(EntraIdSettings.ClientId)}"]);
    }

    public static AuthenticationBuilder AddEntraIdAuthentication(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        if (!configuration.IsEntraIdAuthenticationEnabled())
        {
            return authenticationBuilder;
        }

        authenticationBuilder.AddMicrosoftIdentityWebApp(configuration.GetSection(EntraIdSettings.SectionName), AuthenticationScheme);
        authenticationBuilder.Services.PostConfigure<OpenIdConnectOptions>(AuthenticationScheme, options =>
        {
            Func<TokenValidatedContext, Task> existingTokenValidated = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                await existingTokenValidated(context);
                LoginAuthenticationClaims.AddAuthenticationSchemeClaim(context.Principal, AuthenticationScheme);
            };

            Func<RemoteFailureContext, Task> existingRemoteFailure = options.Events.OnRemoteFailure;
            options.Events.OnRemoteFailure = async context =>
            {
                await existingRemoteFailure(context);
                context.HandleResponse();
                context.Response.Redirect(LoginAuthenticationConstants.LoginErrorPath);
            };
        });

        return authenticationBuilder;
    }

    private static bool IsClientIdEnabled(string? clientId)
    {
        return !string.IsNullOrWhiteSpace(clientId) && !clientId.Equals("None", StringComparison.OrdinalIgnoreCase);
    }
}
