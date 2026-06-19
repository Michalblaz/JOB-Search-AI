using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1.Importer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine("MauiApp1.Importer", "appsettings.json"), optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "JOBIMPORTER__")
                .Build();

            var settings = configuration.Get<ImporterSettings>() ?? new ImporterSettings();
            var configurationErrors = ValidateConfiguration(settings).ToList();
            if (configurationErrors.Any())
            {
                Console.WriteLine("Uzupełnij ConnectionString w appsettings.json albo przez zmienne środowiskowe.");
                foreach (var error in configurationErrors)
                {
                    Console.WriteLine($"- {error}");
                }

                return 1;
            }

            using var services = new ServiceCollection()
                .AddHttpClient("job-importer", client => client.Timeout = TimeSpan.FromSeconds(30))
                .Services
                .BuildServiceProvider();
            var coordinator = new JobImportCoordinator(settings, services.GetRequiredService<IHttpClientFactory>());
            var repository = new PostgresJobRepository(
                settings.Database.ConnectionString,
                settings.Import.DeactivateMissingOffers);

            if (args.Any(arg => string.Equals(arg, "--reclassify-existing", StringComparison.OrdinalIgnoreCase)))
            {
                var options = ParseReclassificationOptions(args);
                var reclassifiedCount = await repository.ReclassifyExistingOffersAsync(options);
                Console.WriteLine(options.DryRun
                    ? $"[DONE] Dry-run reclassification checked offers: {reclassifiedCount}"
                    : $"[DONE] Reclassified existing offers: {reclassifiedCount}");
                return 0;
            }

            if (args.Any(arg => string.Equals(arg, "--classification-stats", StringComparison.OrdinalIgnoreCase)))
            {
                var lines = await repository.GetClassificationStatsAsync();
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }

                return 0;
            }

            if (args.Any(arg => string.Equals(arg, "--classification-gaps", StringComparison.OrdinalIgnoreCase)))
            {
                var limit = ParseLimit(args, 80);
                var lines = await repository.GetClassificationGapReportAsync(limit);
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }

                return 0;
            }

            await repository.EnsureSourceIdsAsync();
            foreach (var sourceCode in coordinator.GetEnabledSourceCodes())
            {
                Console.WriteLine($"[START] {sourceCode}");
                var importContext = await repository.StartImportAsync(sourceCode);

                try
                {
                    var offers = await coordinator.FetchAsync(sourceCode);
                    var stats = await repository.UpsertOffersAsync(importContext, sourceCode, offers);
                    stats.SourceCode = sourceCode;
                    await repository.FinishImportAsync(importContext.ImportRunId, "success", stats);
                    Console.WriteLine($"[DONE] {sourceCode}: fetched={stats.FetchedCount}, inserted={stats.InsertedCount}, updated={stats.UpdatedCount}, deactivated={stats.DeactivatedCount}");
                }
                catch (Exception ex)
                {
                    await repository.FailImportAsync(importContext.ImportRunId, ex.Message);
                    Console.WriteLine($"[FAIL] {sourceCode}: {ex.Message}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd importera: {ex.Message}");
            return 1;
        }
    }

    private static IEnumerable<string> ValidateConfiguration(ImporterSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Database.ConnectionString) ||
            settings.Database.ConnectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Database:ConnectionString";
        }

        if (settings.Sources.Adzuna.Enabled)
        {
            if (string.IsNullOrWhiteSpace(settings.Sources.Adzuna.AppId))
            {
                yield return "Sources:Adzuna:AppId";
            }

            if (string.IsNullOrWhiteSpace(settings.Sources.Adzuna.AppKey))
            {
                yield return "Sources:Adzuna:AppKey";
            }
        }

        if (settings.Sources.Jooble.Enabled && string.IsNullOrWhiteSpace(settings.Sources.Jooble.ApiKey))
        {
            yield return "Sources:Jooble:ApiKey";
        }

        if (!settings.Sources.Adzuna.Enabled &&
            !settings.Sources.Jooble.Enabled &&
            !settings.Sources.Remotive.Enabled &&
            !settings.Sources.Arbeitnow.Enabled)
        {
            yield return "Sources: at least one source must be enabled";
        }
    }

    private static ReclassificationOptions ParseReclassificationOptions(string[] args)
    {
        var options = new ReclassificationOptions();
        foreach (var arg in args)
        {
            if (arg.StartsWith("--limit=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--limit=".Length..], out var limit) &&
                limit > 0)
            {
                options.Limit = limit;
            }
            else if (arg.StartsWith("--category=", StringComparison.OrdinalIgnoreCase))
            {
                options.CategoryCode = arg["--category=".Length..].Trim();
            }
            else if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.DryRun = true;
            }
            else if (string.Equals(arg, "--only-without-criteria", StringComparison.OrdinalIgnoreCase))
            {
                options.OnlyWithoutCriteria = true;
            }
        }

        return options;
    }

    private static int ParseLimit(string[] args, int defaultLimit)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--limit=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--limit=".Length..], out var limit) &&
                limit > 0)
            {
                return limit;
            }
        }

        return defaultLimit;
    }
}
