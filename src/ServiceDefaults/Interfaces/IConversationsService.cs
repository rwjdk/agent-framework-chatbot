using ServiceDefaults.Models;

namespace ServiceDefaults.Interfaces;

public interface IConversationsService
{
    Task<List<Conversation>> LoadPreviousConversationsAsync(string userId);
    Task StoreConversationAsync(Conversation conversation);
    Task DeleteConversationAsync(string userId, Guid conversationId);
    Task DeleteUserConversationsAsync(string userId);
}