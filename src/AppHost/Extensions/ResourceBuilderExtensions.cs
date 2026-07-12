using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using ServiceDefaults.Constants;
using ServiceDefaults.Models;

namespace AppHost.Extensions;

public static class ResourceBuilderExtensions
{
    public static TSettings GetRequiredSettings<TSettings>(this IConfiguration configuration) where TSettings : class, ISectionSettings
    {
        return configuration
            .GetRequiredSection(TSettings.SectionName)
            .Get<TSettings>() ?? throw new InvalidOperationException($"{typeof(TSettings).Name} configuration is missing.");
    }

    public static IResourceBuilder<ProjectResource> WithAzureOpenAI(
        this IResourceBuilder<ProjectResource> resource,
        IDistributedApplicationBuilder builder,
        AzureOpenAISettings settings)
    {
        IResourceBuilder<ParameterResource> key = AddSecretParameter(
            builder,
            SecretKeys.AzureOpenAIKey,
            GetSettingsKey<AzureOpenAISettings>(nameof(AzureOpenAISettings.Key)));

        return resource
            .WithEnvironment(SecretKeys.AzureOpenAIEndpoint, settings.Endpoint)
            .WithEnvironment(SecretKeys.AzureOpenAIKey, key);
    }

    public static IResourceBuilder<ProjectResource> WithWeatherService(
        this IResourceBuilder<ProjectResource> resource,
        IDistributedApplicationBuilder builder)
    {
        IResourceBuilder<ParameterResource> key = AddSecretParameter(
            builder,
            SecretKeys.WeatherServiceKey,
            description: "Enter 'None' if you do not want to use Open Weather Service");
        return resource.WithEnvironment(SecretKeys.WeatherServiceKey, key);
    }

    public static IResourceBuilder<ProjectResource> WithCosmosDb(
        this IResourceBuilder<ProjectResource> resource,
        IDistributedApplicationBuilder builder)
    {
        IResourceBuilder<ParameterResource> cosmosDb = AddSecretParameter(
            builder,
            SecretKeys.CosmosDbConnectionString,
            description: "Enter 'Local' if you do not wish to use CosmosDB");

        return resource.WithEnvironment(SecretKeys.CosmosDbConnectionString, cosmosDb);
    }

    public static IResourceBuilder<ProjectResource> WithBlobStorage(
        this IResourceBuilder<ProjectResource> resource,
        IDistributedApplicationBuilder builder)
    {
        IResourceBuilder<ParameterResource> blobStorage = AddSecretParameter(
            builder,
            SecretKeys.BlobStorageConnectionString,
            description: "Enter 'Local' if you do not wish to use BlobStorage");

        return resource.WithEnvironment(SecretKeys.BlobStorageConnectionString, blobStorage);
    }

    public static IResourceBuilder<ProjectResource> WithAuth0(
        this IResourceBuilder<ProjectResource> resource,
        IDistributedApplicationBuilder builder,
        Auth0Settings settings)
    {
        IResourceBuilder<ParameterResource> clientSecret = AddSecretParameter(
            builder,
            SecretKeys.Auth0ClientSecret,
            GetSettingsKey<Auth0Settings>(nameof(Auth0Settings.ClientSecret)),
            "Enter the Auth0 application client secret, or 'None' if you do not want to use Auth0");

        return resource
            .WithEnvironment(settings, value => value.Domain)
            .WithEnvironment(settings, value => value.ClientId)
            .WithEnvironment<ProjectResource, Auth0Settings, string>(value => value.ClientSecret, clientSecret)
            .WithEnvironment(settings, value => value.CallbackPath);
    }

    public static IResourceBuilder<ProjectResource> WithEntraId(this IResourceBuilder<ProjectResource> resource, EntraIdSettings settings)
    {
        return resource
            .WithEnvironment(settings, value => value.Instance)
            .WithEnvironment(settings, value => value.TenantId)
            .WithEnvironment(settings, value => value.ClientId)
            .WithEnvironment(settings, value => value.CallbackPath)
            .WithEnvironment(settings, value => value.SignedOutCallbackPath);
    }

    public static IResourceBuilder<ProjectResource> WithServerSettings(this IResourceBuilder<ProjectResource> resource, ServerSettings settings)
    {
        return resource
            .WithEnvironment(settings, value => value.UseImageGeneration)
            .WithEnvironment(settings, value => value.UseUserMemory)
            .WithEnvironment(settings, value => value.AllowMcpServers)
            .WithEnvironment(settings, value => value.AllowCustomInstructions)
            .WithEnvironment(settings, value => value.AllowChatVisualsCustomization)
            .WithEnvironment(settings, value => value.AllowFileAttachments)
            .WithEnvironment(settings, value => value.AllowAudioTranscription);
    }

    private static IResourceBuilder<TResource> WithEnvironment<TResource, TSettings, TValue>(
        this IResourceBuilder<TResource> resource,
        TSettings settings,
        Expression<Func<TSettings, TValue>> propertyExpression)
        where TResource : IResourceWithEnvironment
        where TSettings : ISectionSettings
    {
        TValue value = propertyExpression.Compile()(settings);
        return resource.WithEnvironment(GetEnvironmentVariableName(propertyExpression), value?.ToString() ?? string.Empty);
    }

    private static IResourceBuilder<TResource> WithEnvironment<TResource, TSettings, TValue>(
        this IResourceBuilder<TResource> resource,
        Expression<Func<TSettings, TValue>> propertyExpression,
        IResourceBuilder<ParameterResource> parameter)
        where TResource : IResourceWithEnvironment
        where TSettings : ISectionSettings
    {
        return resource.WithEnvironment(GetEnvironmentVariableName(propertyExpression), parameter);
    }

    private static IResourceBuilder<ParameterResource> AddSecretParameter(
        IDistributedApplicationBuilder builder,
        string name,
        string? configurationKey = null,
        string? description = null)
    {
        IResourceBuilder<ParameterResource> parameter = configurationKey is null
            ? builder.AddParameter(name, secret: true)
            : builder.AddParameterFromConfiguration(name, configurationKey, secret: true);
        parameter.Resource.Description = description;
        return parameter;
    }

    private static string GetSettingsKey<TSettings>(string propertyName) where TSettings : ISectionSettings
    {
        return $"{TSettings.SectionName}:{propertyName}";
    }

    private static string GetEnvironmentVariableName<TSettings, TValue>(Expression<Func<TSettings, TValue>> propertyExpression)
        where TSettings : ISectionSettings
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("The expression must select a settings property.", nameof(propertyExpression));
        }

        return $"{TSettings.SectionName}__{memberExpression.Member.Name}";
    }
}
