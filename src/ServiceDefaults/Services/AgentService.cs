using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.OpenAI;
using AgentFrameworkToolkit.Tools.ModelContextProtocol;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Audio;
using ServiceDefaults.AIContextProviders;
using ServiceDefaults.Constants;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;
using System.ClientModel;
using AgentFrameworkToolkit.Tools;
using AgentFrameworkToolkit.Tools.Common;
using Microsoft.Extensions.Configuration;
using OpenAI;
using ServiceDefaults.Tools;

namespace ServiceDefaults.Services;

public class AgentService(
    AzureOpenAIAgentFactory azureOpenAIAgentFactory,
    ISettingsService settingsService,
    IStorageService storageService,
    AIToolsFactory aiToolsFactory,
    IConfiguration configuration,
    ServerSettings serverSettings)
{
    public async Task<string> GenerateTitleAsync(string firstChatMessage)
    {
        AIAgent agent = azureOpenAIAgentFactory.CreateAgent(new AgentOptions
        {
            Model = AIModelIds.TitleGenerationModel,
            Instructions = "You are a conversation title generator"
        });

        string message = $"""
                           Given the following first chat message in a conversation: 
                           <message>{firstChatMessage}</message>
                           Generate a title that represent the what the conversation is about (Max 25 char long)
                           """;
        
        AgentResponse<string> response = await agent.RunAsync<string>(message);
        
        return response.Result;
    }

    public async Task<MemoryUpdate> GetMemoryUpdatesAsync(List<ChatMessage> inputToMemoryExtractor)
    {
        if (!serverSettings.UseUserMemory)
        {
            return new MemoryUpdate(null, null);
        }

        AzureOpenAIAgent agent = azureOpenAIAgentFactory.CreateAgent(new AgentOptions
        {
            Model = AIModelIds.MemoryModel,
            ReasoningEffort = OpenAIReasoningEffort.Low,
            Instructions = """
                           Your job is to extract facts about the user that is not yet known:
                           - Facts are names, places, likes and dislikes
                           - Not all messages contains user-facts so 100% OK to not find any facts
                           - If you are in doubt that something is a user-fact or not, consider it not to be and do not include
                           """
        });

        AgentResponse<MemoryUpdate> response = await agent.RunAsync<MemoryUpdate>(inputToMemoryExtractor);
        return response.Result;
    }

    public AIAgent GetMainAgent(string userId, IList<McpClientTools> mcpClientTools, Conversation conversation, string instructions, Func<MemoryUpdate, Task> memoryUpdateNotification)
    {
        //Prepare Regular Tools
        List<AITool> tools = [..TimeTools.All()];
        if (serverSettings.UseImageGeneration)
        {
            tools.AddRange(aiToolsFactory.GetTools(new ImageGenerationTool(azureOpenAIAgentFactory, conversation, storageService)));
        }

        string? weatherServiceKey = configuration[SecretKeys.WeatherServiceKey];
        if (weatherServiceKey != null && !weatherServiceKey.Equals("None", StringComparison.InvariantCultureIgnoreCase))
        {
            AITool weatherTool = WeatherTools.GetWeatherForCity(new OpenWeatherMapOptions
            {
                ApiKey = weatherServiceKey
            });
            tools.Add(weatherTool);
        }

        //Prepare MCP Tools
        if (serverSettings.AllowMcpServers)
        {
            foreach (McpClientTools mcpClientTool in mcpClientTools)
            {
                tools.AddRange(mcpClientTool.Tools);
            }
        }

        List<AIContextProvider> aiContextProviders = [];
        if (serverSettings.UseUserMemory)
        {
            aiContextProviders.Add(new PersonalizationContextProvider(this, userId, settingsService, memoryUpdateNotification));
        }

        return azureOpenAIAgentFactory.CreateAgent(new AgentOptions
        {
            ClientType = ClientType.ResponsesApi,
            Model = AIModelIds.Model,
            ReasoningEffort = OpenAIReasoningEffort.Medium,
            ReasoningSummaryVerbosity = OpenAIReasoningSummaryVerbosity.Detailed,
            Tools = tools,
            Instructions = serverSettings.AllowCustomInstructions ? instructions : null,
            AIContextProviders = aiContextProviders
        });
    }

    public async Task<string> GenerateTranscriptionAsync(Stream audioStream, string filename)
    {
        if (!serverSettings.AllowAudioTranscription)
        {
            throw new InvalidOperationException("Audio transcription is disabled by server settings.");
        }

        OpenAIClient client = azureOpenAIAgentFactory.Connection.GetClient();
        AudioClient audioClient = client.GetAudioClient(AIModelIds.TranscribeModel);
        ClientResult<AudioTranscription> audioTranscription = await audioClient.TranscribeAudioAsync(
            audioStream,
            filename,
            new AudioTranscriptionOptions());

        return audioTranscription.Value.Text.Trim();
    }
}
