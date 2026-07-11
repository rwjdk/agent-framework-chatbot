using System.Security.Claims;
using System.Text.Json;
using AgentFrameworkToolkit.Tools;
using AgentFrameworkToolkit.Tools.ModelContextProtocol;
using ChatBot.Api.Mapping;
using ChatBot.Api.Models;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using ServiceDefaults.Extensions;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

namespace ChatBot.Api.Endpoints;

internal static class ChatBotApiEndpoints
{
    private const int MaximumAttachmentCount = 10;
    private const long MaximumAttachmentSize = 20 * 1024 * 1024;
    private const long MaximumAudioSize = 25 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatBotApi(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder api = endpoints.MapGroup("/api").RequireAuthorization();

        api.MapGet("/session", (HttpContext context, IAntiforgery antiforgery) =>
        {
            AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
            string displayName = context.User.Identity?.Name ?? "User";
            return TypedResults.Ok(new UserSessionDto(displayName, tokens.RequestToken ?? string.Empty));
        });

        api.MapGet("/server-settings", (ServerSettings settings) => TypedResults.Ok(new ServerSettingsDto(
            settings.UseImageGeneration,
            settings.UseUserMemory,
            settings.AllowMcpServers,
            settings.AllowCustomInstructions,
            settings.AllowChatVisualsCustomization,
            settings.AllowFileAttachments,
            settings.AllowAudioTranscription)));

        api.MapGet("/conversations", async (ClaimsPrincipal user, IConversationsService conversationsService) =>
        {
            string userId = GetRequiredUserId(user);
            List<Conversation> conversations = await conversationsService.LoadPreviousConversationsAsync(userId);
            List<ConversationSummaryDto> summaries = conversations
                .OrderByDescending(conversation => conversation.Id)
                .Select(conversation => conversation.ToSummaryDto())
                .ToList();
            return TypedResults.Ok(summaries);
        });

        api.MapPost("/conversations", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
            {
                return;
            }

            string userId = GetRequiredUserId(context.User);
            await context.Response.WriteAsJsonAsync(Conversation.NewConversation(userId).ToDto(), JsonOptions, context.RequestAborted);
        }).DisableAntiforgery();

        api.MapGet("/conversations/{conversationId:guid}", async Task<Results<Ok<ConversationDto>, NotFound>> (
            Guid conversationId,
            ClaimsPrincipal user,
            IConversationsService conversationsService) =>
        {
            string userId = GetRequiredUserId(user);
            Conversation? conversation = await conversationsService.LoadConversationAsync(userId, conversationId);
            return conversation is null ? TypedResults.NotFound() : TypedResults.Ok(conversation.ToDto());
        });

