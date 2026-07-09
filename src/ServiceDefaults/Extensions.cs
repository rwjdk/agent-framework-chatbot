using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.Tools;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ServiceDefaults.Constants;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130
// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // ReSharper disable once UnusedMethodReturnValue.Global
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton(ReadServerSettings(builder.Configuration));

        //Agent Framework Toolkit Initialization
        string? azureOpenAIEndpoint = builder.Configuration[SecretKeys.AzureOpenAIEndpoint];
        string? azureOpenAIKey = builder.Configuration[SecretKeys.AzureOpenAIKey];
        if (azureOpenAIEndpoint != null && azureOpenAIKey != null)
        {
            builder.Services.AddAzureOpenAIAgentFactory(azureOpenAIEndpoint, azureOpenAIKey);
        }

        //Tools Factory (MCP)
        builder.Services.AddAIToolFactory();

        //Cosmos DB (or local)
        string? cosmosDbConnectionString = builder.Configuration[SecretKeys.CosmosDbConnectionString];
        if (!string.IsNullOrWhiteSpace(cosmosDbConnectionString))
        {
            if (cosmosDbConnectionString.Equals("Local", StringComparison.InvariantCultureIgnoreCase))
            {
                builder.Services.AddSingleton<IConversationsService, FileConversationsService>();
                builder.Services.AddSingleton<ISettingsService, FileSettingsService>();
            }
            else
            {
                builder.Services.AddSingleton(new CosmosClient(
                    cosmosDbConnectionString,
                    new CosmosClientOptions
                    {
                        UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    }));
                builder.Services.AddSingleton<IConversationsService, CosmosDbConversationsService>();
                builder.Services.AddSingleton<ISettingsService, CosmosDbSettingsService>();
            }
        }

        //Blob Storage (or local)
        string? blobStorageConnectionString = builder.Configuration[SecretKeys.BlobStorageConnectionString];
        if (blobStorageConnectionString != null)
        {
            if (blobStorageConnectionString.Equals("Local", StringComparison.InvariantCultureIgnoreCase))
            {
                builder.Services.AddSingleton<IStorageService, FileStorageService>();
            }
            else
            {
                builder.Services.AddSingleton<IStorageService, BlobStorageService>();
            }
        }

        //Other Services
        builder.Services.AddSingleton<AgentService>();
        builder.Services.AddSingleton<ImageGenerationService>();
        builder.Services.AddSingleton<ConversationChatMessageMapper>();
        
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });



        return builder;
    }

    private static ServerSettings ReadServerSettings(IConfiguration configuration)
    {
        return configuration
            .GetRequiredSection(nameof(ServerSettings))
            .Get<ServerSettings>() ?? throw new InvalidOperationException($"{nameof(ServerSettings)} configuration is missing.");
    }

    [PublicAPI]
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    // ReSharper disable once VariableHidesOuterVariable
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        bool useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    [PublicAPI]
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    [PublicAPI]
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
