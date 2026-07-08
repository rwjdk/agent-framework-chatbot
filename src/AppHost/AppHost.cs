using Projects;
using ServiceDefaults.Constants;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

//Parameters
IResourceBuilder<ParameterResource> azureOpenAiEndpoint = builder.AddParameter(SecretKeys.AzureOpenAIEndpoint, secret: false);
IResourceBuilder<ParameterResource> azureOpenAiKey = builder.AddParameter(SecretKeys.AzureOpenAIKey, secret: true);
IResourceBuilder<ParameterResource> weatherServiceKey = builder.AddParameter(SecretKeys.WeatherServiceKey, secret: true);
IResourceBuilder<ParameterResource> cosmosDbConnectionString = builder.AddParameter(SecretKeys.CosmosDbConnectionString, secret: true);

builder.AddProject<ChatBot_BlazorServerOnly>("blazor-server-only")
    .WithEnvironment(SecretKeys.WeatherServiceKey, weatherServiceKey)
    .WithEnvironment(SecretKeys.AzureOpenAIEndpoint, azureOpenAiEndpoint)
    .WithEnvironment(SecretKeys.AzureOpenAIKey, azureOpenAiKey)
    .WithEnvironment(SecretKeys.CosmosDbConnectionString, cosmosDbConnectionString);

builder.Build().Run();
