using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.Tools.Common;
using ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;
using ChatBot.BlazorServerOnly.Extensions;
using ChatBot.BlazorServerOnly.Services;
using ChatBot.BlazorServerOnly.Tools;
using JetBrains.Annotations;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;
using MudBlazor;
using AgentFrameworkToolkit.Tools;
using AgentFrameworkToolkit.Tools.ModelContextProtocol;
using ChatBot.BlazorServerOnly.Models;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot;

[UsedImplicitly]
public partial class ChatbotPage(
    AzureOpenAIAgentFactory azureOpenAIAgentFactory,
    AIToolsFactory aiToolsFactory,
    ConversationsService conversationsService,
    AgentService agentService,
    SettingsService settingsService,
    FileUploadStorageService fileUploadStorageService,
    ConversationChatMessageMapper conversationChatMessageMapper,
    AuthenticationStateProvider authenticationStateProvider,
    OpenWeatherMapOptions openWeatherMapOptions,
    ThemeModeState themeModeState,
    IJSRuntime jsRuntime,
    IDialogService dialogService,
    ISnackbar snackbar) : IAsyncDisposable
{
    //Input and Conversation
    private readonly UserInput _userInput = new();
    private readonly State _state = new();


    private string _userId = string.Empty;
    private Conversation _conversation = Conversation.NewConversation(string.Empty);
    private Settings? _settings;

    //Streaming and temp values
    

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
        await themeModeState.InitializeAsync();
    }

    private async Task SendAsync()
    {
        if (_state.IsSendingMessage)
        {
            return;
        }

        string? input = _userInput.Text?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        _state.IsSendingMessage = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            if (_conversation.MissingATitle)
            {
                _conversation.Title = await agentService.GenerateTitleAsync(input);
                _leftSidebar?.AddConversation(_conversation);
            }

            List<ConversationAttachment> attachments = await SavePendingFilesAsync();
            ResetMidTurnValues();
            _state.MemoryUpdate = null;
            _conversation.AddUserMessage(input, attachments);
            await InvokeAsync(StateHasChanged);
            await ScrollMessagesToBottomAsync();
            await AnswerAsync();
            await conversationsService.StoreConversationAsync(_conversation);
        }
        catch (Exception exception)
        {
            string error = !string.IsNullOrWhiteSpace(exception.Message) ? $"Failed to send message: {exception.Message}" : "Failed to send message.";
            snackbar.Add(error, Severity.Error);
        }
        finally
        {
            _state.IsSendingMessage = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task AnswerAsync()
    {
        if (_settings is null)
        {
            return;
        }

        List<AITool> tools =
        [
            AIFunctionFactory.Create(new ImageGenerationTool(azureOpenAIAgentFactory, _conversation).GenerateImageAsync, "generate_image"),
            WeatherTools.GetWeatherForCity(openWeatherMapOptions)
        ];

        List<McpClientTools> mcpClientToolsList = await AddMcpToolsAsync();
        foreach (McpClientTools mcpClientTools in mcpClientToolsList)
        {
            tools.AddRange(mcpClientTools.Tools);
        }

        AIAgent agent = agentService.GetMainAgent(_userId, tools, _settings.Instructions, MemoryUpdateNotificationAsync); //todo... own more of tool-generation?

        if (!_settings.Streaming)
        {
            await GenerateNonStreamingResponseAsync(agent);
        }
        else
        {
            await GenerateStreamingResponseAsync(agent);
        }

        //MCP Tools Cleanup
        foreach (McpClientTools mcpClientTools in mcpClientToolsList)
        {
            await mcpClientTools.McpClient.DisposeAsync();
        }
    }

    private async Task<List<McpClientTools>> AddMcpToolsAsync()
    {
        List<McpClientTools> mcpTools = [];
        if (_settings != null)
        {
            foreach (McpServer mcpServer in _settings.McpServers)
            {
                mcpTools.Add(await aiToolsFactory.GetToolsFromRemoteMcpAsync(mcpServer.Url, mcpServer.Headers));
            }
        }
        return mcpTools;
    }

    private async Task MemoryUpdateNotificationAsync(MemoryUpdate obj)
    {
        _state.MemoryUpdate = obj;
        if (_settings is not null)
        {
            foreach (string memoryToRemove in obj.MemoryToRemove)
            {
                _settings.UserMemories.Remove(memoryToRemove);
            }

            foreach (string memoryToAdd in obj.MemoryToAdd)
            {
                if (!_settings.UserMemories.Contains(memoryToAdd))
                {
                    _settings.UserMemories.Add(memoryToAdd);
                }
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task GenerateNonStreamingResponseAsync(AIAgent agent)
    {
        List<ChatMessage> chatMessages = await conversationChatMessageMapper.ToChatMessagesAsync(_conversation);
        AgentResponse response = await agent.RunAsync(chatMessages);
        _conversation.AddDataFromAgentResponse(response);
    }

    private async Task GenerateStreamingResponseAsync(AIAgent agent)
    {
        List<AgentResponseUpdate> updates = [];
        List<ChatMessage> chatMessages = await conversationChatMessageMapper.ToChatMessagesAsync(_conversation);
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(chatMessages))
        {
            updates.Add(update);
            foreach (AIContent content in update.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent textReasoningContent:
                        _state.StreamedReasoning += textReasoningContent.Text;
                        break;
                    default:
                        _state.StreamedContents.Add(content);
                        break;
                }
            }

            _state.StreamedResponse += update.Text;
            await InvokeAsync(StateHasChanged);
        }

        ResetMidTurnValues();
        AgentResponse response = updates.ToAgentResponse();
        _conversation.AddDataFromAgentResponse(response);
    }

    private void NewChat()
    {
        if (_state.IsSendingMessage)
        {
            return;
        }

        _conversation = Conversation.NewConversation(_userId);
        ResetMidTurnValues();
        _state.MemoryUpdate = null;
    }

    private void SwitchSession(Conversation conversation)
    {
        if (_state.IsSendingMessage)
        {
            return;
        }

        _conversation = conversation;
        ResetMidTurnValues();
        _state.MemoryUpdate = null;
    }

    private void RemoveSession(Conversation conversation)
    {
        if (_conversation.Id == conversation.Id)
        {
            NewChat();
        }
    }

    private async Task ToggleDarkModeAsync()
    {
        if (_state.IsSendingMessage)
        {
            return;
        }

        await themeModeState.ToggleAsync();
    }

    private string GetThemeToggleIcon()
    {
        if (themeModeState.IsDarkMode)
        {
            return Icons.Material.Filled.LightMode;
        }

        return Icons.Material.Filled.DarkMode;
    }

    private string GetThemeToggleText()
    {
        if (themeModeState.IsDarkMode)
        {
            return "Switch to light mode";
        }

        return "Switch to dark mode";
    }

    private async Task OpenSettingsDialogAsync()
    {
        if (_state.IsSendingMessage || _settings is null)
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
        ResetMidTurnValues();
        _state.MemoryUpdate = null;
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
        if (!_state.IsSendingMessage && args is { Key: "Enter", ShiftKey: false })
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
        if (_state.IsSendingMessage)
        {
            return;
        }

        _audioRecorderModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "/chatbotAudioRecorder.js");

        if (!_state.IsRecordingAudio)
        {
            //Start Recording
            await _audioRecorderModule.InvokeVoidAsync("startRecording");
            _state.IsRecordingAudio = true;
        }
        else
        {
            //Stop Recording (and transcribe)
            _state.IsTranscribingAudio = true;
            try
            {
                RecordedAudio? recordedAudio = await _audioRecorderModule.InvokeAsync<RecordedAudio?>("stopRecording");
                _state.IsRecordingAudio = false;

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
                _state.IsRecordingAudio = false;
                _state.IsTranscribingAudio = false;
            }
        }
    }

    private async Task ScrollMessagesToBottomAsync()
    {
        _scrollModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "/chatbotScroll.js");
        await _scrollModule.InvokeVoidAsync("scrollToBottom", _chatMessagesElement);
    }

    private async Task<List<ConversationAttachment>> SavePendingFilesAsync()
    {
        List<ConversationAttachment> attachments = [];
        foreach (UserInputAttachment file in _userInput.Attachments)
        {
            attachments.Add(await fileUploadStorageService.SaveAsync(_userId, file.FileName, file.ContentType, file.Bytes));
        }

        return attachments;
    }
    
    private void ResetMidTurnValues()
    {
        _userInput.Reset();
        _state.ResetStreamingValues();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_audioRecorderModule is not null)
            {
                if (_state.IsRecordingAudio)
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