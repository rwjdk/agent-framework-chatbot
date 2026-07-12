using JetBrains.Annotations;

namespace ServiceDefaults.Models;

[PublicAPI]
public class Auth0Settings : ISectionSettings
{
    public static string SectionName => "Auth0";

    public required string Domain { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; } // Stored as the Auth0:ClientSecret user secret.
    public required string CallbackPath { get; set; }
}
