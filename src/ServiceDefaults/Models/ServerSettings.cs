namespace ServiceDefaults.Models;

public class ServerSettings
{
    public required bool UseImageGeneration { get; set; }
    public required bool UseUserMemory { get; set; }
    public required bool AllowMcpServers { get; set; }
    public required bool AllowCustomInstructions { get; set; }
    public required bool AllowChatVisualsCustomization { get; set; }
    public required bool AllowFileAttachments { get; set; }
    public required bool AllowAudioTranscription { get; set; }
}
