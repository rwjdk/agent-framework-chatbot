using ChatBot.BlazorServerOnly.Models;
using ChatBot.BlazorServerOnly.Services;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;

[UsedImplicitly]
public partial class SettingsDialog(UserPersonalizationService userPersonalizationService, ISnackbar snackbar)
{
    private string? _customInstructions;
    private List<McpServerEditor> _mcpServers = [];
    private string? _loadedUserId;
    private string? _mcpValidationMessage;
    private SettingsSection _selectedSection = SettingsSection.Chat;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter, EditorRequired]
    public required string UserId { get; set; }

    [Parameter, EditorRequired]
    public bool Streaming { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<bool> StreamingChanged { get; set; }

    [Parameter, EditorRequired]
    public ImageGenStyle SelectedImageGenStyle { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<ImageGenStyle> SelectedImageGenStyleChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (_loadedUserId == UserId)
        {
            return;
        }

        LoadPersonalization();
        _loadedUserId = UserId;
    }

    private void SelectSection(SettingsSection section)
    {
        _selectedSection = section;
        _mcpValidationMessage = null;
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

    private async Task SetStreamingAsync(bool streaming)
    {
        Streaming = streaming;
        await StreamingChanged.InvokeAsync(streaming);
    }

    private async Task SetImageGenStyleAsync(ImageGenStyle imageGenStyle)
    {
        SelectedImageGenStyle = imageGenStyle;
        await SelectedImageGenStyleChanged.InvokeAsync(imageGenStyle);
    }

    private void SaveCustomInstructions()
    {
        UserPersonalization personalization = GetOrCreatePersonalization();
        personalization.CustomerInstructions = _customInstructions;
        userPersonalizationService.SavePersonalization(UserId, personalization);
        snackbar.Add("Custom instructions saved.", Severity.Success);
    }

    private void AddMcpServer()
    {
        _mcpServers.Add(new());
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
        if (serverIndex >= 0 && serverIndex < _mcpServers.Count)
        {
            _mcpServers[serverIndex].Headers.Add(new());
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

    private void SaveMcpServers()
    {
        if (!TryValidateMcpServers(out string? validationMessage))
        {
            _mcpValidationMessage = validationMessage;
            return;
        }

        UserPersonalization personalization = GetOrCreatePersonalization();
        personalization.McpServers = _mcpServers.Select(ToMcpServer).ToList();
        userPersonalizationService.SavePersonalization(UserId, personalization);
        _mcpValidationMessage = null;
        snackbar.Add("MCP servers saved.", Severity.Success);
    }

    private void LoadPersonalization()
    {
        UserPersonalization? personalization = userPersonalizationService.GetPersonalization(UserId);
        _customInstructions = personalization?.CustomerInstructions;
        _mcpServers = personalization?.McpServers.Select(ToEditor).ToList() ?? [];
    }

    private UserPersonalization GetOrCreatePersonalization()
    {
        UserPersonalization? personalization = userPersonalizationService.GetPersonalization(UserId);
        personalization ??= new UserPersonalization
        {
            Memories = [],
            McpServers = []
        };
        return personalization;
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

    private static McpServerEditor ToEditor(McpServer server)
    {
        return new()
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
        return new()
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
        McpServers
    }

    private sealed class McpServerEditor
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<McpHeaderEditor> Headers { get; set; } = [];
    }

    private sealed class McpHeaderEditor
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
