using JetBrains.Annotations;
using ChatBot.BlazorServerOnly.Services;
using Microsoft.AspNetCore.Components;

namespace ChatBot.BlazorServerOnly.Components.Layout;

[UsedImplicitly]
public partial class MainLayout(
    ThemeModeState themeModeState) : LayoutComponentBase, IDisposable
{
    protected override async Task OnInitializedAsync()
    {
        themeModeState.Changed += ThemeModeChanged;
        await themeModeState.InitializeAsync();
    }

    private void ThemeModeChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        themeModeState.Changed -= ThemeModeChanged;
    }
}
