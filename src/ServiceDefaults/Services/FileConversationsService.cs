using System.Net;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using ServiceDefaults.Constants;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ServiceDefaults.Services;

public class FileConversationsService : IConversationsService
{
    public async Task<List<Conversation>> LoadPreviousConversationsAsync(string userId)
    {
        List<Conversation> conversations = [];

        string folder = GetConversationFolder(userId);
        string[] files = Directory.GetFiles(folder, "*.json");
        foreach (string file in files)
        {
            string json = await File.ReadAllTextAsync(file);
            conversations.Add(JsonSerializer.Deserialize<Conversation>(json)!);
        }
        return conversations;
    }

    public async Task StoreConversationAsync(Conversation conversation)
    {
        string path = GetConversationPath(conversation.UserId, conversation.Id);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(conversation));
    }

    public Task DeleteConversationAsync(string userId, Guid conversationId)
    {
        string path = GetConversationPath(userId, conversationId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task DeleteUserConversationsAsync(string userId)
    {
        string folder = GetConversationFolder(userId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder);
        }
        return Task.CompletedTask;
    }

    private static string GetConversationFolder(string userId)
    {
        string folder = Path.Combine(GetRootFolder(), "conversations" ,userId);
        Directory.CreateDirectory(folder);
        return folder;
    }
    private static string GetConversationPath(string userId, Guid conversationId)
    {
        string folder = GetConversationFolder(userId);
        return Path.Combine(folder, $"{conversationId}.json");
    }

    private static string GetRootFolder()
    {
        return Path.Combine(Path.GetTempPath(), "chatbot");
    }
}
