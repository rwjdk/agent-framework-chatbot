using Microsoft.Extensions.Configuration;
using Projects;
using ServiceDefaults.Constants;
using ServiceDefaults.Models;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

//Parameters
IResourceBuilder<ParameterResource> azureOpenAiEndpoint = builder.AddParameter(SecretKeys.AzureOpenAIEndpoint, secret: false);
IResourceBuilder<ParameterResource> azureOpenAiKey = builder.AddParameter(SecretKeys.AzureOpenAIKey, secret: true);
IResourceBuilder<ParameterResource> weatherServiceKey = builder.AddParameter(SecretKeys.WeatherServiceKey, secret: true);
IResourceBuilder<ParameterResource> cosmosDbConnectionString = builder.AddParameter(SecretKeys.CosmosDbConnectionString, secret: true);
cosmosDbConnectionString.Resource.Description = "Enter 'Local' if you do not with to use CosmosDB";

IResourceBuilder<ParameterResource> blobStorageConnectionString = builder.AddParameter(SecretKeys.BlobStorageConnectionString, secret: true);
blobStorageConnectionString.Resource.Description = "Enter 'Local' if you do not with to use BlobStorage";

builder.AddProject<ChatBot_BlazorServerOnly>("blazor-server-only")
    .WithEnvironment(SecretKeys.WeatherServiceKey, weatherServiceKey)
    .WithEnvironment(SecretKeys.AzureOpenAIEndpoint, azureOpenAiEndpoint)
    .WithEnvironment(SecretKeys.AzureOpenAIKey, azureOpenAiKey)
    .WithEnvironment(SecretKeys.CosmosDbConnectionString, cosmosDbConnectionString)
    .WithEnvironment(SecretKeys.BlobStorageConnectionString, blobStorageConnectionString);

builder.Build().Run();