        api.MapPut("/conversations/{conversationId:guid}/title", async Task (
            Guid conversationId,
            RenameConversationRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            IConversationsService conversationsService) =>
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
            {
                return;
            }

            string title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "A conversation title is required.");
                return;
            }

            string userId = GetRequiredUserId(context.User);
            Conversation? conversation = await conversationsService.LoadConversationAsync(userId, conversationId);
            if (conversation is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            conversation.Title = title;
            await conversationsService.StoreConversationAsync(conversation);
            await context.Response.WriteAsJsonAsync(conversation.ToSummaryDto(), JsonOptions, context.RequestAborted);
        }).DisableAntiforgery();

        api.MapDelete("/conversations/{conversationId:guid}", async Task (
            Guid conversationId,
            HttpContext context,
            IAntiforgery antiforgery,
            IConversationsService conversationsService) =>
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
            {
                return;
            }

            await conversationsService.DeleteConversationAsync(GetRequiredUserId(context.User), conversationId);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        }).DisableAntiforgery();

        api.MapGet("/settings", async (ClaimsPrincipal user, ISettingsService settingsService) =>
        {
            Settings settings = await settingsService.LoadAsync(GetRequiredUserId(user));
            return TypedResults.Ok(settings.ToDto());
        });

        api.MapPut("/settings", SaveSettingsAsync).DisableAntiforgery();
        api.MapDelete("/data", DeleteUserDataAsync).DisableAntiforgery();
        api.MapPost("/transcriptions", TranscribeAudioAsync).DisableAntiforgery();
        api.MapPost("/conversations/{conversationId:guid}/messages", SendMessageAsync).DisableAntiforgery();

        api.MapGet("/files/attachments/{storedFileName}", async Task<Results<FileContentHttpResult, NotFound, ForbidHttpResult>> (
            string storedFileName,
            ClaimsPrincipal user,
            IStorageService storageService) =>
        {
            string userId = user.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return TypedResults.Forbid();
            }

            StoredFile? storedFile = await storageService.GetAttachmentAsync(userId, storedFileName);
            return storedFile is null ? TypedResults.NotFound() : TypedResults.File(storedFile.Bytes, storedFile.ContentType);
        });

        api.MapGet("/files/generated-images/{storedFileName}", async Task<Results<FileContentHttpResult, NotFound, ForbidHttpResult>> (
            string storedFileName,
            ClaimsPrincipal user,
            IStorageService storageService) =>
        {
            string userId = user.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return TypedResults.Forbid();
            }

            StoredFile? storedFile = await storageService.GetGeneratedImageAsync(userId, storedFileName);
            return storedFile is null ? TypedResults.NotFound() : TypedResults.File(storedFile.Bytes, storedFile.ContentType);
        });

        return endpoints;
    }

    private static async Task SaveSettingsAsync(
        SettingsDto request,
        HttpContext context,
        IAntiforgery antiforgery,
        ISettingsService settingsService,
        ServerSettings serverSettings)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return;
        }

        string? validationError = ValidateSettings(request, serverSettings);
        if (validationError is not null)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, validationError);
            return;
        }

        string userId = GetRequiredUserId(context.User);
        Settings settings = await settingsService.LoadAsync(userId);
        if (serverSettings.AllowChatVisualsCustomization)
        {
            settings.Streaming = request.Streaming;
            settings.ShowReasoning = request.ShowReasoning;
            settings.ShowTokens = request.ShowTokens;
            settings.ShowToolCalls = request.ShowToolCalls;
            settings.ShowMemoryUpdate = request.ShowMemoryUpdate;
        }

        if (serverSettings.AllowCustomInstructions)
        {
            settings.Instructions = request.Instructions;
        }

        if (serverSettings.UseUserMemory)
        {
            settings.UserMemories = request.UserMemories.Select(memory => memory.Trim()).ToList();
        }

        if (serverSettings.AllowMcpServers)
        {
            settings.McpServers = request.McpServers.Select(server => new McpServer
            {
                Name = server.Name.Trim(),
                Url = server.Url.Trim(),
                Headers = server.Headers.ToDictionary(header => header.Key.Trim(), header => header.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            }).ToList();
        }

        await settingsService.SaveAsync(settings);
        await context.Response.WriteAsJsonAsync(settings.ToDto(), JsonOptions, context.RequestAborted);
    }

    private static async Task DeleteUserDataAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IConversationsService conversationsService,
        ISettingsService settingsService)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return;
        }

        string userId = GetRequiredUserId(context.User);
        await conversationsService.DeleteUserConversationsAsync(userId);
        await settingsService.DeleteSettingsAsync(userId);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static async Task TranscribeAudioAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        AgentService agentService,
        ServerSettings serverSettings)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return;
        }

        if (!serverSettings.AllowAudioTranscription)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Audio transcription is disabled.");
            return;
        }

        if (!context.Request.HasFormContentType)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Audio must be sent as multipart form data.");
            return;
        }

        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        IFormFile? audio = form.Files.GetFile("audio");
        if (audio is null || audio.Length == 0 || audio.Length > MaximumAudioSize)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Select an audio recording smaller than 25 MB.");
            return;
        }

        await using Stream audioStream = audio.OpenReadStream();
        string transcription = await agentService.GenerateTranscriptionAsync(audioStream, audio.FileName);
        await context.Response.WriteAsJsonAsync(new TranscriptionDto(transcription), JsonOptions, context.RequestAborted);
    }

    private static async Task SendMessageAsync(
        Guid conversationId,
        HttpContext context,
        IAntiforgery antiforgery,
        AIToolsFactory aiToolsFactory,
        IConversationsService conversationsService,
        AgentService agentService,
        ISettingsService settingsService,
        IStorageService storageService,
        ConversationChatMessageMapper conversationChatMessageMapper,
        ServerSettings serverSettings,
        ILoggerFactory loggerFactory)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return;
        }

        if (!context.Request.HasFormContentType)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Messages must be sent as multipart form data.");
            return;
        }

        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        string input = form["message"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "A message is required.");
            return;
        }

        string? attachmentError = ValidateAttachments(form.Files, serverSettings);
        if (attachmentError is not null)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, attachmentError);
            return;
        }

        List<McpClientTools> mcpClientTools = [];
        ILogger logger = loggerFactory.CreateLogger("ChatBot.Api.Chat");
        try
        {
            string userId = GetRequiredUserId(context.User);
            Settings settings = await settingsService.LoadAsync(userId);
            Conversation conversation = await conversationsService.LoadConversationAsync(userId, conversationId)
                ?? new Conversation { Id = conversationId, UserId = userId };

            if (conversation.MissingATitle)
            {
                conversation.Title = await agentService.GenerateTitleAsync(input);
            }

            List<ConversationAttachment> attachments = [];
            if (serverSettings.AllowFileAttachments)
            {
                foreach (IFormFile file in form.Files)
                {
                    await using MemoryStream memoryStream = new();
                    await file.CopyToAsync(memoryStream, context.RequestAborted);
                    attachments.Add(await storageService.SaveAttachmentAsync(userId, file.FileName, file.ContentType, memoryStream.ToArray()));
                }
            }

            conversation.AddUserMessage(input, attachments);

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/x-ndjson; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store";
            await WriteEventAsync(context, new ChatStreamEvent("conversation", Conversation: conversation.ToDto()));

            if (serverSettings.AllowMcpServers)
            {
                foreach (McpServer mcpServer in settings.McpServers)
                {
                    McpClientTools mcpClientTool = await aiToolsFactory.GetToolsFromRemoteMcpAsync(mcpServer.Url, mcpServer.Headers);
                    mcpClientTools.Add(mcpClientTool);
                }
            }

            async Task MemoryUpdateNotificationAsync(MemoryUpdate memoryUpdate)
            {
                List<string> added = memoryUpdate.MemoryToAdd ?? [];
                List<string> removed = memoryUpdate.MemoryToRemove ?? [];
                MemoryUpdateDto dto = new(added, removed, memoryUpdate.GetDisplayString());
                await WriteEventAsync(context, new ChatStreamEvent("memory", MemoryUpdate: dto));
            }

            AIAgent agent = agentService.GetMainAgent(userId, mcpClientTools, conversation, settings.Instructions, MemoryUpdateNotificationAsync);
            List<ChatMessage> chatMessages = await conversationChatMessageMapper.ToChatMessagesAsync(conversation);
            bool useStreaming = !serverSettings.AllowChatVisualsCustomization || settings.Streaming;
            AgentResponse response;
            if (useStreaming)
            {
                List<AgentResponseUpdate> updates = [];
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(chatMessages).WithCancellation(context.RequestAborted))
                {
                    updates.Add(update);
                    foreach (AIContent content in update.Contents)
                    {
                        if (content is TextReasoningContent reasoning && !string.IsNullOrWhiteSpace(reasoning.Text))
                        {
                            await WriteEventAsync(context, new ChatStreamEvent("reasoning", Text: reasoning.Text));
                        }
                        else if (content is FunctionCallContent functionCall)
                        {
                            await WriteEventAsync(context, new ChatStreamEvent("toolCall", ToolCall: functionCall.ToToolCallDto(update.Contents)));
                        }
                    }

                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        await WriteEventAsync(context, new ChatStreamEvent("text", Text: update.Text));
                    }
                }

                response = updates.ToAgentResponse();
            }
            else
            {
                response = await agent.RunAsync(chatMessages);
            }

            conversation.AddDataFromAgentResponse(response);
            await conversationsService.StoreConversationAsync(conversation);
            await WriteEventAsync(context, new ChatStreamEvent("complete", Conversation: conversation.ToDto()));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Chat request was cancelled by the client.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send a chat message.");
            if (context.Response.HasStarted)
            {
                await WriteEventAsync(context, new ChatStreamEvent("error", Error: exception.Message));
            }
            else
            {
                await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, $"Failed to send message: {exception.Message}");
            }
        }
        finally
        {
            foreach (McpClientTools mcpClientTool in mcpClientTools)
            {
                await mcpClientTool.McpClient.DisposeAsync();
            }
        }
    }

    private static string? ValidateSettings(SettingsDto settings, ServerSettings serverSettings)
    {
        if (serverSettings.UseUserMemory)
        {
            if (settings.UserMemories.Any(string.IsNullOrWhiteSpace))
            {
                return "Memories cannot be blank.";
            }

            List<string> memories = settings.UserMemories.Select(memory => memory.Trim()).ToList();
            if (memories.Count != memories.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                return "Duplicate memories are not allowed.";
            }
        }

        if (serverSettings.AllowMcpServers)
        {
            for (int index = 0; index < settings.McpServers.Count; index++)
            {
                McpServerDto server = settings.McpServers[index];
                if (string.IsNullOrWhiteSpace(server.Name) || string.IsNullOrWhiteSpace(server.Url))
                {
                    return $"Server {index + 1} must have a name and URL.";
                }

                if (server.Headers.Keys.Any(string.IsNullOrWhiteSpace))
                {
                    return $"Server {index + 1} has a header value without a key.";
                }
            }
        }

        return null;
    }

    private static string? ValidateAttachments(IFormFileCollection files, ServerSettings serverSettings)
    {
        if (files.Count == 0)
        {
            return null;
        }

        if (!serverSettings.AllowFileAttachments)
        {
            return "File attachments are disabled.";
        }

        if (files.Count > MaximumAttachmentCount)
        {
            return $"A maximum of {MaximumAttachmentCount} attachments is allowed.";
        }

        foreach (IFormFile file in files)
        {
            bool supportedType = file.ContentType == "application/pdf" || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            if (!supportedType)
            {
                return $"'{file.FileName}' is not a supported PDF or image file.";
            }

            if (file.Length == 0 || file.Length > MaximumAttachmentSize)
            {
                return $"'{file.FileName}' must be smaller than 20 MB.";
            }
        }

        return null;
    }

    private static async Task<bool> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The request verification token is invalid or expired.");
            return false;
        }
    }

    private static string GetRequiredUserId(ClaimsPrincipal user)
    {
        string userId = user.GetUserId();
        return string.IsNullOrWhiteSpace(userId) ? throw new InvalidOperationException("The authenticated user has no stable identifier.") : userId;
    }

    private static async Task WriteEventAsync(HttpContext context, ChatStreamEvent chatEvent)
    {
        await JsonSerializer.SerializeAsync(context.Response.Body, chatEvent, JsonOptions, context.RequestAborted);
        await context.Response.WriteAsync("\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { detail }, JsonOptions, context.RequestAborted);
    }
}
