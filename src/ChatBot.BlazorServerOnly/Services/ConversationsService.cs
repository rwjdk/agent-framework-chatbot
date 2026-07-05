using System.Text.Json;
using ChatBot.BlazorServerOnly.Models;

namespace ChatBot.BlazorServerOnly.Services;

public class ConversationsService
{
    public async Task<List<Conversation>> LoadPreviousConversationsAsync(string userId)
    {
        List<Conversation> conversations = [];
        string conversationFolder = GetConversationFolder();
        foreach (string conversationFile in Directory.GetFiles(conversationFolder, "*.json"))
        {
            string json = await File.ReadAllTextAsync(conversationFile);
            Conversation? conversation = JsonSerializer.Deserialize<Conversation>(json);
            if (conversation?.UserId == userId)
            {
                conversations.Add(conversation);
            }
        }
        return conversations;
    }

    public async Task StoreConversationAsync(Conversation conversation)
    {
        string conversationFolder = GetConversationFolder();
        string conversationFile = Path.Combine(conversationFolder, $"{conversation.Id}.json");
        await File.WriteAllTextAsync(conversationFile, JsonSerializer.Serialize(conversation));
    }

    public void DeleteConversation(Guid conversationId)
    {
        string conversationFolder = GetConversationFolder();
        string conversationFile = Path.Combine(conversationFolder, $"{conversationId}.json");
        if (File.Exists(conversationFile))
        {
            File.Delete(conversationFile);
        }
    }

    private static string GetConversationFolder()
    {
        string tempPath = Path.GetTempPath();
        string conversationFolder = Path.Combine(tempPath, "blazor-server-only-conversations");
        if (!Directory.Exists(conversationFolder))
        {
            Directory.CreateDirectory(conversationFolder);
        }
        return conversationFolder;
    }
}
