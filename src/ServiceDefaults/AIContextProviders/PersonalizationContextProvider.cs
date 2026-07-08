using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

namespace ServiceDefaults.AIContextProviders;

internal class PersonalizationContextProvider(
    AgentService agentService,
    string userId,
    SettingsService settingsService,
    Func<MemoryUpdate, Task> memoryUpdateNotification) : AIContextProvider
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        Settings settings = await settingsService.LoadAsync(userId);

        string? instructions = null;
        if (!string.IsNullOrWhiteSpace(settings.Instructions))
        {
            instructions += $"<personal_instructions>{settings.Instructions}</personal_instructions>";
        }

        if (settings.UserMemories.Count > 0)
        {
            IEnumerable<string> memories = settings.UserMemories.Select(x => $"<memory>{x}</memory>");
            instructions += $"<personal_memories>{string.Join("", memories)}</personal_memories>";
        }

        return new AIContext
        {
            Instructions = instructions
        };
    }

    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        Settings settings = await settingsService.LoadAsync(userId);

        //Todo - Fix that system save too much in memory! (aka example image gen requests or just saying hi)

        ChatMessage lastMessageFromUser = context.RequestMessages.Last();
        List<ChatMessage> inputToMemoryExtractor =
        [
            new(ChatRole.Assistant, $"Already know user-facts: [{string.Join(" | ", settings.UserMemories)}] (do not extracts these again)"),
            lastMessageFromUser
        ];

        MemoryUpdate memoryUpdate = await agentService.GetMemoryUpdatesAsync(inputToMemoryExtractor);
        if (memoryUpdate.MemoryToAdd.Count > 0 || memoryUpdate.MemoryToRemove.Count > 0)
        {
            foreach (string memoryToRemove in memoryUpdate.MemoryToRemove)
            {
                settings.UserMemories.Remove(memoryToRemove);
            }

            foreach (string newMemory in memoryUpdate.MemoryToAdd)
            {
                if (!settings.UserMemories.Contains(newMemory))
                {
                    settings.UserMemories.Add(newMemory);
                }
            }
            await settingsService.SaveAsync(settings);

            await memoryUpdateNotification.Invoke(memoryUpdate);
        }
    }
}
