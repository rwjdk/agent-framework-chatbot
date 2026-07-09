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

ServerSettings serverSettings = builder.Configuration
    .GetRequiredSection(nameof(ServerSettings))
    .Get<ServerSettings>() ?? throw new InvalidOperationException($"{nameof(ServerSettings)} configuration is missing.");

builder.AddProject<ChatBot_BlazorServerOnly>("blazor-server-only")
    .WithEnvironment(SecretKeys.WeatherServiceKey, weatherServiceKey)
    .WithEnvironment(SecretKeys.AzureOpenAIEndpoint, azureOpenAiEndpoint)
    .WithEnvironment(SecretKeys.AzureOpenAIKey, azureOpenAiKey)
    .WithEnvironment(SecretKeys.CosmosDbConnectionString, cosmosDbConnectionString)
    .WithEnvironment(SecretKeys.BlobStorageConnectionString, blobStorageConnectionString)
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.UseImageGeneration)}", serverSettings.UseImageGeneration.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.UseUserMemory)}", serverSettings.UseUserMemory.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowMcpServers)}", serverSettings.AllowMcpServers.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowCustomInstructions)}", serverSettings.AllowCustomInstructions.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowChatVisualsCustomization)}", serverSettings.AllowChatVisualsCustomization.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowFileAttachments)}", serverSettings.AllowFileAttachments.ToString())
    .WithEnvironment($"{nameof(ServerSettings)}__{nameof(ServerSettings.AllowAudioTranscription)}", serverSettings.AllowAudioTranscription.ToString());

builder.Build().Run();
