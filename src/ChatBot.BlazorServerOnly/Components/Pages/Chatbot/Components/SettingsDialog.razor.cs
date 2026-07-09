using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;

[UsedImplicitly]
public partial class SettingsDialog(
    ISettingsService settingsService,
    IConversationsService conversationsService,
    IDialogService dialogService,
    ServerSettings serverSettings,
    ISnackbar snackbar)
{
    private Settings? _settings;
    private List<MemoryEditor> _memories = [];
    private List<McpServerEditor> _mcpServers = [];
    private string? _loadedUserId;
    private string? _memoryValidationMessage;
    private string? _mcpValidationMessage;
    private SettingsSection _selectedSection = SettingsSection.Chat;
    private ServerSettings ServerSettings => serverSettings;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter, EditorRequired]
    public required string UserId { get; set; }

    [Parameter, EditorRequired]
    public required Settings Settings { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<Settings> SettingsChanged { get; set; }

    [Parameter, EditorRequired]
    public EventCallback SettingsDeleted { get; set; }

    protected override void OnParametersSet()
    {
        if (_loadedUserId == UserId)
        {
            return;
        }

        _settings = CloneSettings(Settings);
        _memories = _settings.UserMemories.Select(x => new MemoryEditor
        {
            Value = x
        }).ToList();
        _mcpServers = _settings.McpServers.Select(ToEditor).ToList();
        if (!IsSectionAvailable(_selectedSection))
        {
            _selectedSection = GetDefaultSection();
        }

        _loadedUserId = UserId;
    }

    private void SelectSection(SettingsSection section)
    {
        if (!IsSectionAvailable(section))
        {
            return;
        }

        _selectedSection = section;
        _memoryValidationMessage = null;
        _mcpValidationMessage = null;
    }

    private SettingsSection GetDefaultSection()
    {
        if (IsSectionAvailable(SettingsSection.Chat))
        {
            return SettingsSection.Chat;
        }

        if (IsSectionAvailable(SettingsSection.Instructions))
        {
            return SettingsSection.Instructions;
        }

        if (IsSectionAvailable(SettingsSection.Memories))
        {
            return SettingsSection.Memories;
        }

        if (IsSectionAvailable(SettingsSection.McpServers))
        {
            return SettingsSection.McpServers;
        }

        return SettingsSection.DangerZone;
    }

    private bool IsSectionAvailable(SettingsSection section)
    {
        return section switch
        {
            SettingsSection.Chat => ServerSettings.AllowChatVisualsCustomization,
            SettingsSection.Instructions => ServerSettings.AllowCustomInstructions,
            SettingsSection.Memories => ServerSettings.UseUserMemory,
            SettingsSection.McpServers => ServerSettings.AllowMcpServers,
            _ => true
        };
    }

    private Variant GetSectionButtonVariant(SettingsSection section)
    {
        if (_selectedSection == section)
        {
            return Variant.Filled;
        }

        return Variant.Text;
    }

    private Color GetSectionButtonColor(SettingsSection section)
    {
        if (_selectedSection == section)
        {
            return Color.Primary;
        }

        return Color.Default;
    }

    private string GetSectionButtonClass(SettingsSection section)
    {
        if (_selectedSection == section)
        {
            return "settings-section-button active";
        }

        return "settings-section-button";
    }

    private async Task SaveChatSettingsAsync()
    {
        if (!ServerSettings.AllowChatVisualsCustomization)
        {
            return;
        }

        await SaveCurrentSettingsAsync("Chat settings saved.");
    }

    private async Task SaveInstructionsAsync()
    {
        if (!ServerSettings.AllowCustomInstructions)
        {
            return;
        }

        await SaveCurrentSettingsAsync("Instructions saved.");
    }

    private void AddMemory()
    {
        if (!ServerSettings.UseUserMemory)
        {
            return;
        }

        _memories.Add(new MemoryEditor());
        _memoryValidationMessage = null;
    }

    private void RemoveMemory(int index)
    {
        if (index >= 0 && index < _memories.Count)
        {
            _memories.RemoveAt(index);
            _memoryValidationMessage = null;
        }
    }

    private async Task SaveMemoriesAsync()
    {
        if (_settings is null || !ServerSettings.UseUserMemory)
        {
            return;
        }

        if (!TryValidateMemories(out string? validationMessage))
        {
            _memoryValidationMessage = validationMessage;
            return;
        }

        _settings.UserMemories = _memories.Select(x => x.Value.Trim()).ToList();
        _memoryValidationMessage = null;
        await SaveCurrentSettingsAsync("Memories saved.");
    }

    private void AddMcpServer()
    {
        if (!ServerSettings.AllowMcpServers)
        {
            return;
        }

        _mcpServers.Add(new McpServerEditor());
        _mcpValidationMessage = null;
    }

    private void RemoveMcpServer(int index)
    {
        if (index >= 0 && index < _mcpServers.Count)
        {
            _mcpServers.RemoveAt(index);
        }
    }

    private void AddHeader(int serverIndex)
    {
        if (ServerSettings.AllowMcpServers && serverIndex >= 0 && serverIndex < _mcpServers.Count)
        {
            _mcpServers[serverIndex].Headers.Add(new McpHeaderEditor());
            _mcpValidationMessage = null;
        }
    }

    private void RemoveHeader(int serverIndex, int headerIndex)
    {
        if (serverIndex >= 0 && serverIndex < _mcpServers.Count && headerIndex >= 0 && headerIndex < _mcpServers[serverIndex].Headers.Count)
        {
            _mcpServers[serverIndex].Headers.RemoveAt(headerIndex);
        }
    }

    private async Task SaveIntegrationsAsync()
    {
        if (_settings is null || !ServerSettings.AllowMcpServers)
        {
            return;
        }

        if (!TryValidateMcpServers(out string? validationMessage))
        {
            _mcpValidationMessage = validationMessage;
            return;
        }

        _settings.McpServers = _mcpServers.Select(ToMcpServer).ToList();
        _mcpValidationMessage = null;
        await SaveCurrentSettingsAsync("Integrations saved.");
    }

    private async Task SaveCurrentSettingsAsync(string message)
    {
        if (_settings is null)
        {
            return;
        }

        await settingsService.SaveAsync(_settings);
        Settings changedSettings = CloneSettings(_settings);
        await SettingsChanged.InvokeAsync(changedSettings);
        snackbar.Add(message, Severity.Success);
    }

    private async Task DeleteSettingsAndConversationsAsync()
    {
        DialogOptions options = new()
        {
            FullWidth = false,
            MaxWidth = MaxWidth.ExtraSmall
        };

        bool? deleteData = await dialogService.ShowMessageBoxAsync(
            "Delete settings and conversations?",
            "This permanently deletes your settings and conversations. This cannot be undone.",
            yesText: "Delete",
            noText: "Cancel",
            options: options);

        if (deleteData != true)
        {
            return;
        }

        await conversationsService.DeleteUserConversationsAsync(UserId);
        await settingsService.DeleteSettingsAsync(UserId);
        snackbar.Add("Settings and conversations deleted.", Severity.Success);
        MudDialog?.Close();
        await SettingsDeleted.InvokeAsync();
    }

    private bool TryValidateMcpServers(out string? validationMessage)
    {
        for (int serverIndex = 0; serverIndex < _mcpServers.Count; serverIndex++)
        {
            McpServerEditor server = _mcpServers[serverIndex];
            if (string.IsNullOrWhiteSpace(server.Name) || string.IsNullOrWhiteSpace(server.Url))
            {
                validationMessage = $"Server {serverIndex + 1} must have a name and URL.";
                return false;
            }

            List<string> headerKeys = server.Headers
                .Select(x => x.Key.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (server.Headers.Any(x => string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value)))
            {
                validationMessage = $"Server {serverIndex + 1} has a header value without a key.";
                return false;
            }

            if (headerKeys.Count != headerKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                validationMessage = $"Server {serverIndex + 1} has duplicate header keys.";
                return false;
            }
        }

        validationMessage = null;
        return true;
    }

    private bool TryValidateMemories(out string? validationMessage)
    {
        if (_memories.Any(x => string.IsNullOrWhiteSpace(x.Value)))
        {
            validationMessage = "Memories cannot be blank.";
            return false;
        }

        List<string> memories = _memories
            .Select(x => x.Value.Trim())
            .ToList();

        if (memories.Count != memories.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            validationMessage = "Duplicate memories are not allowed.";
            return false;
        }

        validationMessage = null;
        return true;
    }

    private static Settings CloneSettings(Settings settings)
    {
        return new Settings
        {
            UserId = settings.UserId,
            Streaming = settings.Streaming,
            ShowReasoning = settings.ShowReasoning,
            ShowTokens = settings.ShowTokens,
            ShowToolCalls = settings.ShowToolCalls,
            ShowMemoryUpdate = settings.ShowMemoryUpdate,
            Instructions = settings.Instructions,
            UserMemories = settings.UserMemories.ToList(),
            McpServers = settings.McpServers.Select(CloneMcpServer).ToList(),
        };
    }

    private static McpServer CloneMcpServer(McpServer server)
    {
        return new McpServer
        {
            Name = server.Name,
            Url = server.Url,
            Headers = server.Headers.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static McpServerEditor ToEditor(McpServer server)
    {
        return new McpServerEditor
        {
            Name = server.Name,
            Url = server.Url,
            Headers = server.Headers.Select(x => new McpHeaderEditor
            {
                Key = x.Key,
                Value = x.Value
            }).ToList()
        };
    }

    private static McpServer ToMcpServer(McpServerEditor editor)
    {
        return new McpServer
        {
            Name = editor.Name.Trim(),
            Url = editor.Url.Trim(),
            Headers = editor.Headers
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(x => x.Key.Trim(), x => x.Value.Trim(), StringComparer.OrdinalIgnoreCase)
        };
    }

    private void CloseSettings()
    {
        MudDialog?.Close();
    }

    private enum SettingsSection
    {
        Chat,
        Instructions,
        Memories,
        McpServers,
        DangerZone
    }

    private sealed class MemoryEditor
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class McpServerEditor
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<McpHeaderEditor> Headers { get; init; } = [];
    }

    private sealed class McpHeaderEditor
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
