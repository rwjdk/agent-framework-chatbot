using JetBrains.Annotations;
using Newtonsoft.Json;

namespace ServiceDefaults.Models;

public class Settings
{

    public static Settings CreateDefault(string userId)
    {
        return new Settings
        {
            UserId = userId,
            Streaming = true,
            ShowReasoning = true,
            ShowTokens = true,
            ShowToolCalls = true,
            ShowMemoryUpdate = true,
            Instructions = "You are a Nice AI",
            UserMemories = [],
            McpServers = [],
        };
    }

    [JsonProperty("id")]
    [UsedImplicitly]
    public string Id => UserId;

    [JsonProperty("userId")]
    public required string UserId { get; init; }

    //Chat
    public bool Streaming { get; set; } = true;
    public bool ShowReasoning { get; set; } = true;
    public bool ShowTokens { get; set; } = true;
    public bool ShowToolCalls { get; set; } = true;
    public bool ShowMemoryUpdate { get; set; } = true;

    //Personalization
    public string Instructions { get; set; } = "You are a Nice AI";
    public List<string> UserMemories { get; set; } = [];

    //Integrations
    public List<McpServer> McpServers { get; set; } = [];
}
