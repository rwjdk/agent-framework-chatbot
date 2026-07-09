using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.OpenAI;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Audio;
using ServiceDefaults.AIContextProviders;
using ServiceDefaults.Constants;
using ServiceDefaults.Models;
using System.ClientModel;
using ServiceDefaults.Interfaces;

namespace ServiceDefaults.Services;

public class AgentService(AzureOpenAIAgentFactory azureOpenAIAgentFactory, ISettingsService settingsService)
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
        //todo: Better prompt engineering
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

    public AIAgent GetMainAgent(string userId, IList<AITool> tools, string instructions, Func<MemoryUpdate, Task> memoryUpdateNotification)
    {
        return azureOpenAIAgentFactory.CreateAgent(new AgentOptions
        {
            ClientType = ClientType.ResponsesApi,
            Model = AIModelIds.Model,
            ReasoningEffort = OpenAIReasoningEffort.Medium,
            ReasoningSummaryVerbosity = OpenAIReasoningSummaryVerbosity.Detailed,
            Tools = tools,
            Instructions = instructions,
            AIContextProviders = [new PersonalizationContextProvider(this, userId, settingsService, memoryUpdateNotification)]
        });
    }

    public async Task<string> GenerateTranscriptionAsync(Stream audioStream, string filename)
    {
        AzureOpenAIClient client = azureOpenAIAgentFactory.Connection.GetClient();
        AudioClient audioClient = client.GetAudioClient(AIModelIds.TranscribeModel);
        ClientResult<AudioTranscription> audioTranscription = await audioClient.TranscribeAudioAsync(
            audioStream,
            filename,
            new AudioTranscriptionOptions());

        return audioTranscription.Value.Text.Trim();
    }
}