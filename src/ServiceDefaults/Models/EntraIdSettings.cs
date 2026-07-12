using JetBrains.Annotations;

namespace ServiceDefaults.Models;

[PublicAPI]
public class EntraIdSettings : ISectionSettings
{
    public static string SectionName => "EntraId";

    public required string Instance { get; set; }
    public required string TenantId { get; set; }
    public required string ClientId { get; set; }
    public required string CallbackPath { get; set; }
    public required string SignedOutCallbackPath { get; set; }
}
