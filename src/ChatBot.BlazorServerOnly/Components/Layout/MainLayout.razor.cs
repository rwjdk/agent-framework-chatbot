using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatBot.BlazorServerOnly.Components.Layout;

[UsedImplicitly]
public partial class MainLayout(
    ILocalStorageService localStorageService) : LayoutComponentBase
{
    private bool IsDarkMode { get; set; }

    protected override async Task OnInitializedAsync()
    {
        IsDarkMode = await localStorageService.GetItemAsync<bool>(LocalStorageKeys.DarkMode);
    }

    private async Task ToggleDarkModeAsync()
    {
        IsDarkMode = !IsDarkMode;
        await localStorageService.SetItemAsync(LocalStorageKeys.DarkMode, IsDarkMode);
    }
}
