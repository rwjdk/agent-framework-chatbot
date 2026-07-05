using Microsoft.JSInterop;

namespace ChatBot.BlazorServerOnly.Services;

public sealed class ThemeModeState(ILocalStorageService localStorageService)
{
    private bool _initialized;

    public event Action? Changed;

    public bool IsDarkMode { get; private set; }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        IsDarkMode = await localStorageService.GetItemAsync<bool>(LocalStorageKeys.DarkMode);
        _initialized = true;
    }

    public async Task ToggleAsync()
    {
        IsDarkMode = !IsDarkMode;
        await localStorageService.SetItemAsync(LocalStorageKeys.DarkMode, IsDarkMode);
        Changed?.Invoke();
    }
}
