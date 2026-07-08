using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.Tools;
using AgentFrameworkToolkit.Tools.Common;
using AgentFrameworkToolkit.Tools.ModelContextProtocol;
using ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;
using ChatBot.BlazorServerOnly.Models;
using JetBrains.Annotations;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;
using ModelContextProtocol.Client;
using MudBlazor;
using ServiceDefaults.Extensions;
using ServiceDefaults.Models;
using ServiceDefaults.Services;
using ServiceDefaults.Tools;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot;

[UsedImplicitly]
public partial class ChatbotPage(
    AzureOpenAIAgentFactory azureOpenAIAgentFactory,
    AIToolsFactory aiToolsFactory,
    ConversationsService conversationsService,
    AgentService agentService,
    SettingsService settingsService,
    BlobStorageService blobStorageService,
    ConversationChatMessageMapper conversationChatMessageMapper,
    AuthenticationStateProvider authenticationStateProvider,
    OpenWeatherMapOptions openWeatherMapOptions,
    IJSRuntime jsRuntime,
    IDialogService dialogService,
    ISnackbar snackbar) : IAsyncDisposable
{
    private readonly UserInput _userInput = new();
    private readonly VisualState _visualState = new();
    private string _userId = string.Empty;
    private Conversation _conversation = Conversation.NewConversation(string.Empty);
    private Settings? _settings;

    //Components
    private LeftSidebar? _leftSidebar;
    private IJSObjectReference? _audioRecorderModule;
    private IJSObjectReference? _scrollModule;
    private ElementReference _chatMessagesElement;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authenticationState.User.GetUserId();
        _settings = await settingsService.LoadAsync(_userId);
        _conversation = Conversation.NewConversation(_userId);
    }

    private async Task SendAsync()
    {
        List<McpClientTools> mcpClientTools = [];
        try
        {
            if (_visualState.IsSendingMessage || _settings == null)
            {
                return;
            }

            string? input = _userInput.Text?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            _visualState.IsSendingMessage = true;
            await InvokeAsync(StateHasChanged);

            //Title
            if (_conversation.MissingATitle)
            {
                _conversation.Title = await agentService.GenerateTitleAsync(input);
                _leftSidebar?.AddConversation(_conversation);
            }

            //Attachments
            List<ConversationAttachment> attachments = [];
            foreach (UserInputAttachment file in _userInput.Attachments)
            {
                attachments.Add(await blobStorageService.SaveAsync(_userId, file.FileName, file.ContentType, file.Bytes));
            }

            //Reset GUI so it is ready for new message
            _userInput.Reset();
            _visualState.MemoryUpdate = null;
            _conversation.AddUserMessage(input, attachments);
            await InvokeAsync(StateHasChanged);
            await ScrollMessagesToBottomAsync();

            //Prepare Regular Tools
            List<AITool> tools =
            [
                WeatherTools.GetWeatherForCity(openWeatherMapOptions),
                ..aiToolsFactory.GetTools(new ImageGenerationTool(azureOpenAIAgentFactory, _conversation, blobStorageService)),
                ..TimeTools.All()
            ];

            //Prepare MCP Tools (and convert to regular tools)
            foreach (McpServer mcpServer in _settings.McpServers)
            {
                McpClientTools mcpClientTool = await aiToolsFactory.GetToolsFromRemoteMcpAsync(mcpServer.Url, mcpServer.Headers);
                mcpClientTools.Add(mcpClientTool);
                tools.AddRange(mcpClientTool.Tools);
            }

            //LLM Work
            AIAgent agent = agentService.GetMainAgent(_userId, tools, _settings.Instructions, MemoryUpdateNotificationAsync); //todo... own more of tool-generation?
            List<ChatMessage> chatMessagesToSend = await conversationChatMessageMapper.ToChatMessagesAsync(_conversation);
            AgentResponse response;
            if (_settings.Streaming)
            {
                List<AgentResponseUpdate> updates = [];
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(chatMessagesToSend))
                {
                    updates.Add(update);
                    foreach (AIContent content in update.Contents)
                    {
                        switch (content)
                        {
                            case TextReasoningContent textReasoningContent:
                                _visualState.StreamedReasoning += textReasoningContent.Text;
                                break;
                            default:
                                _visualState.StreamedContents.Add(content);
                                break;
                        }
                    }

                    _visualState.StreamedResponse += update.Text;
                    await InvokeAsync(StateHasChanged);
                }

                _visualState.ResetStreamingValues();
                response = updates.ToAgentResponse();
            }
            else
            {
                response = await agent.RunAsync(chatMessagesToSend);
            }
            _conversation.AddDataFromAgentResponse(response);

            //Store the new conversation
            await conversationsService.StoreConversationAsync(_conversation);
        }
        catch (Exception exception)
        {
            string error = !string.IsNullOrWhiteSpace(exception.Message) ? $"Failed to send message: {exception.Message}" : "Failed to send message.";
            snackbar.Add(error, Severity.Error);
        }
        finally
        {
            _visualState.IsSendingMessage = false;
            await InvokeAsync(StateHasChanged);

            //MCP Tools Cleanup
            foreach (McpClientTools mcpClientTool in mcpClientTools)
            {
                await mcpClientTool.McpClient.DisposeAsync();
            }
        }
    }

    private async Task MemoryUpdateNotificationAsync(MemoryUpdate obj)
    {
        _visualState.MemoryUpdate = obj;
        if (_settings is not null)
        {
            foreach (string memoryToRemove in obj.MemoryToRemove)
            {
                _settings.UserMemories.Remove(memoryToRemove);
            }

            foreach (string memoryToAdd in obj.MemoryToAdd.Where(x => !_settings.UserMemories.Contains(x)))
            {
                _settings.UserMemories.Add(memoryToAdd);
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private void NewChat()
    {
        _conversation = Conversation.NewConversation(_userId);
        _userInput.Reset();
        _visualState.MemoryUpdate = null;
    }

    private void SwitchSession(Conversation conversation)
    {
        _conversation = conversation;
        _userInput.Reset();
        _visualState.MemoryUpdate = null;
    }

    private void RemoveSession(Conversation conversation)
    {
        if (_conversation.Id == conversation.Id)
        {
            NewChat();
        }
    }

    private async Task OpenSettingsDialogAsync()
    {
        if (_visualState.IsSendingMessage || _settings is null)
        {
            return;
        }

        DialogParameters<SettingsDialog> parameters = new()
        {
            { x => x.UserId, _userId },
            { x => x.Settings, _settings },
            { x => x.SettingsChanged, EventCallback.Factory.Create<Settings>(this, SettingsChangedAsync) },
            { x => x.SettingsDeleted, EventCallback.Factory.Create(this, SettingsDeletedAsync) }
        };

        DialogOptions options = new()
        {
            CloseButton = true,
            FullWidth = true,
            MaxWidth = MaxWidth.Medium
        };

        await dialogService.ShowAsync<SettingsDialog>("Settings", parameters, options);
    }

    private async Task SettingsChangedAsync(Settings settings)
    {
        _settings = settings;
        await InvokeAsync(StateHasChanged);
    }

    private async Task SettingsDeletedAsync()
    {
        _settings = null;
        _conversation = Conversation.NewConversation(_userId);
        _userInput.Reset();
        _visualState.MemoryUpdate = null;
        await InvokeAsync(StateHasChanged);
        _settings = await settingsService.LoadAsync(_userId);
    }

    private async Task OpenImagePreviewAsync(string imageSource, string altText)
    {
        DialogParameters<ImagePreviewDialog> parameters = new()
        {
            { x => x.ImageSource, imageSource },
            { x => x.AltText, altText }
        };
        DialogOptions options = new()
        {
            CloseButton = true,
            FullScreen = true,
            FullWidth = true,
            MaxWidth = MaxWidth.ExtraExtraLarge
        };

        await dialogService.ShowAsync<ImagePreviewDialog>(altText, parameters, options);
    }

    private async Task HandleComposerKeyDownAsync(KeyboardEventArgs args)
    {
        if (!_visualState.IsSendingMessage && args is { Key: "Enter", ShiftKey: false })
        {
            await SendAsync();
        }
    }

    private async Task SelectFilesAsync(InputFileChangeEventArgs args)
    {
        List<UserInputAttachment> attachments = [];
        foreach (IBrowserFile file in args.GetMultipleFiles().Where(x => x.ContentType == "application/pdf" || x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            await using MemoryStream memoryStream = new();
            long maxAllowedSize = 20 * 1024 * 1024;
            await file.OpenReadStream(maxAllowedSize).CopyToAsync(memoryStream);
            byte[] fileBytes = memoryStream.ToArray();
            string? previewDataUri = null;
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                previewDataUri = $"data:{file.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
            }

            attachments.Add(new UserInputAttachment(file.Name, file.ContentType, fileBytes, previewDataUri));
        }

        _userInput.Attachments = attachments;
    }

    private async Task ToggleRecordingAsync()
    {
        if (_visualState.IsSendingMessage)
        {
            return;
        }

        _audioRecorderModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "/chatbotAudioRecorder.js");

        if (!_visualState.IsRecordingAudio)
        {
            //Start Recording
            await _audioRecorderModule.InvokeVoidAsync("startRecording");
            _visualState.IsRecordingAudio = true;
        }
        else
        {
            //Stop Recording (and transcribe)
            _visualState.IsTranscribingAudio = true;
            try
            {
                RecordedAudio? recordedAudio = await _audioRecorderModule.InvokeAsync<RecordedAudio?>("stopRecording");
                _visualState.IsRecordingAudio = false;

                if (recordedAudio is null)
                {
                }
                else
                {
                    IJSStreamReference audioStreamReference = await _audioRecorderModule.InvokeAsync<IJSStreamReference>("getRecordedAudioStream");
                    await using Stream audioStream = await audioStreamReference.OpenReadStreamAsync();

                    string transcription = await agentService.GenerateTranscriptionAsync(audioStream, recordedAudio.FileName);

                    _userInput.Text = string.IsNullOrWhiteSpace(_userInput.Text) ? transcription : $"{_userInput.Text.TrimEnd()} {transcription}";
                }
            }
            finally
            {
                _visualState.IsRecordingAudio = false;
                _visualState.IsTranscribingAudio = false;
            }
        }
    }

    private async Task ScrollMessagesToBottomAsync()
    {
        _scrollModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "/chatbotScroll.js");
        await _scrollModule.InvokeVoidAsync("scrollToBottom", _chatMessagesElement);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_audioRecorderModule is not null)
            {
                if (_visualState.IsRecordingAudio)
                {
                    await _audioRecorderModule.InvokeVoidAsync("cancelRecording");
                }

                await _audioRecorderModule.DisposeAsync();
            }

            if (_scrollModule is not null)
            {
                await _scrollModule.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            //Empty
        }
    }

    [UsedImplicitly]
    private sealed class RecordedAudio
    {
        public string FileName { get; [UsedImplicitly] set; } = string.Empty;
    }
}
