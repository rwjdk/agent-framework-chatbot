using Microsoft.Extensions.AI;

namespace ServiceDefaults.Models;

public class ConversationMessage
{
    public ChatRole Role { get; init; }
    public string Text { get; init; } = string.Empty;
    public List<AIContent> Contents { get; init; } = [];
    public UsageDetails? Usage { get; set; }
    public List<ConversationAttachment> Attachments { get; init; } = [];
    public string? ImagePath { get; init; }
}
