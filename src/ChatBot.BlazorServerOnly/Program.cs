using System.Security.Claims;
using ChatBot.BlazorServerOnly.Components;
using Markdig;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using ServiceDefaults.Extensions;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); //From Aspire Service Defaults
builder.Services.AddLocalStorageServices();
builder.Services.AddMudServices();
builder.Services.AddSingleton(new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .DisableHtml()
    .Build());

//Auth (Start)
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(90);
    options.SlidingExpiration = true;
    options.Events.OnSigningIn = context =>
    {
        context.Properties.IsPersistent = true;
        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(options.ExpireTimeSpan);

        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();
//Auth (End)

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapGet("/attachments/{storedFileName}", async (string storedFileName, ClaimsPrincipal user, ServiceDefaults.Interfaces.IStorageService storageService) =>
{
    string userId = user.GetUserId();
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Forbid();
    }

    StoredFile? storedFile = await storageService.GetAttachmentAsync(userId, storedFileName);
    if (storedFile is null)
    {
        return Results.NotFound();
    }

    return Results.File(storedFile.Bytes, storedFile.ContentType);
}).RequireAuthorization();

app.MapGet("/generated-images/{storedFileName}", async (string storedFileName, ClaimsPrincipal user, IStorageService storageService) =>
{
    string userId = user.GetUserId();
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Forbid();
    }

    StoredFile? blobFile = await storageService.GetGeneratedImageAsync(userId, storedFileName);
    if (blobFile is null)
    {
        return Results.NotFound();
    }

    return Results.File(blobFile.Bytes, blobFile.ContentType);
}).RequireAuthorization();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.Run();
