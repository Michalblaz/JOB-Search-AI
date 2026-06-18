using Newtonsoft.Json;
using System;
using System.IO;

namespace MauiApp1.testowe
{
    public class AppSettings
    {
        public JobSourcesSettings JobSources { get; set; } = new();
        public DatabaseSettings Database { get; set; } = new();
        public GeminiSettings Gemini { get; set; } = new();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
    }

    public class JobSourcesSettings
    {
        public AdzunaSettings Adzuna { get; set; } = new();
        public JoobleSettings Jooble { get; set; } = new();
        public EPracaSettings EPraca { get; set; } = new();
    }

    public class AdzunaSettings
    {
        public string AppId { get; set; } = "49714c0c";
        public string AppKey { get; set; } = "0ec25da791293e6e9610698e1a2cd9a1";
    }

    public class JoobleSettings
    {
        public string ApiKey { get; set; } = "e999ac15-a94f-4a24-b23a-4ee2d305f732";
    }

    public class EPracaSettings
    {
        public string PartnerName { get; set; } = string.Empty;
    }

    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-2.5-flash-lite";
        public int MaxOffersPerRequest { get; set; } = 20;
        public int BatchSize { get; set; } = 5;
    }

    public class AppSettingsProvider
    {
        private const string SettingsFileName = "appsettings.json";
        private readonly IAppPackageFileProvider _packageFileProvider;
        private AppSettings? _cachedSettings;

        public AppSettingsProvider(IAppPackageFileProvider packageFileProvider)
        {
            _packageFileProvider = packageFileProvider;
        }

        public AppSettings GetSettings()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            var settings = LoadFromPackage();
            ApplyEnvironmentOverrides(settings);
            _cachedSettings = settings;
            return settings;
        }

        private AppSettings LoadFromPackage()
        {
            try
            {
                using var stream = _packageFileProvider.OpenRead(SettingsFileName);
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        private static void ApplyEnvironmentOverrides(AppSettings settings)
        {
            settings.Database.ConnectionString = ReadOverride("JOBSEARCH_DB_CONNECTION_STRING", settings.Database.ConnectionString);
            settings.JobSources.Adzuna.AppId = ReadOverride("JOBSEARCH_ADZUNA_APP_ID", settings.JobSources.Adzuna.AppId);
            settings.JobSources.Adzuna.AppKey = ReadOverride("JOBSEARCH_ADZUNA_APP_KEY", settings.JobSources.Adzuna.AppKey);
            settings.JobSources.Jooble.ApiKey = ReadOverride("JOBSEARCH_JOOBLE_API_KEY", settings.JobSources.Jooble.ApiKey);
            settings.JobSources.EPraca.PartnerName = ReadOverride("JOBSEARCH_EPRACA_PARTNER_NAME", settings.JobSources.EPraca.PartnerName);
            settings.Gemini.ApiKey = ReadOverride("JOBSEARCH_GEMINI_API_KEY", settings.Gemini.ApiKey);
            settings.Gemini.Model = ReadOverride("JOBSEARCH_GEMINI_MODEL", settings.Gemini.Model);
        }

        private static string ReadOverride(string variableName, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
