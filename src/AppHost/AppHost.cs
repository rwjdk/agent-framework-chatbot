using AppHost.Extensions;
using Projects;
using ServiceDefaults.Models;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

AzureOpenAISettings azureOpenAI = builder.Configuration.GetRequiredSettings<AzureOpenAISettings>();
Auth0Settings auth0 = builder.Configuration.GetRequiredSettings<Auth0Settings>();
EntraIdSettings entraId = builder.Configuration.GetRequiredSettings<EntraIdSettings>();
ServerSettings server = builder.Configuration.GetRequiredSettings<ServerSettings>();

builder.AddProject<ChatBot_BlazorServerOnly>("blazor-server-only")
    .WithExternalHttpEndpoints()
    .WithAzureOpenAI(builder, azureOpenAI)
    .WithWeatherService(builder)
    .WithCosmosDb(builder)
    .WithBlobStorage(builder)
    .WithAuth0(builder, auth0)
    .WithEntraId(entraId)
    .WithServerSettings(server);

builder.Build().Run();
