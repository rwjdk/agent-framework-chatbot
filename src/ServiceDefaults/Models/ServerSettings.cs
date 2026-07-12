using JetBrains.Annotations;

namespace ServiceDefaults.Models;

[PublicAPI]
public class ServerSettings : ISectionSettings
{
    public static string SectionName => "Settings";

    public required bool UseImageGeneration { get; set; }
    public required bool UseUserMemory { get; set; }
    public required bool AllowMcpServers { get; set; }
    public required bool AllowCustomInstructions { get; set; }
    public required bool AllowChatVisualsCustomization { get; set; }
    public required bool AllowFileAttachments { get; set; }
    public required bool AllowAudioTranscription { get; set; }
}
