using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;

[UsedImplicitly]
public partial class LeftSidebar(ConversationsService conversationsService, IDialogService dialogService)
{
    private List<Conversation> _conversations = [];
    private Guid? _openConversationMenuId;

    [Parameter]
    public EventCallback OnNewChat { get; set; }

    [Parameter]
    public EventCallback<Conversation> OnConversationSelected { get; set; }

    [Parameter]
    public EventCallback<Conversation> OnConversationDeleted { get; set; }

    [Parameter]
    public EventCallback OnSettings { get; set; }

    [Parameter]
    public string UserId { get; set; } = string.Empty;

    [Parameter]
    public bool IsDisabled { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            _conversations = [];
            return;
        }

        List<Conversation> conversations = await conversationsService.LoadPreviousConversationsAsync(UserId);
        _conversations = conversations.OrderByDescending(x => x.Id).ToList();
    }

    public void AddConversation(Conversation conversation)
    {
        _conversations.Insert(0, conversation);
        StateHasChanged();
    }

    private void ToggleConversationMenu(Conversation conversation)
    {
        if (IsDisabled)
        {
            return;
        }

        _openConversationMenuId = _openConversationMenuId == conversation.Id ? null : conversation.Id;
    }

    private async Task RenameConversationAsync(Conversation conversation)
    {
        if (IsDisabled)
        {
            return;
        }

        _openConversationMenuId = null;
        DialogParameters<RenameConversationDialog> parameters = new() { { x => x.Title, conversation.Title ?? string.Empty } };

        DialogOptions options = new()
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small
        };

        IDialogReference dialog = await dialogService.ShowAsync<RenameConversationDialog>("Rename conversation", parameters, options);
        DialogResult? result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not string newTitle || string.IsNullOrWhiteSpace(newTitle))
        {
            return;
        }

        conversation.Title = newTitle.Trim();
        await conversationsService.StoreConversationAsync(conversation);
        StateHasChanged();
    }

    private async Task ConfirmDeleteConversationAsync(Conversation conversation)
    {
        if (IsDisabled)
        {
            return;
        }

        _openConversationMenuId = null;
        DialogOptions options = new()
        {
            FullWidth = false,
            MaxWidth = MaxWidth.ExtraSmall
        };

        bool? deleteConversation = await dialogService.ShowMessageBoxAsync(
            "Delete conversation?",
            $"Delete \"{conversation.Title}\"? This cannot be undone.",
            yesText: "Yes",
            noText: "No",
            options: options);

        if (deleteConversation != true)
        {
            return;
        }

        await conversationsService.DeleteConversationAsync(UserId, conversation.Id);
        _conversations.Remove(conversation);
        await OnConversationDeleted.InvokeAsync(conversation);
    }
}
