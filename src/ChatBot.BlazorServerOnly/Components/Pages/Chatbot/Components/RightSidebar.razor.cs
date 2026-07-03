using ChatBot.BlazorServerOnly.Models;
using ChatBot.BlazorServerOnly.Services;
using Microsoft.AspNetCore.Components;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;

public partial class RightSidebar(UserPersonalizationService userPersonalizationService)
{
    private string? _customInstructions;
    private List<McpServerEditor> _mcpServers = [];
    private bool _showMcpServers;
    private string? _loadedUserId;
    private string? _mcpValidationMessage;

    [Parameter,EditorRequired] public required string UserId { get; set; }

    [Parameter, EditorRequired] public bool Streaming { get; set; }

    [Parameter, EditorRequired] public EventCallback<bool> StreamingChanged { get; set; }

    [Parameter, EditorRequired] public ImageGenStyle SelectedImageGenStyle { get; set; }

    [Parameter, EditorRequired] public EventCallback<ImageGenStyle> SelectedImageGenStyleChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (_loadedUserId == UserId)
        {
            return;
        }

        LoadPersonalization();
        _loadedUserId = UserId;
    }

    private Task SetStreamingAsync(bool streaming)
    {
        return StreamingChanged.InvokeAsync(streaming);
    }

    private Task SetImageGenStyleAsync(ImageGenStyle imageGenStyle)
    {
        return SelectedImageGenStyleChanged.InvokeAsync(imageGenStyle);
    }

    private void SaveCustomInstructions()
    {
        UserPersonalization personalization = GetOrCreatePersonalization();
        personalization.CustomerInstructions = _customInstructions;
        userPersonalizationService.SavePersonalization(UserId, personalization);
    }

    private void ShowMcpServers()
    {
        LoadMcpServers();
        _showMcpServers = true;
        _mcpValidationMessage = null;
    }

    private void HideMcpServers()
    {
        _showMcpServers = false;
        _mcpValidationMessage = null;
    }

    private void AddMcpServer()
    {
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
        if (serverIndex >= 0 && serverIndex < _mcpServers.Count)
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
        _showMcpServers = false;
        _mcpValidationMessage = null;
    }

    private void LoadPersonalization()
    {
        UserPersonalization? personalization = userPersonalizationService.GetPersonalization(UserId);
        _customInstructions = personalization?.CustomerInstructions;
        _mcpServers = personalization?.McpServers.Select(ToEditor).ToList() ?? [];
    }

    private void LoadMcpServers()
    {
        UserPersonalization? personalization = userPersonalizationService.GetPersonalization(UserId);
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
