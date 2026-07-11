using ChatBot.Api.Models;
using Microsoft.Extensions.AI;
using ServiceDefaults.Extensions;
using ServiceDefaults.Models;

namespace ChatBot.Api.Mapping;

internal static class ConversationDtoMapper
{
    private const int CollapsedFunctionCallLength = 96;

    public static ConversationSummaryDto ToSummaryDto(this Conversation conversation)
    {
        return new ConversationSummaryDto(conversation.Id, conversation.Title);
    }

    public static ConversationDto ToDto(this Conversation conversation)
    {
        List<AIContent> allContents = conversation.Messages.SelectMany(message => message.Contents).ToList();
        List<ConversationMessageDto> messages = conversation.Messages.Select(message => ToMessageDto(message, allContents)).ToList();
        return new ConversationDto(conversation.Id, conversation.Title, messages);
    }

    public static ToolCallDto ToToolCallDto(this FunctionCallContent functionCall, IEnumerable<AIContent> contents)
    {
        string summary = functionCall.AsDisplayString();
        FunctionResultContent? result = contents
            .OfType<FunctionResultContent>()
            .FirstOrDefault(functionResult => functionResult.CallId == functionCall.CallId);

        return new ToolCallDto(
            functionCall.CallId,
            summary,
            summary.Length > CollapsedFunctionCallLength ? summary : null,
            result?.AsDisplayString());
    }

    public static SettingsDto ToDto(this Settings settings)
    {
        List<McpServerDto> mcpServers = settings.McpServers
            .Select(server => new McpServerDto(server.Name, server.Url, server.Headers))
            .ToList();

        return new SettingsDto(
            settings.Streaming,
            settings.ShowReasoning,
            settings.ShowTokens,
            settings.ShowToolCalls,
            settings.ShowMemoryUpdate,
            settings.Instructions,
            settings.UserMemories,
            mcpServers);
    }

    private static ConversationMessageDto ToMessageDto(ConversationMessage message, IReadOnlyList<AIContent> allContents)
    {
        string role = message.Role == ChatRole.User ? "user" : message.Role == ChatRole.Assistant ? "assistant" : "system";
        List<ConversationAttachmentDto> attachments = message.Attachments
            .Select(attachment => new ConversationAttachmentDto(
                attachment.FileName,
                attachment.ContentType,
                $"/api/files/attachments/{Uri.EscapeDataString(attachment.StoredFileName)}"))
            .ToList();
        List<string> reasoning = message.Contents
            .OfType<TextReasoningContent>()
            .Select(content => content.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        List<ToolCallDto> toolCalls = message.Contents
            .OfType<FunctionCallContent>()
            .Select(content => content.ToToolCallDto(allContents))
            .ToList();
        TokenUsageDto? usage = message.Usage is null
            ? null
            : new TokenUsageDto(message.Usage.InputTokenCount, message.Usage.OutputTokenCount, message.Usage.ReasoningTokenCount);
        string? imageUrl = string.IsNullOrWhiteSpace(message.ImagePath)
            ? null
            : $"/api/files/generated-images/{Uri.EscapeDataString(Path.GetFileName(message.ImagePath))}";

        return new ConversationMessageDto(role, message.Text, attachments, reasoning, toolCalls, usage, imageUrl);
    }
}
