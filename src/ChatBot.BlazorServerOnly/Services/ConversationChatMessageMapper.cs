using Microsoft.Extensions.AI;
using ServiceDefaults.Models;

namespace ChatBot.BlazorServerOnly.Services;

public class ConversationChatMessageMapper(FileUploadStorageService fileUploadStorageService)
{
    public async Task<List<ChatMessage>> ToChatMessagesAsync(Conversation conversation)
    {
        List<ChatMessage> chatMessages = [];
        foreach (ConversationMessage message in conversation.Messages)
        {
            chatMessages.Add(await ToChatMessageAsync(message));
        }

        return chatMessages;
    }

    private async Task<ChatMessage> ToChatMessageAsync(ConversationMessage message)
    {
        if (message.Role == ChatRole.User && message.Attachments.Count > 0)
        {
            List<AIContent> contents = [new TextContent(message.Text)];
            foreach (ConversationAttachment attachment in message.Attachments)
            {
                contents.Add(await fileUploadStorageService.CreateDataContentAsync(attachment));
            }

            return new ChatMessage(ChatRole.User, contents);
        }

        if (message.Contents.Count > 0)
        {
            return new ChatMessage(message.Role, message.Contents);
        }

        return new ChatMessage(message.Role, message.Text);
    }
}
