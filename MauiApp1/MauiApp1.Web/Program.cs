using MauiApp1.Components;
using MauiApp1.testowe;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAppSessionStore, WebSessionStore>();
builder.Services.AddSingleton<IAppDataPathProvider, WebAppDataPathProvider>();
builder.Services.AddSingleton<IAppPackageFileProvider, WebAppPackageFileProvider>();
builder.Services.AddScoped<IUrlLauncher, WebUrlLauncher>();
builder.Services.AddSingleton<AppSettingsProvider>();
builder.Services.AddSingleton<IUserStore, PostgresUserStore>();
builder.Services.AddSingleton<PostgresJobReader>();
builder.Services.AddScoped<JobSearchService>();
builder.Services.AddScoped<GeminiMatchService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
