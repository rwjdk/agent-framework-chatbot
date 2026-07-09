using System.Net;
using System.Security.Cryptography;
using Microsoft.Azure.Cosmos;
using ServiceDefaults.Constants;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;

namespace ServiceDefaults.Services;

public class CosmosDbSettingsService(CosmosClient cosmosClient) : ISettingsService
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private Container? _container;

    public async Task<Settings> LoadAsync(string userId)
    {
        Container container = await GetContainerAsync();
        try
        {
            ItemResponse<Settings> response = await container.ReadItemAsync<Settings>(userId, new PartitionKey(userId));
            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            Settings settings = Settings.CreateDefault(userId);
            await SaveAsync(settings);
            return settings;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The settings passphrase could not decrypt the stored settings.", exception);
        }
    }

    public async Task SaveAsync(Settings settings)
    {
        Container container = await GetContainerAsync();
        await container.UpsertItemAsync(settings, new PartitionKey(settings.UserId));
    }

    public async Task DeleteSettingsAsync(string userId)
    {
        Container container = await GetContainerAsync();
        try
        {
            await container.DeleteItemAsync<Settings>(userId, new PartitionKey(userId));
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            //Empty
        }
    }

    private async Task<Container> GetContainerAsync()
    {
        if (_container is not null)
        {
            return _container;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_container is not null)
            {
                return _container;
            }

            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(CosmosDbIds.DatabaseName);
            ContainerResponse containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(CosmosDbIds.Containers.Settings.ContainerName, CosmosDbIds.Containers.Settings.PartitionKeyPath), 400);
            _container = containerResponse.Container;
            return _container;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
