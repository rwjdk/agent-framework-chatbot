namespace ServiceDefaults.Models;

public class ConversationAttachment
{
    public string UserId { get; init; } = string.Empty;
    public required string FileName { get; init; }
    public required string StoredFileName { get; init; }
    public required string ContentType { get; init; }
    public required string RelativePath { get; init; }
}
