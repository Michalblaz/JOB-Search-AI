using MauiApp1.Importer;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var languageOffer = ImporterHelpers.DetectLanguagesWithLevels(
    "Backend Developer",
    "Requirements: English B2 required. Nice to have: communicative German.");
Assert(languageOffer.Any(x => x.Code == "en" && x.LevelMin == "B2" && x.IsRequired == true), "English B2 required was not detected.");
Assert(languageOffer.Any(x => x.Code == "de" && x.LevelMin == "B1" && x.IsRequired == false), "Optional communicative German was not detected.");

var goWithoutTechContext = JobClassificationRules.Classify(
    "Pracownik biurowy",
    "Do zadan nalezy obsluga dokumentow i go live procesu administracyjnego.",
    Array.Empty<string>());
Assert(!goWithoutTechContext.CriterionHits.Any(x => x.Code == "go"), "Short alias 'go' matched without tech context.");

var goWithTechContext = JobClassificationRules.Classify(
    "Go Developer",
    "Requirements: Go, SQL and Docker in backend software projects.",
    Array.Empty<string>());
Assert(goWithTechContext.Criteria.Any(x => x.Code == "go" && x.IsRequired), "Go was not detected in IT context.");

var workMode = ImporterHelpers.DetectWorkModeInfo("Tester", "Praca hybrydowa, 2 dni w biurze.", "Rzeszow", Array.Empty<string>(), false);
Assert(workMode.WorkMode == "hybrid", "Hybrid work mode was not detected.");

var workTime = ImporterHelpers.DetectWorkTimeInfo("Sprzedawca", "Pelny etat, 40 godzin tygodniowo.", "pelny etat", Array.Empty<string>());
Assert(workTime.WorkTimeType is "full_time" or "full_or_part", "Full-time work was not detected.");

var salary = ImporterHelpers.DetectSalaryInfo(8000, 12000, "8000-12000 PLN brutto miesiecznie");
Assert(salary.SalaryPeriod == "month", "Monthly salary period was not detected.");
Assert(salary.SalaryTaxType == "gross", "Gross salary tax type was not detected.");

Console.WriteLine("Importer rule tests passed.");
