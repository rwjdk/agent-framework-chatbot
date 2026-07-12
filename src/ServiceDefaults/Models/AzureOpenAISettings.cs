using JetBrains.Annotations;

namespace ServiceDefaults.Models;

[PublicAPI]
public class AzureOpenAISettings : ISectionSettings
{
    public static string SectionName => "AzureOpenAI";

    public required string Endpoint { get; set; }
    public required string Key { get; set; } // Stored as the AzureOpenAI:Key user secret.
}
