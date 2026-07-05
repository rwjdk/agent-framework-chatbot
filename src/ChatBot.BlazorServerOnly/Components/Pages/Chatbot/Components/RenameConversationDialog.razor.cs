using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChatBot.BlazorServerOnly.Components.Pages.Chatbot.Components;

[UsedImplicitly]
public partial class RenameConversationDialog
{
    private string _title = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    public string Title { get; set; } = string.Empty;

    protected override void OnParametersSet()
    {
        _title = Title;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_title))
        {
            return;
        }

        MudDialog?.Close(DialogResult.Ok(_title.Trim()));
    }

    private void Cancel()
    {
        MudDialog?.Cancel();
    }
}
