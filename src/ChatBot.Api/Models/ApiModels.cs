namespace ChatBot.Api.Models;

internal sealed record UserSessionDto(string DisplayName, string AntiforgeryToken);

internal sealed record ServerSettingsDto(
    bool UseImageGeneration,
    bool UseUserMemory,
    bool AllowMcpServers,
    bool AllowCustomInstructions,
    bool AllowChatVisualsCustomization,
    bool AllowFileAttachments,
    bool AllowAudioTranscription);

internal sealed record ConversationSummaryDto(Guid Id, string? Title);

internal sealed record ConversationDto(Guid Id, string? Title, IReadOnlyList<ConversationMessageDto> Messages);

internal sealed record ConversationMessageDto(
    string Role,
    string Text,
    IReadOnlyList<ConversationAttachmentDto> Attachments,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<ToolCallDto> ToolCalls,
    TokenUsageDto? Usage,
    string? ImageUrl);

internal sealed record ConversationAttachmentDto(string FileName, string ContentType, string Url);

internal sealed record ToolCallDto(string CallId, string Summary, string? FullRequest, string? Result);

internal sealed record TokenUsageDto(long? Input, long? Output, long? Reasoning);

internal sealed record SettingsDto(
    bool Streaming,
    bool ShowReasoning,
    bool ShowTokens,
    bool ShowToolCalls,
    bool ShowMemoryUpdate,
    string Instructions,
    IReadOnlyList<string> UserMemories,
    IReadOnlyList<McpServerDto> McpServers);

internal sealed record McpServerDto(string Name, string Url, IReadOnlyDictionary<string, string> Headers);

internal sealed record RenameConversationRequest(string Title);

internal sealed record TranscriptionDto(string Text);

internal sealed record MemoryUpdateDto(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, string DisplayText);

internal sealed record ChatStreamEvent(
    string Type,
    ConversationDto? Conversation = null,
    string? Text = null,
    ToolCallDto? ToolCall = null,
    MemoryUpdateDto? MemoryUpdate = null,
    string? Error = null);
