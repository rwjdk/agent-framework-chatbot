using Microsoft.Extensions.Configuration;
using Projects;
using ServiceDefaults.Constants;
using ServiceDefaults.Models;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

//Parameters
IResourceBuilder<ParameterResource> azureOpenAiEndpoint = builder.AddParameter(SecretKeys.AzureOpenAIEndpoint, secret: false);
IResourceBuilder<ParameterResource> azureOpenAiKey = builder.AddParameter(SecretKeys.AzureOpenAIKey, secret: true);
IResourceBuilder<ParameterResource> weatherServiceKey = builder.AddParameter(SecretKeys.WeatherServiceKey, secret: true);
weatherServiceKey.Resource.Description = "Enter 'None' if you do not want to use Open Weather Service";

IResourceBuilder<ParameterResource> cosmosDbConnectionString = builder.AddParameter(SecretKeys.CosmosDbConnectionString, secret: true);
cosmosDbConnectionString.Resource.Description = "Enter 'Local' if you do not with to use CosmosDB";

IResourceBuilder<ParameterResource> blobStorageConnectionString = builder.AddParameter(SecretKeys.BlobStorageConnectionString, secret: true);
blobStorageConnectionString.Resource.Description = "Enter 'Local' if you do not with to use BlobStorage";

IResourceBuilder<ParameterResource> auth0Domain = builder.AddParameter(SecretKeys.Auth0Domain, secret: true);
auth0Domain.Resource.Description = "Enter the Auth0 tenant domain, or 'None' if you do not want to use Auth0";

IResourceBuilder<ParameterResource> auth0ClientId = builder.AddParameter(SecretKeys.Auth0ClientId, secret: true);
auth0ClientId.Resource.Description = "Enter the Auth0 application client ID. Use 'None' to disable Auth0 login";

IResourceBuilder<ParameterResource> auth0ClientSecret = builder.AddParameter(SecretKeys.Auth0ClientSecret, secret: true);
auth0ClientSecret.Resource.Description = "Enter the Auth0 application client secret, or 'None' if you do not want to use Auth0";

IResourceBuilder<ParameterResource> auth0CallbackPath = builder.AddParameter(SecretKeys.Auth0CallbackPath, "/callback", secret: false);
auth0CallbackPath.Resource.Description = "Enter the Auth0 callback path";

IResourceBuilder<ParameterResource> entraIdInstance = builder.AddParameter(SecretKeys.EntraIdInstance, "https://login.microsoftonline.com/", secret: false);
entraIdInstance.Resource.Description = "Enter the Entra ID instance URL";

IResourceBuilder<ParameterResource> entraIdTenantId = builder.AddParameter(SecretKeys.EntraIdTenantId, secret: true);
entraIdTenantId.Resource.Description = "Enter the Entra ID tenant ID, or 'None' if you do not want to use Entra ID";

IResourceBuilder<ParameterResource> entraIdClientId = builder.AddParameter(SecretKeys.EntraIdClientId, secret: true);
entraIdClientId.Resource.Description = "Enter the Entra ID application client ID. Use 'None' to disable Entra ID login";

IResourceBuilder<ParameterResource> entraIdCallbackPath = builder.AddParameter(SecretKeys.EntraIdCallbackPath, "/signin-oidc", secret: false);
entraIdCallbackPath.Resource.Description = "Enter the Entra ID callback path";

IResourceBuilder<ParameterResource> entraIdSignedOutCallbackPath = builder.AddParameter(SecretKeys.EntraIdSignedOutCallbackPath, "/signout-callback-oidc", secret: false);
entraIdSignedOutCallbackPath.Resource.Description = "Enter the Entra ID signed-out callback path";

ServerSettings serverSettings = builder.Configuration
    .GetRequiredSection(nameof(ServerSettings))
    .Get<ServerSettings>() ?? throw new InvalidOperationException($"{nameof(ServerSettings)} configuration is missing.");

builder.AddProject<ChatBot_BlazorServerOnly>("blazor-server-only")
    .WithEnvironment(SecretKeys.WeatherServiceKey, weatherServiceKey)
    .WithEnvironment(SecretKeys.AzureOpenAIEndpoint, azureOpenAiEndpoint)
    .WithEnvironment(SecretKeys.AzureOpenAIKey, azureOpenAiKey)
    .WithEnvironment(SecretKeys.CosmosDbConnectionString, cosmosDbConnectionString)
    .WithEnvironment(SecretKeys.BlobStorageConnectionString, blobStorageConnectionString)
    .WithEnvironment(ToEnvironmentVariableName(SecretKeys.Auth0Domain), auth0Domain)
    .WithEnvironment(ToEnvironmentVariableName(SecretKeys.Auth0ClientId), auth0ClientId)
    .WithEnvironment(ToEnvironmentVariableName(SecretKeys.Auth0ClientSecret), auth0ClientSecret)
    .WithEnvironment(ToEnvironmentVariableName(SecretKeys.Auth0CallbackPath), auth0CallbackPath)
    .WithEnvironment(ToAzureAdEnvironmentVariableName(SecretKeys.EntraIdInstance), entraIdInstance)
    .WithEnvironment(ToAzureAdEnvironmentVariableName(SecretKeys.EntraIdTenantId), entraIdTenantId)
    .WithEnvironment(ToAzureAdEnvironmentVariableName(SecretKeys.EntraIdClientId), entraIdClientId)
    .WithEnvironment(ToAzureAdEnvironmentVariableName(SecretKeys.EntraIdCallbackPath), entraIdCallbackPath)
    .WithEnvironment(ToAzureAdEnvironmentVariableName(SecretKeys.EntraIdSignedOutCallbackPath), entraIdSignedOutCallbackPath)
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.UseImageGeneration)}", serverSettings.UseImageGeneration.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.UseUserMemory)}", serverSettings.UseUserMemory.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowMcpServers)}", serverSettings.AllowMcpServers.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowCustomInstructions)}", serverSettings.AllowCustomInstructions.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowChatVisualsCustomization)}", serverSettings.AllowChatVisualsCustomization.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowFileAttachments)}", serverSettings.AllowFileAttachments.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowAudioTranscription)}", serverSettings.AllowAudioTranscription.ToString());

builder.Build().Run();

static string ToEnvironmentVariableName(string parameterName)
{
    return parameterName.Replace("-", "__", StringComparison.Ordinal);
}

static string ToAzureAdEnvironmentVariableName(string parameterName)
{
    string environmentVariableName = ToEnvironmentVariableName(parameterName);
    return environmentVariableName.Replace("EntraId__", "AzureAd__", StringComparison.Ordinal);
}
