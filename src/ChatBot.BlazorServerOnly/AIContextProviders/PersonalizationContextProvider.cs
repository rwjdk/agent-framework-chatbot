using ChatBot.BlazorServerOnly.Models;
using ChatBot.BlazorServerOnly.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ChatBot.BlazorServerOnly.AIContextProviders;

internal class PersonalizationContextProvider(AIAgent memoryExtractorAgent, string userId, UserPersonalizationService userPersonalizationService, Func<MemoryUpdate, Task> memoryUpdateNotification) : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        UserPersonalization? personalization = userPersonalizationService.GetPersonalization(userId);

        string? instructions = null;
        if (!string.IsNullOrWhiteSpace(personalization?.CustomerInstructions))
        {
            instructions += $"<personal_instructions>{personalization.CustomerInstructions}</personal_instructions>";
        }

        if (personalization?.Memories.Count > 0)
        {
            IEnumerable<string> memories = personalization.Memories.Select(x => $"<memory>{x.ToString()}</memory>");
            instructions += $"<personal_memories>{string.Join("", memories)}</personal_memories>";
        }

        return ValueTask.FromResult(new AIContext
        {
            Instructions = instructions
        });
    }

    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        UserPersonalization? personalization = userPersonalizationService.GetPersonalization(userId);

        personalization ??= new UserPersonalization
        {
            Memories = [],
            McpServers = []
        };

        ChatMessage lastMessageFromUser = context.RequestMessages.Last();
        List<ChatMessage> inputToMemoryExtractor =
        [
            new(ChatRole.Assistant, $"We know the following about the user already and should not extract that again: {string.Join(" | ", personalization.Memories)}"),
            lastMessageFromUser
        ];

        AgentResponse<MemoryUpdate> response = await memoryExtractorAgent.RunAsync<MemoryUpdate>(inputToMemoryExtractor, cancellationToken: cancellationToken);

        MemoryUpdate memoryUpdate = response.Result;
        if (memoryUpdate.MemoryToAdd.Count > 0 || memoryUpdate.MemoryToRemove.Count > 0)
        {
            foreach (string memoryToRemove in memoryUpdate.MemoryToRemove)
            {
                personalization.Memories.Remove(memoryToRemove);
            }

            foreach (string newMemory in memoryUpdate.MemoryToAdd)
            {
                if (!personalization.Memories.Contains(newMemory))
                {
                    personalization.Memories.Add(newMemory);
                }
            }
            userPersonalizationService.SavePersonalization(userId, personalization);

            await memoryUpdateNotification.Invoke(memoryUpdate);
        }
    }
}
