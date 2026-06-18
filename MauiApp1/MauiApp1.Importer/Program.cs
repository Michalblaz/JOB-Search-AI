using Microsoft.Extensions.Configuration;

namespace MauiApp1.Importer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "JOBIMPORTER_")
                .Build();

            var settings = configuration.Get<ImporterSettings>() ?? new ImporterSettings();
            if (string.IsNullOrWhiteSpace(settings.Database.ConnectionString) ||
                settings.Database.ConnectionString.Contains("YOUR_SUPABASE_HOST", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Uzupełnij ConnectionString w appsettings.json albo przez zmienne środowiskowe.");
                return 1;
            }

            var coordinator = new JobImportCoordinator(settings);
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
}
