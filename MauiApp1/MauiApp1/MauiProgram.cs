using MauiApp1.testowe;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace MauiApp1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });
            builder.Services.AddMudServices();
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<IAppSessionStore, MauiSessionStore>();
            builder.Services.AddSingleton<IAppDataPathProvider, MauiAppDataPathProvider>();
            builder.Services.AddSingleton<IAppPackageFileProvider, MauiAppPackageFileProvider>();
            builder.Services.AddSingleton<IUrlLauncher, MauiUrlLauncher>();
            builder.Services.AddSingleton<AppSettingsProvider>();
            builder.Services.AddSingleton<IUserStore, PostgresUserStore>();
            builder.Services.AddSingleton<PostgresJobReader>();
            builder.Services.AddSingleton<JobSearchService>();
            builder.Services.AddSingleton<GeminiMatchService>();
            return builder.Build();
        }
    }
}
