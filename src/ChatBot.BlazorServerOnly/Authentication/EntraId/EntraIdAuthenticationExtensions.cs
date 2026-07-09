using ChatBot.BlazorServerOnly.Authentication.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace ChatBot.BlazorServerOnly.Authentication.EntraId;

internal static class EntraIdAuthenticationExtensions
{
    public const string AuthenticationScheme = "EntraId";

    public static AuthenticationBuilder AddEntraIdAuthentication(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        authenticationBuilder.AddMicrosoftIdentityWebApp(configuration.GetSection("AzureAd"), AuthenticationScheme);
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
}
