using JetBrains.Annotations;

namespace ServiceDefaults.Models;

[PublicAPI]
public class McpServer
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    public required Dictionary<string, string> Headers { get; set; }
}