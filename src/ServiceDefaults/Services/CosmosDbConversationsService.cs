using System.Net;
using Microsoft.Azure.Cosmos;
using ServiceDefaults.Constants;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;

namespace ServiceDefaults.Services;

public class CosmosDbConversationsService(CosmosClient cosmosClient) : IConversationsService
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private Container? _container;

    public async Task<List<Conversation>> LoadPreviousConversationsAsync(string userId)
    {
        List<Conversation> conversations = [];
        Container container = await GetContainerAsync();
        QueryDefinition queryDefinition = new("SELECT * FROM c WHERE c.userId = @userId");
        queryDefinition.WithParameter("@userId", userId);
        using FeedIterator<Conversation> iterator = container.GetItemQueryIterator<Conversation>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            FeedResponse<Conversation> response = await iterator.ReadNextAsync();
            foreach (Conversation conversation in response)
            {
                conversations.Add(conversation);
            }
        }

        return conversations;
    }

    public async Task StoreConversationAsync(Conversation conversation)
    {
        Container container = await GetContainerAsync();
        await container.UpsertItemAsync(conversation, new PartitionKey(conversation.UserId));
    }

    public async Task DeleteConversationAsync(string userId, Guid conversationId)
    {
        Container container = await GetContainerAsync();
        try
        {
            await container.DeleteItemAsync<Conversation>(conversationId.ToString(), new PartitionKey(userId));
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            //Empty
        }
    }

    public async Task DeleteUserConversationsAsync(string userId)
    {
        List<Conversation> conversations = await LoadPreviousConversationsAsync(userId);
        foreach (Conversation conversation in conversations)
        {
            await DeleteConversationAsync(userId, conversation.Id);
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
            ContainerResponse containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(CosmosDbIds.Containers.Conversations.ContainerName, CosmosDbIds.Containers.Conversations.PartitionKeyPath), 400);
            _container = containerResponse.Container;
            return _container;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
