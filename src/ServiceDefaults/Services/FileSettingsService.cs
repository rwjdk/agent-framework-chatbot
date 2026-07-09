using System.Text.Json;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;

namespace ServiceDefaults.Services;

public class FileSettingsService : ISettingsService
{
    public async Task<Settings> LoadAsync(string userId)
    {
        string path = GetSettingsPath(userId);
        if (!File.Exists(path))
        {
            Settings settings = Settings.CreateDefault(userId);
            await SaveAsync(settings);
        }
        string json = await File.ReadAllTextAsync(path);

        return JsonSerializer.Deserialize<Settings>(json)!;
    }

    public async Task SaveAsync(Settings settings)
    {
        string path = GetSettingsPath(settings.UserId);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(settings));
    }

    public Task DeleteSettingsAsync(string userId)
    {
        string path = GetSettingsPath(userId);
        File.Delete(path);
        return Task.CompletedTask;
    }
    
    private static string GetSettingsPath(string userId)
    {
        string folder = Path.Combine(GetRootFolder(), "settings");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{userId}.json");
    }

    private static string GetRootFolder()
    {
        return Path.Combine(Path.GetTempPath(), "chatbot");
    }
}
