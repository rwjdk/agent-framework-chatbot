namespace ChatBot.BlazorServerOnly.Authentication.Login;

internal sealed record LoginProvider(string DisplayName, string LoginPath, string AuthenticationScheme, bool IsEnabled);
