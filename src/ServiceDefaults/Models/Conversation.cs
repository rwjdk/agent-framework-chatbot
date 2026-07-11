using JetBrains.Annotations;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

namespace ServiceDefaults.Models;

public class Conversation
{
    public static Conversation NewConversation(string userId)
    {
        return new Conversation
        {
            Id = Guid.CreateVersion7(),
            UserId = userId
        };
    }

    public List<ConversationMessage> Messages { get; [UsedImplicitly] init; } = [];

    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;
    public string? Title { get; set; }

    [JsonIgnore]
    public bool MissingATitle => string.IsNullOrWhiteSpace(Title);

    public void AddUserMessage(string message, List<ConversationAttachment>? attachments = null)
    {
        Messages.Add(new ConversationMessage
        {
            Role = ChatRole.User,
            Text = message,
            Attachments = attachments ?? []
        });
    }

    public void AddDataFromAgentResponse(AgentResponse response)
    {
        foreach (ChatMessage message in response.Messages)
        {
            Messages.Add(new ConversationMessage
            {
                Role = message.Role,
                Text = message.Text,
                Contents = message.Contents.ToList()
            });
        }
        Messages.LastOrDefault()?.Usage = response.Usage;
    }
}
