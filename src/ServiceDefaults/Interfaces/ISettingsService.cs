using ServiceDefaults.Models;

namespace ServiceDefaults.Interfaces;

public interface ISettingsService
{
    Task<Settings> LoadAsync(string userId);
    Task SaveAsync(Settings settings);
    Task DeleteSettingsAsync(string userId);
}