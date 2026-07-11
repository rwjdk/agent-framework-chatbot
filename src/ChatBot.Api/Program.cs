using ChatBot.Api.Authentication;
using ChatBot.Api.Endpoints;
using Microsoft.AspNetCore.Http.Features;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
IReadOnlyList<LoginProvider> loginProviders = builder.AddChatBotAuthentication();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 210 * 1024 * 1024;
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapChatBotAuthentication(loginProviders);
app.MapChatBotApi();
app.MapDefaultEndpoints();

app.Run();
