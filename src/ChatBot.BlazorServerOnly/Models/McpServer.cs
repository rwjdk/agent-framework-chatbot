namespace ChatBot.BlazorServerOnly.Models;

public class McpServer
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    public required Dictionary<string, string> Headers { get; set; }
}