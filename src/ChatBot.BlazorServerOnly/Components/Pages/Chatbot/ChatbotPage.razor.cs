using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.OpenAI;
using AgentFrameworkToolkit.Tools.Common;
using ChatBot.BlazorServerOnly.AIContextProviders;
using ChatBot.BlazorServerOnly.Extensions;
using ChatBot.BlazorServerOnly.Models;
using ChatBot.BlazorServerOnly.Services;
using ChatBot.BlazorServerOnly.Tools;
using Azure.AI.OpenAI;
using JetBrains.Annotations;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;
using OpenAI.Audio;
using System.ClientModel;
using AgentFrameworkToolkit.Tools;
using AgentFrameworkToolkit.Tools.ModelContextProtocol;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot;

[UsedImplicitly]
public partial class ChatbotPage(
    AzureOpenAIAgentFactory azureOpenAIAgentFactory,
    AIToolsFactory aiToolsFactory,
    ConversationsService conversationsService,
    FileUploadStorageService fileUploadStorageService,
    ConversationChatMessageMapper conversationChatMessageMapper,
    ILocalStorageService localStorageService,
    AuthenticationStateProvider authenticationStateProvider,
    UserPersonalizationService userPersonalizationService,
    OpenWeatherMapOptions openWeatherMapOptions,
    IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const long MaxAttachmentSize = 20 * 1024 * 1024;

    //Input and Conversation
    private string? _input;
    private List<PendingAttachment> _pendingFiles = [];
    private string _userId = string.Empty;
    private Conversation _conversation = Conversation.NewConversation(string.Empty);

    //Streaming and temp values
    private bool _streaming;
    private string? _streamedResponse;
    private string? _streamedReasoning;
    private List<AIContent> _streamedContent = [];
    private MemoryUpdate? _memoryUpdate;
    private bool _isRecordingAudio;
    private bool _isTranscribingAudio;

    //Options
    private ImageGenStyle _imageGenStyle;

    //Components
    private Components.LeftSidebar? _leftSidebar;
    private bool _inImageGenerationMode;
    private IJSObjectReference? _audioRecorderModule;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authenticationState.User.GetUserId();
        _conversation = Conversation.NewConversation(_userId);
        _streaming = await localStorageService.GetItemAsync<bool>(LocalStorageKeys.Streaming);
        _imageGenStyle = await localStorageService.GetItemAsync<ImageGenStyle>(LocalStorageKeys.ImageGenStyle);
    }

    private async Task SendAsync()
    {
        string? input = _input?.Trim();

        if (string.IsNullOrWhiteSpace(input) && _pendingFiles.Count == 0)
        {
            return;
        }
        input ??= string.Empty;

        if (_conversation.MissingATitle)
        {
            AzureOpenAIAgent titleGenerationAgent = azureOpenAIAgentFactory.CreateAgent(OpenAIChatModels.Gpt41Nano);
            string message = $"Given the following message: '{GetTitleSource(input)}' generate a max 25 char long title for this question";
            AgentResponse<string> response = await titleGenerationAgent.RunAsync<string>(message);
            _conversation.Title = response.Result;
            _leftSidebar?.AddConversation(_conversation);
        }

        List<ConversationAttachment> attachments = await SavePendingFilesAsync();
        ResetMidTurnValues();
        _memoryUpdate = null;
        _conversation.AddUserMessage(input, attachments);
        await InvokeAsync(StateHasChanged);

        switch (_imageGenStyle)
        {
            case ImageGenStyle.RouterAgent:
                {
                    AzureOpenAIAgent routerAgent = azureOpenAIAgentFactory.CreateAgent(new AgentOptions
                    {
                        ClientType = ClientType.ChatClient,
                        Model = OpenAIChatModels.Gpt5Mini,
                        ReasoningEffort = OpenAIReasoningEffort.Low,
                        Instructions = "You are a router-agent determining what task the user is asking (being either generating an image (use can say show image, generate image, draw image, render image) or being a normal chatbot). If you are at all in doubt, go the chatbot route"
                    });

                    AgentResponse<TaskType> routerResponse = await routerAgent.RunAsync<TaskType>(await conversationChatMessageMapper.ToChatMessagesAsync(_conversation));
                    switch (routerResponse.Result)
                    {
                        case TaskType.GenerateImageRoute:
                            await DoImageGenerationAsync();
                            break;
                        case TaskType.ChatBotRoute:
                            await AnswerWithChatbotAsync();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                break;
            case ImageGenStyle.ImageGenAsTool:
                await AnswerWithChatbotAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await InvokeAsync(StateHasChanged);
        await conversationsService.StoreConversationAsync(_conversation);
    }

    private async Task DoImageGenerationAsync()
    {
        _inImageGenerationMode = true;
        await InvokeAsync(StateHasChanged);
        string imageGenerationPrompt = _conversation.GetAsImageGenerationPrompt();
        await new ImageGenerationTool(azureOpenAIAgentFactory, _conversation).GenerateImageAsync(imageGenerationPrompt);
        ResetMidTurnValues();
    }

    private async Task AnswerWithChatbotAsync()
    {
        ImageGenerationTool imageGenerationTool = new(azureOpenAIAgentFactory, _conversation);

        AzureOpenAIAgent memoryExtractorAgent = azureOpenAIAgentFactory.CreateAgent(new AgentOptions
        {
            Model = OpenAIChatModels.Gpt41Mini,
            Instructions = "Look at the user's message and extract any user-facts that we do not already know about the user. Facts are names, places, likes, dislikes, or anything the user prefix with 'Remember this' (or non if there aren't any memories to store)"
        });

        List<AITool> tools =
        [
            WeatherTools.GetWeatherForCity(openWeatherMapOptions),
            AIFunctionFactory.Create(imageGenerationTool.GenerateImageAsync, "generate_image")
        ];

        List<McpClientTools> mcpClientToolsList = await AddMcpToolsAsync();
        foreach (McpClientTools mcpClientTools in mcpClientToolsList)
        {
            tools.AddRange(mcpClientTools.Tools);
        }

        AzureOpenAIAgent agent = azureOpenAIAgentFactory.CreateAgent(new AgentOptions
        {
            ClientType = ClientType.ResponsesApi,
            Model = OpenAIChatModels.Gpt5Mini,
            ReasoningEffort = OpenAIReasoningEffort.Medium,
            ReasoningSummaryVerbosity = OpenAIReasoningSummaryVerbosity.Detailed,
            Tools = tools,
            Instructions = "You are a chatbot answering questions",
            AIContextProviders = [new PersonalizationContextProvider(memoryExtractorAgent, _userId, userPersonalizationService, MemoryUpdateNotificationAsync)]
        });

        if (!_streaming)
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
        UserPersonalization? userPersonalization = userPersonalizationService.GetPersonalization(_userId);
        if (userPersonalization != null)
        {
            foreach (McpServer mcpServer in userPersonalization.McpServers)
            {
                mcpTools.Add(await aiToolsFactory.GetToolsFromRemoteMcpAsync(mcpServer.Url, mcpServer.Headers));
            }
        }
        return mcpTools;
    }

    private async Task MemoryUpdateNotificationAsync(MemoryUpdate obj)
    {
        _memoryUpdate = obj;
        await InvokeAsync(StateHasChanged);
    }

    private async Task GenerateNonStreamingResponseAsync(AzureOpenAIAgent agent)
    {
        List<ChatMessage> chatMessages = await conversationChatMessageMapper.ToChatMessagesAsync(_conversation);
        AgentResponse response = await agent.RunAsync(chatMessages);
        _conversation.AddDataFromAgentResponse(response);
    }

    private async Task GenerateStreamingResponseAsync(AzureOpenAIAgent agent)
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
                        _streamedReasoning += textReasoningContent.Text;
                        break;
                    default:
                        _streamedContent.Add(content);
                        break;
                }
            }

            _streamedResponse += update.Text;
            await InvokeAsync(StateHasChanged);
        }

        ResetMidTurnValues();
        AgentResponse response = updates.ToAgentResponse();
        _conversation.AddDataFromAgentResponse(response);
    }

    private void NewChat()
    {
        _conversation = Conversation.NewConversation(_userId);
        ResetMidTurnValues();
        _memoryUpdate = null;
    }

    private void SwitchSession(Conversation conversation)
    {
        _conversation = conversation;
        ResetMidTurnValues();
        _memoryUpdate = null;
    }

    private async Task SetStreamingAsync(bool streaming)
    {
        _streaming = streaming;
        await localStorageService.SetItemAsync(LocalStorageKeys.Streaming, streaming);
    }

    private async Task SetImageGenStyleAsync(ImageGenStyle imageGenStyle)
    {
        _imageGenStyle = imageGenStyle;
        await localStorageService.SetItemAsync(LocalStorageKeys.ImageGenStyle, imageGenStyle);
    }

    private async Task SelectFilesAsync(InputFileChangeEventArgs args)
    {
        List<PendingAttachment> pendingFiles = [];
        foreach (IBrowserFile file in args.GetMultipleFiles().Where(IsSupportedFile))
        {
            await using MemoryStream memoryStream = new();
            await file.OpenReadStream(MaxAttachmentSize).CopyToAsync(memoryStream);
            byte[] fileBytes = memoryStream.ToArray();
            string? previewDataUri = null;
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                previewDataUri = $"data:{file.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
            }

            pendingFiles.Add(new PendingAttachment(file.Name, file.ContentType, fileBytes, previewDataUri));
        }

        _pendingFiles = pendingFiles;
    }

    private async Task ToggleRecordingAsync()
    {
        _audioRecorderModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "/chatbotAudioRecorder.js");

        if (!_isRecordingAudio)
        {
            //Start Recording
            await _audioRecorderModule.InvokeVoidAsync("startRecording");
            _isRecordingAudio = true;
        }
        else
        {
            //Stop Recording (and transcribe)
            _isTranscribingAudio = true;
            try
            {
                RecordedAudio? recordedAudio = await _audioRecorderModule.InvokeAsync<RecordedAudio?>("stopRecording");
                _isRecordingAudio = false;

                if (recordedAudio is null)
                {
                }
                else
                {
                    IJSStreamReference audioStreamReference = await _audioRecorderModule.InvokeAsync<IJSStreamReference>("getRecordedAudioStream");
                    await using Stream audioStream = await audioStreamReference.OpenReadStreamAsync();

                    AzureOpenAIClient client = azureOpenAIAgentFactory.Connection.GetClient();
                    AudioClient audioClient = client.GetAudioClient("gpt-4o-mini-transcribe");

                    ClientResult<AudioTranscription> audioTranscription = await audioClient.TranscribeAudioAsync(
                        audioStream,
                        recordedAudio.FileName,
                        new AudioTranscriptionOptions());

                    string transcription = audioTranscription.Value.Text.Trim();

                    _input = string.IsNullOrWhiteSpace(_input)
                        ? transcription
                        : $"{_input.TrimEnd()} {transcription}";
                }
            }
            finally
            {
                _isRecordingAudio = false;
                _isTranscribingAudio = false;
            }
        }
    }

    private async Task<List<ConversationAttachment>> SavePendingFilesAsync()
    {
        List<ConversationAttachment> attachments = [];
        foreach (PendingAttachment file in _pendingFiles)
        {
            attachments.Add(await fileUploadStorageService.SaveAsync(_userId, file.FileName, file.ContentType, file.Bytes));
        }

        return attachments;
    }

    private string GetTitleSource(string input)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return string.Join(", ", _pendingFiles.Select(x => x.FileName));
    }

    private static bool IsSupportedFile(IBrowserFile file)
    {
        return file.ContentType == "application/pdf" || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetMidTurnValues()
    {
        _input = null;
        _pendingFiles = [];
        _streamedReasoning = null;
        _streamedResponse = null;
        _streamedContent = [];
        _inImageGenerationMode = false;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_audioRecorderModule is not null)
            {
                if (_isRecordingAudio)
                {
                    await _audioRecorderModule.InvokeVoidAsync("cancelRecording");
                }

                await _audioRecorderModule.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            //Empty
        }
    }

    private sealed class PendingAttachment(string fileName, string contentType, byte[] bytes, string? previewDataUri)
    {
        public string FileName { get; } = fileName;
        public string ContentType { get; } = contentType;
        public byte[] Bytes { get; } = bytes;
        public string? PreviewDataUri { get; } = previewDataUri;
    }

    [UsedImplicitly]
    private sealed class RecordedAudio
    {
        public string FileName { get; [UsedImplicitly] set; } = string.Empty;
    }
}