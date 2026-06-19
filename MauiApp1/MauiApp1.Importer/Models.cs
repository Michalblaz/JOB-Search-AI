using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MauiApp1.Importer;

public sealed class ImporterSettings
{
    public DatabaseSettings Database { get; set; } = new();
    public ImportSettings Import { get; set; } = new();
    public SourceSettingsContainer Sources { get; set; } = new();
}

public static class ExtractorVersionProvider
{
    public const string Current = "rules_v3";
}

public sealed class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class ImportSettings
{
    public int MaxPagesPerQuery { get; set; } = 3;
    public int AdzunaResultsPerPage { get; set; } = 20;
    public int JoobleResultsPerPage { get; set; } = 20;
    public bool DeactivateMissingOffers { get; set; }
    public List<string> DefaultQueries { get; set; } = new();
    public List<string> Locations { get; set; } = new();
}

public sealed class SourceSettingsContainer
{
    public AdzunaSettings Adzuna { get; set; } = new();
    public JoobleSettings Jooble { get; set; } = new();
    public ApiSourceSettings Remotive { get; set; } = new();
    public ApiSourceSettings Arbeitnow { get; set; } = new();
}

public sealed class AdzunaSettings
{
    public bool Enabled { get; set; }
    public string Code { get; set; } = "adzuna";
    public string AppId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
}

public sealed class JoobleSettings
{
    public bool Enabled { get; set; }
    public string Code { get; set; } = "jooble";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class ApiSourceSettings
{
    public bool Enabled { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Wspólny model oferty pracy przygotowany do zapisu w bazie importera.
/// </summary>
/// <remarks>
/// Różne API zwracają inne nazwy pól, formaty lokalizacji, wynagrodzeń i dat. Ten model jest granicą normalizacji, dzięki której
/// <see cref="PostgresJobRepository"/> może zapisywać wszystkie źródła jednym mechanizmem.
/// </remarks>
public sealed class NormalizedJobOffer
{
    public string SourceCode { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? CompanyLogoUrl { get; set; }
    public string? LocationName { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? CountryCode { get; set; }
    public string? Description { get; set; }
    public string DescriptionQuality { get; set; } = "full";
    public string? DescriptionShort { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryRaw { get; set; }
    public bool? SalaryIsPredicted { get; set; }
    public string? EmploymentType { get; set; }
    public string? ContractType { get; set; }
    public string? EmploymentTypeRaw { get; set; }
    public string? ContractTypeRaw { get; set; }
    public WorkModeInfo WorkMode { get; set; } = new();
    public WorkTimeInfo WorkTime { get; set; } = new();
    public SalaryInfo SalaryInfo { get; set; } = new();
    public string? ExperienceLevel { get; set; }
    public string? EducationLevel { get; set; }
    public ExperienceInfo Experience { get; set; } = new();
    public EducationInfo Education { get; set; } = new();
    public decimal DataQualityScore { get; set; }
    public decimal ExtractionScore { get; set; }
    public bool IsRemote { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public string RawPayloadJson { get; set; } = string.Empty;
    public OfferClassification Classification { get; set; } = new();
    public List<OfferLanguage> Languages { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<OfferContractType> ContractTypes { get; set; } = new();
    public List<OfferScheduleFlag> ScheduleFlags { get; set; } = new();
    public List<OfferBenefit> Benefits { get; set; } = new();
    public List<OfferDomain> Domains { get; set; } = new();
    public List<OfferFormalRequirement> FormalRequirements { get; set; } = new();
}

public sealed class ExperienceInfo
{
    public string Level { get; set; } = "Wszystkie";
    public decimal? MinYears { get; set; }
    public decimal? MaxYears { get; set; }
    public bool? IsRequired { get; set; }
    public bool NoExperienceAllowed { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
}

public sealed class EducationInfo
{
    public string Level { get; set; } = "Brak wymagań lub niewymagane";
    public string? Field { get; set; }
    public bool? IsRequired { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
}

public sealed class WorkModeInfo
{
    public string WorkMode { get; set; } = "unknown";
    public string RemoteScope { get; set; } = "unknown";
    public string? RemoteCountryRestriction { get; set; }
    public decimal? OfficeDaysPerWeekMin { get; set; }
    public decimal? OfficeDaysPerWeekMax { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
}

public sealed class WorkTimeInfo
{
    public string WorkTimeType { get; set; } = "unknown";
    public decimal? FteMin { get; set; }
    public decimal? FteMax { get; set; }
    public decimal? HoursPerWeekMin { get; set; }
    public decimal? HoursPerWeekMax { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
}

public sealed class SalaryInfo
{
    public string SalaryPeriod { get; set; } = "unknown";
    public string SalaryTaxType { get; set; } = "unknown";
    public string SalaryRateType { get; set; } = "unknown";
    public decimal? ParsedMin { get; set; }
    public decimal? ParsedMax { get; set; }
    public string? ParsedCurrency { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
}

public sealed class OfferContractType
{
    public string ContractType { get; set; } = "unknown";
    public bool IsPrimary { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string SourceField { get; set; } = "description";
    public string? SourceSection { get; set; }
}

public sealed class OfferScheduleFlag
{
    public string ScheduleFlag { get; set; } = string.Empty;
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string SourceField { get; set; } = "description";
    public string? SourceSection { get; set; }
}

public sealed class OfferBenefit
{
    public string BenefitCode { get; set; } = string.Empty;
    public string BenefitName { get; set; } = string.Empty;
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string SourceField { get; set; } = "description";
    public string? SourceSection { get; set; }
}

public sealed class OfferDomain
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string SourceField { get; set; } = "description";
    public string? SourceSection { get; set; }
    public string ExtractorVersion { get; set; } = ExtractorVersionProvider.Current;
}

public sealed class OfferFormalRequirement
{
    public string RequirementCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string SourceField { get; set; } = "description";
    public string? SourceSection { get; set; }
    public string ExtractorVersion { get; set; } = ExtractorVersionProvider.Current;
}

public sealed class OfferClassification
{
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public string? RoleCode { get; set; }
    public string? RoleName { get; set; }
    public decimal RoleConfidence { get; set; } = 0.5m;
    public string? RoleEvidence { get; set; }
    public List<OfferCriterion> Criteria { get; set; } = new();
    public List<OfferCriterion> CriterionHits { get; set; } = new();
}

public sealed class OfferCriterion
{
    public string Kind { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string RequirementLevel { get; set; } = "unknown";
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string? SourceField { get; set; }
    public string? SourceSection { get; set; }
    public int? EvidenceStart { get; set; }
    public int? EvidenceEnd { get; set; }
    public string? MatchedAlias { get; set; }
    public string ExtractorVersion { get; set; } = ExtractorVersionProvider.Current;
}

public sealed class OfferLanguage
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LevelMin { get; set; } = "unknown";
    public bool? IsRequired { get; set; }
    public decimal Confidence { get; set; } = 0.5m;
    public string? Evidence { get; set; }
    public string SourceField { get; set; } = "description";
    public string? SourceSection { get; set; }
    public string ExtractorVersion { get; set; } = ExtractorVersionProvider.Current;
}

public sealed class CriterionAliasSeed
{
    public string Kind { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public bool IsShortAmbiguous { get; set; }
    public bool RequiresTechContext { get; set; }
    public bool RequiresWholeToken { get; set; } = true;
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

public sealed class MatchScoreInfo
{
    public long JobOfferId { get; set; }
    public long? UserId { get; set; }
    public decimal TotalScore { get; set; }
    public decimal SalaryScore { get; set; }
    public decimal LocationScore { get; set; }
    public decimal SkillsScore { get; set; }
    public decimal ExperienceScore { get; set; }
    public decimal EducationScore { get; set; }
    public decimal LanguageScore { get; set; }
    public decimal ContractScore { get; set; }
    public decimal WorkModeScore { get; set; }
    public decimal WorkTimeScore { get; set; }
    public decimal BenefitsScore { get; set; }
    public decimal ScheduleScore { get; set; }
    public int MissingRequiredSkillsCount { get; set; }
    public int MatchedRequiredSkillsCount { get; set; }
    public int MatchedOptionalSkillsCount { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public sealed class SourceImportStats
{
    public string SourceCode { get; set; } = string.Empty;
    public int FetchedCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeactivatedCount { get; set; }
}

public sealed class SourceImportContext
{
    public int SourceId { get; set; }
    public long ImportRunId { get; set; }
}

public sealed class ReclassificationOptions
{
    public int? Limit { get; set; }
    public string? CategoryCode { get; set; }
    public bool DryRun { get; set; }
    public bool OnlyWithoutCriteria { get; set; }
}

/// <summary>
/// Zestaw funkcji normalizujących tekst oferty, lokalizację i sygnały dopasowania podczas importu.
/// </summary>
/// <remarks>
/// Helpery są celowo bezstanowe, ponieważ działają na pojedynczych polach z API i muszą być łatwe do użycia w mapowaniu różnych źródeł.
/// </remarks>
/// <seealso cref="JobImportCoordinator"/>
public static class ImporterHelpers
{
    /// <summary>
    /// Skraca i czyści opis oferty do wersji wygodnej do szybkiego wyświetlania.
    /// </summary>
    /// <param name="description">Pełny opis HTML lub tekst pobrany ze źródła.</param>
    /// <returns>Opis bez tagów HTML, ograniczony do krótkiego fragmentu.</returns>
    public static string BuildShortDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Brak opisu oferty.";
        }

        var normalized = Regex.Replace(description.Replace("\r", " ").Replace("\n", " "), "<.*?>", " ");
        normalized = System.Net.WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"\s{2,}", " ").Trim();

        return normalized.Length <= 300 ? normalized : normalized[..300].Trim() + "...";
    }

    /// <summary>
    /// Wylicza skrót treści używany do wykrywania zmian w ofercie między importami.
    /// </summary>
    /// <param name="value">Zserializowana treść źródłowej oferty.</param>
    /// <returns>Szesnastkowy skrót SHA-256.</returns>
    public static string ComputeContentHash(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Dzieli tekst lokalizacji na miasto, region i kraj, jeśli źródło zwróciło wartości rozdzielone przecinkami.
    /// </summary>
    /// <param name="location">Surowy tekst lokalizacji ze źródła zewnętrznego.</param>
    /// <returns>Krotka z rozpoznanymi częściami lokalizacji; brakujące elementy są <see langword="null"/>.</returns>
    public static (string? City, string? Region, string? Country) SplitLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return (null, null, null);
        }

        var parts = location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 3 => (parts[0], parts[1], parts[2]),
            2 => (parts[0], parts[1], null),
            _ => (parts[0], null, null)
        };
    }

    public static string? NormalizeCountryCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 10)
        {
            return trimmed;
        }

        if (trimmed.Contains("pol", StringComparison.OrdinalIgnoreCase))
        {
            return "PL";
        }

        if (trimmed.Contains("german", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("deutsch", StringComparison.OrdinalIgnoreCase))
        {
            return "DE";
        }

        if (trimmed.Contains("france", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("franc", StringComparison.OrdinalIgnoreCase))
        {
            return "FR";
        }

        if (trimmed.Contains("ukraine", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("ukrai", StringComparison.OrdinalIgnoreCase))
        {
            return "UA";
        }

        return trimmed[..10];
    }

    /// <summary>
    /// Rozpoznaje poziom doświadczenia na podstawie tytułu i opisu oferty.
    /// </summary>
    /// <param name="title">Tytuł stanowiska.</param>
    /// <param name="description">Opis stanowiska.</param>
    /// <returns>Jedna z etykiet używanych później przez filtry aplikacji.</returns>
    /// <example>
    /// <code>
    /// var level = ImporterHelpers.DetectExperienceLevel("Junior .NET Developer", description);
    /// </code>
    /// </example>
    public static ExperienceInfo DetectExperienceInfo(string? title, string? description)
    {
        var titleText = NormalizeForDetection(title);
        var descriptionText = NormalizeForDetection(description);
        var text = $"{titleText} {descriptionText}".Trim();

        var noExperienceMatch = MatchFirst(text,
            @"\b(bez doswiadczenia|brak doswiadczenia|nie wymagamy doswiadczenia|doswiadczenie (nie jest wymagane|niewymagane)|pelne szkolenie|zapewniamy szkolenie|przyuczenie|entry[- ]?level|no experience required|no prior experience|berufseinstieg|quereinsteiger|ohne berufserfahrung)\b");

        if (noExperienceMatch.Success)
        {
            return new ExperienceInfo
            {
                Level = "Brak doświadczenia",
                MinYears = 0,
                MaxYears = 0,
                IsRequired = false,
                NoExperienceAllowed = true,
                Confidence = 0.95m,
                Evidence = ExtractEvidence(text, noExperienceMatch)
            };
        }

        var germanRangeMatch = MatchFirst(text,
            @"(?<min>\d+(?:[,.]\d+)?)\s*(?:-|bis|to)\s*(?<max>\d+(?:[,.]\d+)?)\s*jahre\s+berufserfahrung");

        if (germanRangeMatch.Success && TryGetDecimal(germanRangeMatch, "min", out var germanRangeMin) && TryGetDecimal(germanRangeMatch, "max", out var germanRangeMax))
        {
            return new ExperienceInfo
            {
                Level = MapYearsToExperienceLevel(germanRangeMin),
                MinYears = germanRangeMin,
                MaxYears = germanRangeMax,
                IsRequired = ResolveRequirementFromContext(text, germanRangeMatch),
                NoExperienceAllowed = germanRangeMin <= 0,
                Confidence = 0.88m,
                Evidence = ExtractEvidence(text, germanRangeMatch)
            };
        }

        var germanMinMatch = MatchFirst(text,
            @"(?:mindestens)\s*(?<min>\d+(?:[,.]\d+)?)\s*jahre",
            @"(?<min>\d+(?:[,.]\d+)?)\+\s*jahre\s+berufserfahrung",
            @"(?<min>\d+(?:[,.]\d+)?)\s*jahre\s+berufserfahrung");

        if (germanMinMatch.Success && TryGetDecimal(germanMinMatch, "min", out var germanMinYears))
        {
            return new ExperienceInfo
            {
                Level = MapYearsToExperienceLevel(germanMinYears),
                MinYears = germanMinYears,
                IsRequired = ResolveRequirementFromContext(text, germanMinMatch),
                NoExperienceAllowed = germanMinYears <= 0,
                Confidence = 0.86m,
                Evidence = ExtractEvidence(text, germanMinMatch)
            };
        }

        var rangeMatch = MatchFirst(text,
            @"(?<min>\d+(?:[,.]\d+)?)\s*(?:-|–|—|do|to)\s*(?<max>\d+(?:[,.]\d+)?)\s*(?:lat|lata|roku|years?)\s+(?:komercyjnego\s+)?doswiadczenia",
            @"(?<min>\d+(?:[,.]\d+)?)\s*(?:-|–|—|do|to)\s*(?<max>\d+(?:[,.]\d+)?)\s*(?:years?)\s+of\s+experience");

        if (rangeMatch.Success && TryGetDecimal(rangeMatch, "min", out var rangeMin) && TryGetDecimal(rangeMatch, "max", out var rangeMax))
        {
            return new ExperienceInfo
            {
                Level = MapYearsToExperienceLevel(rangeMin),
                MinYears = rangeMin,
                MaxYears = rangeMax,
                IsRequired = ResolveRequirementFromContext(text, rangeMatch),
                NoExperienceAllowed = rangeMin <= 0,
                Confidence = 0.9m,
                Evidence = ExtractEvidence(text, rangeMatch)
            };
        }

        var minMatch = MatchFirst(text,
            @"(?:minimum|min\.?|co najmniej|przynajmniej|at least)\s*(?<min>\d+(?:[,.]\d+)?)\s*(?:lat|lata|roku|years?)",
            @"(?<min>\d+(?:[,.]\d+)?)\+\s*(?:lat|lata|roku|years?)\s+(?:komercyjnego\s+)?doswiadczenia",
            @"(?<min>\d+(?:[,.]\d+)?)\s*(?:lat|lata|roku|years?)\s+(?:komercyjnego\s+)?doswiadczenia");

        if (minMatch.Success && TryGetDecimal(minMatch, "min", out var minYears))
        {
            return new ExperienceInfo
            {
                Level = MapYearsToExperienceLevel(minYears),
                MinYears = minYears,
                IsRequired = ResolveRequirementFromContext(text, minMatch),
                NoExperienceAllowed = minYears <= 0,
                Confidence = 0.88m,
                Evidence = ExtractEvidence(text, minMatch)
            };
        }

        var optionalExperienceMatch = MatchFirst(text,
            @"\b(mile widziane|dodatkowy atut|atutem bedzie|nice to have|optional|preferred|would be a plus|is a plus|von vorteil|wunsch|idealerweise)\b.{0,120}\b(doswiadczenie|experience|berufserfahrung)\b");

        if (optionalExperienceMatch.Success)
        {
            return new ExperienceInfo
            {
                Level = "Doświadczenie mile widziane",
                IsRequired = false,
                Confidence = 0.78m,
                Evidence = ExtractEvidence(text, optionalExperienceMatch)
            };
        }

        var titleLevel = DetectExperienceLevelFromTitle(titleText);
        return titleLevel ?? new ExperienceInfo();
    }

    public static string DetectExperienceLevel(string? title, string? description)
        => DetectExperienceInfo(title, description).Level;

    private static string DetectExperienceLevelLegacy(string? title, string? description)
    {
        var text = $"{title} {description}";
        if (Regex.IsMatch(text, @"\b(manager|menadżer|kierownik|lead|head of)\b", RegexOptions.IgnoreCase))
        {
            return "Menadżer";
        }

        if (Regex.IsMatch(text, @"\b(senior|sr\.?|starszy specjalista)\b", RegexOptions.IgnoreCase))
        {
            return "Starszy specjalista";
        }

        if (Regex.IsMatch(text, @"\b(junior|jr\.?|młodszy|asystent)\b", RegexOptions.IgnoreCase))
        {
            return "Młodszy specjalista";
        }

        if (Regex.IsMatch(text, @"\b(staż|praktyk|intern|trainee|bez doświadczenia)\b", RegexOptions.IgnoreCase))
        {
            return "Brak doświadczenia";
        }

        if (Regex.IsMatch(text, @"\b(specjalista|specialist|mid|middle)\b", RegexOptions.IgnoreCase))
        {
            return "Specjalista";
        }

        return "Wszystkie";
    }

    public static EducationInfo DetectEducationInfo(string? title, string? description)
    {
        var titleText = NormalizeForDetection(title);
        var descriptionText = NormalizeForDetection(description);
        var text = $"{titleText} {descriptionText}".Trim();
        var field = DetectEducationField(text);

        var noEducationMatch = MatchFirst(text,
            @"\b(brak wymagan dotyczacych wyksztalcenia|wyksztalcenie (nie jest wymagane|niewymagane)|nie wymagamy wyksztalcenia|bez wymagan edukacyjnych|education not required|no degree required|degree not required)\b");

        if (noEducationMatch.Success)
        {
            return new EducationInfo
            {
                Level = "Brak wymagań lub niewymagane",
                Field = field,
                IsRequired = false,
                Confidence = 0.95m,
                Evidence = ExtractEvidence(text, noEducationMatch)
            };
        }

        var studentMatch = MatchFirst(text, @"\b(student|studentka|w trakcie studiow|ostatnie lata studiow|undergraduate|during studies|last years of studies)\b");
        if (studentMatch.Success)
        {
            return BuildEducationInfo("Student / w trakcie", field, text, studentMatch, 0.86m);
        }

        var higherMatch = MatchFirst(text, @"\b(wyksztalcenie wyzsze|wyzsze wyksztalcenie|studia wyzsze|higher education|university degree|bachelor|licencjat|inzynier|master|magister|degree)\b");
        if (higherMatch.Success)
        {
            return BuildEducationInfo("Wyższe", field, text, higherMatch, 0.88m);
        }

        var technicalMatch = MatchFirst(text, @"\b(wyksztalcenie techniczne|techniczne wyksztalcenie|technical education|technical background|technikum)\b");
        if (technicalMatch.Success)
        {
            return BuildEducationInfo("Techniczne / średnie techniczne", field ?? "techniczne", text, technicalMatch, 0.84m);
        }

        var secondaryMatch = MatchFirst(text, @"\b(wyksztalcenie srednie|srednie wyksztalcenie|secondary education|liceum)\b");
        if (secondaryMatch.Success)
        {
            return BuildEducationInfo("Średnie", field, text, secondaryMatch, 0.82m);
        }

        var vocationalMatch = MatchFirst(text, @"\b(wyksztalcenie zawodowe|zawodowe wyksztalcenie|vocational education|szkola zawodowa|szkola branzowa)\b");
        if (vocationalMatch.Success)
        {
            return BuildEducationInfo("Zawodowe", field, text, vocationalMatch, 0.82m);
        }

        return new EducationInfo { Field = field };
    }

    public static string DetectEducationLevel(string? title, string? description)
        => DetectEducationInfo(title, description).Level;

    public static List<OfferContractType> DetectContractTypes(string? title, string? description, string? contractTypeRaw, IEnumerable<string>? tags = null, string? rawPayloadJson = null, string? employmentTypeRaw = null)
    {
        var text = BuildDetectionText(title, description, $"{contractTypeRaw} {employmentTypeRaw} {rawPayloadJson}", tags);
        var rules = new (string Code, string Pattern)[]
        {
            ("employment_contract", @"\b(umowa o prace|uop|employment contract|arbeitsvertrag|permanent contract|permanent employment|festanstellung|festeinstellung|unbefristet|direktvermittlung)\b"),
            ("b2b", @"\b(b2b|kontrakt b2b|business to business)\b"),
            ("mandate_contract", @"\b(umowa zlecenie|zlecenie|mandate contract)\b"),
            ("specific_task_contract", @"\b(umowa o dzielo|specific task contract)\b"),
            ("temporary_contract", @"\b(praca tymczasowa|temporary work|temporary contract|temporary job)\b"),
            ("internship_contract", @"\b(staz|praktyki|internship|trainee|traineeship)\b"),
            ("freelance", @"\b(freelance|freelancer|freiberuflich|contract work)\b"),
            ("agency_contract", @"\b(agencja pracy|przez agencje|agency contract|personalvermittlung)\b"),
            ("civil_contract", @"\b(umowa cywilnoprawna|civil contract)\b"),
            ("apprenticeship", @"\b(praktyki zawodowe|nauka zawodu|apprenticeship)\b")
        };

        var result = new List<OfferContractType>();
        foreach (var (code, pattern) in rules)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                result.Add(new OfferContractType
                {
                    ContractType = code,
                    IsPrimary = result.Count == 0,
                    Confidence = ComesFromRaw(contractTypeRaw, match.Value) ? 0.95m : 0.88m,
                    Evidence = ExtractEvidence(text, match),
                    SourceField = ComesFromRaw(contractTypeRaw, match.Value) ? "contract_type_raw" : "description",
                    SourceSection = ResolveSourceSection(text, match)
                });
            }
        }

        return result
            .GroupBy(x => x.ContractType, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.Confidence).First())
            .ToList();
    }

    public static WorkTimeInfo DetectWorkTimeInfo(string? title, string? description, string? employmentTypeRaw, IEnumerable<string>? tags = null, string? rawPayloadJson = null, string? contractTypeRaw = null)
    {
        var text = BuildDetectionText(title, description, $"{employmentTypeRaw} {contractTypeRaw} {rawPayloadJson}", tags);

        var hoursRange = MatchFirst(text, @"(?<min>\d+(?:[,.]\d+)?)\s*(?:-|–|—|do|to)\s*(?<max>\d+(?:[,.]\d+)?)\s*(?:godzin|h|hours?)\s*(?:tygodniowo|weekly|per week)?");
        if (hoursRange.Success && TryGetDecimal(hoursRange, "min", out var hoursMin) && TryGetDecimal(hoursRange, "max", out var hoursMax))
        {
            return new WorkTimeInfo { WorkTimeType = hoursMax >= 35 ? "full_or_part" : "part_time", HoursPerWeekMin = hoursMin, HoursPerWeekMax = hoursMax, Confidence = 0.9m, Evidence = ExtractEvidence(text, hoursRange) };
        }

        var hours = MatchFirst(text, @"(?<min>\d+(?:[,.]\d+)?)\s*(?:godzin|h|hours?)\s*(?:tygodniowo|weekly|per week)");
        if (hours.Success && TryGetDecimal(hours, "min", out var hoursValue))
        {
            return new WorkTimeInfo { WorkTimeType = hoursValue >= 35 ? "full_time" : "part_time", HoursPerWeekMin = hoursValue, HoursPerWeekMax = hoursValue, Confidence = 0.88m, Evidence = ExtractEvidence(text, hours) };
        }

        var half = MatchFirst(text, @"\b(1/2 etatu|pol etatu|half[- ]?time|halbtags)\b");
        if (half.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "part_time", FteMin = 0.5m, FteMax = 0.5m, Confidence = 0.9m, Evidence = ExtractEvidence(text, half) };
        }

        var threeQuarters = MatchFirst(text, @"\b(3/4 etatu|75%\s*stelle)\b");
        if (threeQuarters.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "part_time", FteMin = 0.75m, FteMax = 0.75m, Confidence = 0.9m, Evidence = ExtractEvidence(text, threeQuarters) };
        }

        var quarter = MatchFirst(text, @"\b(1/4 etatu|25%\s*stelle)\b");
        if (quarter.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "part_time", FteMin = 0.25m, FteMax = 0.25m, Confidence = 0.9m, Evidence = ExtractEvidence(text, quarter) };
        }

        var germanFull = MatchFirst(text, @"\b(vollzeit)\b");
        if (germanFull.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "full_time", FteMin = 1m, FteMax = 1m, Confidence = ComesFromRaw(employmentTypeRaw, germanFull.Value) ? 0.95m : 0.86m, Evidence = ExtractEvidence(text, germanFull) };
        }

        var germanPart = MatchFirst(text, @"\b(teilzeit)\b");
        if (germanPart.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "part_time", Confidence = ComesFromRaw(employmentTypeRaw, germanPart.Value) ? 0.95m : 0.84m, Evidence = ExtractEvidence(text, germanPart) };
        }

        var germanFlexible = MatchFirst(text, @"\b(flexible arbeitszeiten|flexible arbeitszeit)\b");
        if (germanFlexible.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "flexible", Confidence = 0.78m, Evidence = ExtractEvidence(text, germanFlexible) };
        }

        var full = MatchFirst(text, @"\b(pelny etat|full[- ]?time|pełny etat)\b");
        if (full.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "full_time", FteMin = 1m, FteMax = 1m, Confidence = ComesFromRaw(employmentTypeRaw, full.Value) ? 0.95m : 0.88m, Evidence = ExtractEvidence(text, full) };
        }

        var part = MatchFirst(text, @"\b(niepelny etat|czesc etatu|part[- ]?time)\b");
        if (part.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "part_time", Confidence = ComesFromRaw(employmentTypeRaw, part.Value) ? 0.95m : 0.86m, Evidence = ExtractEvidence(text, part) };
        }

        var flexible = MatchFirst(text, @"\b(elastyczny wymiar|elastyczny czas pracy|flexible working time|flexible working hours|flexible hours|elastyczne godziny)\b");
        if (flexible.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "flexible", Confidence = 0.78m, Evidence = ExtractEvidence(text, flexible) };
        }

        var casual = MatchFirst(text, @"\b(dorywcza|praca dodatkowa|casual)\b");
        if (casual.Success)
        {
            return new WorkTimeInfo { WorkTimeType = "casual", Confidence = 0.78m, Evidence = ExtractEvidence(text, casual) };
        }

        return new WorkTimeInfo();
    }

    public static WorkModeInfo DetectWorkModeInfo(string? title, string? description, string? location, IEnumerable<string>? tags, bool isRemoteHint, string? rawPayloadJson = null, string? employmentTypeRaw = null, string? contractTypeRaw = null)
    {
        var text = BuildDetectionText(title, description, $"{location} {employmentTypeRaw} {contractTypeRaw} {rawPayloadJson}", tags);

        var officeDays = MatchFirst(text,
            @"(?<days>\d+(?:[,.]\d+)?)\s*(?:dni|days?)\s+w\s+biurze",
            @"(?<days>\d+(?:[,.]\d+)?)\s*(?:office days?|days?)\s*(?:in|at)?\s*(?:the\s*)?office");
        if (officeDays.Success && TryGetDecimal(officeDays, "days", out var days))
        {
            return new WorkModeInfo { WorkMode = "hybrid", RemoteScope = "hybrid", OfficeDaysPerWeekMin = days, OfficeDaysPerWeekMax = days, Confidence = 0.92m, Evidence = ExtractEvidence(text, officeDays) };
        }

        var fullyRemote = MatchFirst(text, @"\b(100%\s*remote|fully remote|remote-first|praca w 100%\s*zdalna|praca zdalna|zdalnie|zdalna z polski|remote|home office|homeoffice|remote work)\b");
        if (fullyRemote.Success)
        {
            return new WorkModeInfo { WorkMode = "remote", RemoteScope = "fully_remote", Confidence = isRemoteHint ? 0.95m : 0.88m, Evidence = ExtractEvidence(text, fullyRemote) };
        }

        var hybrid = MatchFirst(text, @"\b(hybrid|hybrid work|hybrydowa|hybrydowo|praca hybrydowa|czesciowo zdalnie|partly remote|hybride arbeit|hybrides arbeiten)\b");
        if (hybrid.Success)
        {
            return new WorkModeInfo { WorkMode = "hybrid", RemoteScope = "hybrid", Confidence = 0.88m, Evidence = ExtractEvidence(text, hybrid) };
        }

        var onsite = MatchFirst(text, @"\b(stacjonarna|stacjonarnie|onsite|on-site|praca w biurze|stationary|vor ort|am standort)\b");
        if (onsite.Success)
        {
            return new WorkModeInfo { WorkMode = "onsite", RemoteScope = "not_remote", Confidence = 0.84m, Evidence = ExtractEvidence(text, onsite) };
        }

        var field = MatchFirst(text, @"\b(praca terenowa|praca mobilna|mobilnie|terenowo|w terenie|field work|mobile work|aussendienst)\b");
        if (field.Success)
        {
            return new WorkModeInfo { WorkMode = "field", RemoteScope = "not_remote", Confidence = 0.84m, Evidence = ExtractEvidence(text, field) };
        }

        if (isRemoteHint)
        {
            return new WorkModeInfo { WorkMode = "remote", RemoteScope = "remote_possible", Confidence = 0.75m, Evidence = "remote flag from source or basic detection" };
        }

        return new WorkModeInfo();
    }

    public static SalaryInfo DetectSalaryInfo(decimal? salaryMin, decimal? salaryMax, string? salaryRaw, string? description = null)
    {
        var usesDescriptionFallback = string.IsNullOrWhiteSpace(salaryRaw) && !string.IsNullOrWhiteSpace(description);
        var rawText = usesDescriptionFallback ? description : salaryRaw;
        var text = NormalizeForDetection(rawText);
        var info = new SalaryInfo();
        var hasSalarySignal = !usesDescriptionFallback ||
            Regex.IsMatch(text, @"\b(wynagrodzenie|salary|pay|stawka|rate|brutto|netto|gross|net|zl|zł|pln|eur|usd)\b|€|\$");

        if (salaryMin.HasValue && salaryMax.HasValue)
        {
            info.SalaryRateType = "range";
            info.Confidence = 0.95m;
        }
        else if (salaryMin.HasValue)
        {
            info.SalaryRateType = "from";
            info.Confidence = 0.9m;
        }
        else if (salaryMax.HasValue)
        {
            info.SalaryRateType = "to";
            info.Confidence = 0.9m;
        }

        var negotiable = MatchFirst(text, @"\b(do uzgodnienia|wynagrodzenie do uzgodnienia|negotiable|competitive salary)\b");
        if (negotiable.Success)
        {
            info.SalaryRateType = "negotiable";
            info.Confidence = Math.Max(info.Confidence, 0.8m);
            info.Evidence = ExtractEvidence(text, negotiable);
        }

        var range = MatchFirst(text,
            @"(?<min>\d[\d\s.,]*)\s*(?:-|–|—|do|to)\s*(?<max>\d[\d\s.,]*)\s*(?<currency>zl|zł|pln|eur|€|usd|\$)?",
            @"(?<currency>zl|zł|pln|eur|€|usd|\$)\s*(?<min>\d[\d\s.,]*)\s*(?:-|–|—|do|to)\s*(?<max>\d[\d\s.,]*)");
        if (range.Success &&
            hasSalarySignal &&
            TryParseSalaryAmount(range.Groups["min"].Value, out var parsedMin) &&
            TryParseSalaryAmount(range.Groups["max"].Value, out var parsedMax))
        {
            info.ParsedMin = parsedMin;
            info.ParsedMax = parsedMax;
            info.SalaryRateType = "range";
            info.ParsedCurrency = NormalizeCurrency(range.Groups["currency"].Value);
            info.Confidence = Math.Max(info.Confidence, 0.88m);
            info.Evidence ??= ExtractEvidence(text, range);
        }

        var fixedAmount = MatchFirst(text,
            @"(?<!\d)(?<amount>\d[\d\s.,]*)\s*(?<currency>zl|zł|pln|eur|€|usd|\$)\b",
            @"\b(?<currency>zl|zł|pln|eur|€|usd|\$)\s*(?<amount>\d[\d\s.,]*)");
        if (!info.ParsedMin.HasValue &&
            hasSalarySignal &&
            fixedAmount.Success &&
            TryParseSalaryAmount(fixedAmount.Groups["amount"].Value, out var parsedFixed))
        {
            info.ParsedMin = parsedFixed;
            info.ParsedMax = parsedFixed;
            info.SalaryRateType = "fixed";
            info.ParsedCurrency = NormalizeCurrency(fixedAmount.Groups["currency"].Value);
            info.Confidence = Math.Max(info.Confidence, 0.84m);
            info.Evidence ??= ExtractEvidence(text, fixedAmount);
        }

        var from = MatchFirst(text, @"\b(?:od|from)\s*(?<amount>\d[\d\s.,]*)\s*(?<currency>zl|zł|pln|eur|€|usd|\$)?");
        if (!info.ParsedMin.HasValue &&
            hasSalarySignal &&
            from.Success &&
            TryParseSalaryAmount(from.Groups["amount"].Value, out var parsedFrom))
        {
            info.ParsedMin = parsedFrom;
            info.SalaryRateType = "from";
            info.ParsedCurrency = NormalizeCurrency(from.Groups["currency"].Value);
            info.Confidence = Math.Max(info.Confidence, 0.82m);
            info.Evidence ??= ExtractEvidence(text, from);
        }

        var to = MatchFirst(text, @"\b(?:do|up to)\s*(?<amount>\d[\d\s.,]*)\s*(?<currency>zl|zł|pln|eur|€|usd|\$)?");
        if (!info.ParsedMax.HasValue &&
            hasSalarySignal &&
            to.Success &&
            TryParseSalaryAmount(to.Groups["amount"].Value, out var parsedTo))
        {
            info.ParsedMax = parsedTo;
            info.SalaryRateType = "to";
            info.ParsedCurrency = NormalizeCurrency(to.Groups["currency"].Value);
            info.Confidence = Math.Max(info.Confidence, 0.82m);
            info.Evidence ??= ExtractEvidence(text, to);
        }

        info.ParsedCurrency ??= DetectCurrency(text);

        if (Regex.IsMatch(text, @"(\b(godz|godzin|hour|hourly)\b|/h|zl/h|zł/h|pln/h)"))
        {
            info.SalaryPeriod = "hour";
        }
        else if (Regex.IsMatch(text, @"\b(year|annually|annual|rocznie|rok)\b"))
        {
            info.SalaryPeriod = "year";
        }
        else if (salaryMin.HasValue || salaryMax.HasValue || info.ParsedMin.HasValue || info.ParsedMax.HasValue || Regex.IsMatch(text, @"\b(mies|miesiecznie|month|monthly)\b"))
        {
            info.SalaryPeriod = "month";
        }

        if (Regex.IsMatch(text, @"\b(brutto|gross)\b"))
        {
            info.SalaryTaxType = "gross";
        }
        else if (Regex.IsMatch(text, @"\b(netto|net)\b"))
        {
            info.SalaryTaxType = "net";
        }

        info.Evidence ??= string.IsNullOrWhiteSpace(rawText) ? null : rawText;
        return info;
    }

    public static List<OfferScheduleFlag> DetectScheduleFlags(string? title, string? description, IEnumerable<string>? tags = null)
    {
        var text = BuildDetectionText(title, description, null, tags);
        var rules = new (string Code, string Pattern)[]
        {
            ("shift_work", @"\b(praca zmianowa|system zmianowy|dwie zmiany|trzy zmiany|4[- ]?brygadowy|shift work|schichtarbeit)\b"),
            ("night_shifts", @"\b(praca w nocy|nocne zmiany|nocki|night shifts?|nachtschicht)\b"),
            ("weekend_work", @"\b(praca w weekend|weekend work|weekendy|wochenendarbeit)\b"),
            ("flexible_hours", @"\b(elastyczne godziny|flexible hours|flexible working hours|flexible arbeitszeiten)\b"),
            ("fixed_hours", @"\b(stale godziny|fixed hours)\b"),
            ("overtime", @"\b(nadgodziny|overtime)\b"),
            ("business_trips", @"\b(delegacje|podroze sluzbowe|business trips?|travelling job)\b"),
            ("on_call", @"\b(dyzury|dyżury|on-call|on call)\b"),
            ("morning_shifts", @"\b(poranne zmiany|morning shifts?)\b"),
            ("afternoon_shifts", @"\b(popoldniowe zmiany|afternoon shifts?)\b"),
            ("evening_shifts", @"\b(wieczorne zmiany|evening shifts?)\b")
        };

        return DetectFlags(text, rules)
            .Select(x => new OfferScheduleFlag { ScheduleFlag = x.Code, Confidence = x.Confidence, Evidence = x.Evidence, SourceSection = x.SourceSection })
            .ToList();
    }

    public static List<OfferBenefit> DetectBenefits(string? title, string? description, IEnumerable<string>? tags = null)
    {
        var text = BuildDetectionText(title, description, null, tags);
        var rules = new (string Code, string Name, string Pattern)[]
        {
            ("remote_work", "Praca zdalna", @"\b(homeoffice|home office|remote work)\b"),
            ("flexible_hours", "Elastyczne godziny", @"\b(flexible arbeitszeiten|flexible arbeitszeit|flexible hours)\b"),
            ("training_budget", "Szkolenia", @"\b(weiterbildung|fortbildung|training budget|szkolenia)\b"),
            ("equipment", "Sprzet do pracy", @"\b(arbeitsausstattung|equipment)\b"),
            ("parking", "Parking", @"\b(parkplatz|parking)\b"),
            ("performance_bonus", "Premia wynikowa", @"\b(bonuszahlungen|performance bonus|premia wynikowa|premia za wyniki)\b"),
            ("private_healthcare", "Prywatna opieka medyczna", @"\b(prywatna opieka medyczna|opieka medyczna|private healthcare|medical care)\b"),
            ("multisport", "Multisport", @"\b(multisport|karta sportowa|sport card)\b"),
            ("life_insurance", "Ubezpieczenie na życie", @"\b(ubezpieczenie na zycie|life insurance)\b"),
            ("remote_work", "Praca zdalna", @"\b(praca zdalna|remote work|home office)\b"),
            ("flexible_hours", "Elastyczne godziny", @"\b(elastyczne godziny|flexible hours)\b"),
            ("training_budget", "Budżet szkoleniowy", @"\b(budzet szkoleniowy|training budget|szkolenia)\b"),
            ("language_courses", "Kursy językowe", @"\b(kursy jezykowe|language courses)\b"),
            ("equipment", "Sprzęt do pracy", @"\b(sprzet do pracy|equipment)\b"),
            ("parking", "Parking", @"\b(parking)\b"),
            ("meal_allowance", "Dofinansowanie posiłków", @"\b(dofinansowanie posilkow|meal allowance|lunch card)\b"),
            ("company_car", "Samochód służbowy", @"\b(samochod sluzbowy|company car)\b"),
            ("company_phone", "Telefon służbowy", @"\b(telefon sluzbowy|company phone)\b"),
            ("company_laptop", "Laptop służbowy", @"\b(laptop sluzbowy|company laptop|laptop)\b"),
            ("integration_events", "Wydarzenia integracyjne", @"\b(integracje|integration events?)\b"),
            ("relocation_package", "Pakiet relokacyjny", @"\b(relocation package|pakiet relokacyjny)\b"),
            ("stock_options", "Opcje na akcje", @"\b(stock options?|opcje na akcje)\b"),
            ("performance_bonus", "Premia wynikowa", @"\b(premia|premie|bonus)\b"),
            ("sales_bonus", "Premia sprzedażowa", @"\b(premia sprzedazowa|sales bonus)\b"),
            ("commission", "Prowizja", @"\b(prowizja|commission)\b"),
            ("christmas_bonus", "Premia świąteczna", @"\b(premia swiateczna|christmas bonus|christmas allowance)\b"),
            ("employee_discounts", "Zniżki pracownicze", @"\b(znizki pracownicze|employee discounts?)\b"),
            ("paid_holidays", "Płatny urlop", @"\b(platny urlop|paid holidays?|paid vacation)\b")
        };

        return rules
            .Select(rule => (rule, Match: Regex.Match(text, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            .Where(x => x.Match.Success)
            .Select(x => new OfferBenefit
            {
                BenefitCode = x.rule.Code,
                BenefitName = x.rule.Name,
                Confidence = ResolveSourceSection(text, x.Match) == "benefits" ? 0.92m : 0.78m,
                Evidence = ExtractEvidence(text, x.Match),
                SourceSection = ResolveSourceSection(text, x.Match)
            })
            .GroupBy(x => x.BenefitCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.Confidence).First())
            .ToList();
    }

    public static List<OfferDomain> DetectDomains(string? title, string? description, IEnumerable<string>? tags = null)
    {
        var text = BuildDetectionText(title, description, null, tags);
        var rules = new (string Code, string Name, string Pattern)[]
        {
            ("real_estate", "Nieruchomosci", @"\b(immobilien|real estate|nieruchomosci)\b"),
            ("building_services", "Technika budynkowa", @"\b(gebaudetechnik|gebaeudetechnik|versorgungstechnik|hkls|hlsk|tga|facility management)\b"),
            ("pharma", "Pharma", @"\b(pharma|pharmaindustrie|pharmaceutical)\b"),
            ("fashion", "Fashion / tekstylia", @"\b(fashion|textile|textilien|moda)\b"),
            ("bakery", "Piekarnia", @"\b(backerei|baeckerei|bakery|piekarnia)\b"),
            ("mobility", "Mobilnosc", @"\b(mobilitat|mobilitaet|mobility)\b"),
            ("fintech", "Fintech", @"\b(fintech)\b"),
            ("banking", "Bankowość", @"\b(bankowosc|banking|bank)\b"),
            ("ecommerce", "E-commerce", @"\b(e-commerce|ecommerce|sklep internetowy)\b"),
            ("healthcare", "Healthcare", @"\b(healthcare|medyczn|opieka zdrowotna)\b"),
            ("education", "Edukacja", @"\b(edukacja|education|szkolnictwo)\b"),
            ("logistics", "Logistyka", @"\b(logistyka|logistics|transport)\b"),
            ("automotive", "Automotive", @"\b(automotive|motoryzacja)\b"),
            ("manufacturing", "Produkcja", @"\b(produkcja|manufacturing)\b"),
            ("gaming", "Gaming", @"\b(gaming|games|gry)\b"),
            ("marketing", "Marketing", @"\b(marketing|seo|google ads|meta ads)\b"),
            ("public_sector", "Sektor publiczny", @"\b(sektor publiczny|administracja publiczna|public sector)\b"),
            ("cybersecurity", "Cybersecurity", @"\b(cybersecurity|bezpieczenstwo it|security)\b"),
            ("ai", "AI", @"\b(ai|artificial intelligence|sztuczna inteligencja)\b"),
            ("data", "Data", @"\b(data|danych|analytics|analityka)\b"),
            ("retail", "Retail", @"\b(retail|handel detaliczny|sprzedaz detaliczna)\b"),
            ("construction", "Budownictwo", @"\b(budownictwo|construction)\b"),
            ("gastronomy", "Gastronomia", @"\b(gastronomia|restaurant|restauracja)\b"),
            ("care", "Opieka", @"\b(opieka|caregiver|care)\b")
        };

        return rules
            .Select(rule => (rule, Match: Regex.Match(text, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            .Where(x => x.Match.Success)
            .Select(x => new OfferDomain
            {
                DomainCode = x.rule.Code,
                DomainName = x.rule.Name,
                Confidence = 0.76m,
                Evidence = ExtractEvidence(text, x.Match),
                SourceField = "description",
                SourceSection = ResolveSourceSection(text, x.Match),
                ExtractorVersion = ExtractorVersionProvider.Current
            })
            .GroupBy(x => x.DomainCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public static List<OfferFormalRequirement> DetectFormalRequirements(string? title, string? description, IEnumerable<string>? tags = null)
    {
        var text = BuildDetectionText(title, description, null, tags);
        var rules = new (string Code, string Pattern)[]
        {
            ("driving_license_b", @"\b(prawo jazdy kat\.?\s*b|kat\.?\s*b|driving license b)\b"),
            ("driving_license_c", @"\b(prawo jazdy kat\.?\s*c|kat\.?\s*c|driving license c)\b"),
            ("driving_license_ce", @"\b(prawo jazdy kat\.?\s*c\+e|kat\.?\s*c\+e|driving license c\+e)\b"),
            ("driving_license_required", @"\b(prawo jazdy|driving license|fuhrerschein|fuehrerschein)\b"),
            ("work_permit_required", @"\b(pozwolenie na prace|work permit)\b"),
            ("eu_citizenship_required", @"\b(obywatelstwo ue|eu citizenship)\b"),
            ("security_clearance_required", @"\b(poswiadczenie bezpieczenstwa|security clearance)\b"),
            ("criminal_record_check", @"\b(niekaralnosc|zaswiadczenie o niekaralnosci|criminal record)\b"),
            ("medical_tests_required", @"\b(badania lekarskie|medical tests?)\b"),
            ("student_status_required", @"\b(status studenta|student status)\b"),
            ("availability_asap", @"\b(od zaraz|asap|start asap)\b"),
            ("own_car_required", @"\b(wlasny samochod|own car)\b"),
            ("own_equipment_required", @"\b(wlasny sprzet|own equipment)\b"),
            ("udt_required", @"\b(udt|uprawnienia udt|wozki widlowe|wózki widłowe|forklift license)\b"),
            ("sep_required", @"\b(sep|uprawnienia sep)\b"),
            ("sanitary_book_required", @"\b(ksiazeczka sanepidowska|książeczka sanepidowska|sanepid)\b")
        };

        return DetectFlags(text, rules)
            .Select(x => new OfferFormalRequirement
            {
                RequirementCode = x.Code,
                IsRequired = true,
                Confidence = x.Confidence,
                Evidence = x.Evidence,
                SourceField = "description",
                SourceSection = x.SourceSection,
                ExtractorVersion = ExtractorVersionProvider.Current
            })
            .ToList();
    }

    public static void PopulateDerivedOfferData(NormalizedJobOffer offer)
    {
        offer.EmploymentTypeRaw ??= offer.EmploymentType;
        offer.ContractTypeRaw ??= offer.ContractType;
        offer.WorkMode = DetectWorkModeInfo(offer.Title, offer.Description, offer.LocationName, offer.Tags, offer.IsRemote, offer.RawPayloadJson, offer.EmploymentTypeRaw, offer.ContractTypeRaw);
        offer.IsRemote = offer.IsRemote || offer.WorkMode.WorkMode == "remote";
        offer.WorkTime = DetectWorkTimeInfo(offer.Title, offer.Description, offer.EmploymentTypeRaw, offer.Tags, offer.RawPayloadJson, offer.ContractTypeRaw);
        offer.SalaryInfo = DetectSalaryInfo(offer.SalaryMin, offer.SalaryMax, offer.SalaryRaw, offer.Description);
        if (!offer.SalaryMin.HasValue && offer.SalaryInfo.ParsedMin.HasValue)
        {
            offer.SalaryMin = offer.SalaryInfo.ParsedMin;
        }

        if (!offer.SalaryMax.HasValue && offer.SalaryInfo.ParsedMax.HasValue)
        {
            offer.SalaryMax = offer.SalaryInfo.ParsedMax;
        }

        if (string.IsNullOrWhiteSpace(offer.SalaryCurrency) && !string.IsNullOrWhiteSpace(offer.SalaryInfo.ParsedCurrency))
        {
            offer.SalaryCurrency = offer.SalaryInfo.ParsedCurrency;
        }

        offer.ContractTypes = DetectContractTypes(offer.Title, offer.Description, offer.ContractTypeRaw, offer.Tags, offer.RawPayloadJson, offer.EmploymentTypeRaw);
        offer.ScheduleFlags = DetectScheduleFlags(offer.Title, offer.Description, offer.Tags);
        offer.Benefits = DetectBenefits(offer.Title, offer.Description, offer.Tags);
        offer.Domains = DetectDomains(offer.Title, offer.Description, offer.Tags);
        offer.FormalRequirements = DetectFormalRequirements(offer.Title, offer.Description, offer.Tags);
        ApplyDescriptionQualityPenalty(offer);
        (offer.DataQualityScore, offer.ExtractionScore) = CalculateQualityScores(offer);
    }

    public static decimal AdjustConfidenceForDescriptionQuality(decimal confidence, string? descriptionQuality)
    {
        var penalty = (descriptionQuality ?? "unknown").ToLowerInvariant() switch
        {
            "full" => 0m,
            "html" => 0.03m,
            "snippet" => 0.15m,
            "missing" => 0.35m,
            _ => 0.10m
        };

        return Math.Clamp(confidence - penalty, 0.05m, 0.99m);
    }

    private static void ApplyDescriptionQualityPenalty(NormalizedJobOffer offer)
    {
        offer.WorkMode.Confidence = AdjustConfidenceForDescriptionQuality(offer.WorkMode.Confidence, offer.DescriptionQuality);
        offer.WorkTime.Confidence = AdjustConfidenceForDescriptionQuality(offer.WorkTime.Confidence, offer.DescriptionQuality);
        offer.SalaryInfo.Confidence = AdjustConfidenceForDescriptionQuality(offer.SalaryInfo.Confidence, offer.DescriptionQuality);
        offer.Experience.Confidence = AdjustConfidenceForDescriptionQuality(offer.Experience.Confidence, offer.DescriptionQuality);
        offer.Education.Confidence = AdjustConfidenceForDescriptionQuality(offer.Education.Confidence, offer.DescriptionQuality);

        foreach (var item in offer.ContractTypes)
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
        }

        foreach (var item in offer.ScheduleFlags)
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
        }

        foreach (var item in offer.Benefits)
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
        }

        foreach (var item in offer.Domains)
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
        }

        foreach (var item in offer.FormalRequirements)
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
        }

        foreach (var item in offer.Languages)
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
        }

        foreach (var item in offer.Classification.Criteria.Concat(offer.Classification.CriterionHits))
        {
            item.Confidence = AdjustConfidenceForDescriptionQuality(item.Confidence, offer.DescriptionQuality);
            item.ExtractorVersion = ExtractorVersionProvider.Current;
        }
    }

    public static string NormalizeForDetection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = System.Net.WebUtility.HtmlDecode(value);
        text = Regex.Replace(text, "<.*?>", " ");
        text = text.Replace("\r", " ").Replace("\n", " ");
        text = RemoveDiacritics(text);
        text = Regex.Replace(text, @"\s+", " ").Trim().ToLowerInvariant();
        return text;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string BuildDetectionText(string? title, string? description, string? rawField, IEnumerable<string>? tags)
    {
        var joinedTags = tags is null ? string.Empty : string.Join(" ", tags.Where(x => !string.IsNullOrWhiteSpace(x)));
        return NormalizeForDetection($"{title} {description} {rawField} {joinedTags}");
    }

    private static bool ComesFromRaw(string? rawField, string value)
    {
        return !string.IsNullOrWhiteSpace(rawField) &&
               !string.IsNullOrWhiteSpace(value) &&
               NormalizeForDetection(rawField).Contains(NormalizeForDetection(value), StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveSourceSection(string text, Match match)
    {
        var prefixStart = Math.Max(0, match.Index - 500);
        var prefix = text[prefixStart..match.Index];

        if (Regex.IsMatch(prefix, @"\b(wymagania|requirements|oczekujemy|must have)\b"))
        {
            return "required";
        }

        if (Regex.IsMatch(prefix, @"\b(mile widziane|nice to have|optional|preferred)\b"))
        {
            return "optional";
        }

        if (Regex.IsMatch(prefix, @"\b(oferujemy|benefity|benefits|perks|co oferujemy)\b"))
        {
            return "benefits";
        }

        if (Regex.IsMatch(prefix, @"\b(obowiazki|responsibilities|zadania)\b"))
        {
            return "responsibilities";
        }

        return "unknown";
    }

    private static List<(string Code, decimal Confidence, string Evidence, string? SourceSection)> DetectFlags(string text, IEnumerable<(string Code, string Pattern)> rules)
    {
        var result = new List<(string Code, decimal Confidence, string Evidence, string? SourceSection)>();

        foreach (var (code, pattern) in rules)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var section = ResolveSourceSection(text, match);
            result.Add((code, section is "required" or "benefits" ? 0.88m : 0.76m, ExtractEvidence(text, match), section));
        }

        return result
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.Confidence).First())
            .ToList();
    }

    private static (decimal DataQualityScore, decimal ExtractionScore) CalculateQualityScores(NormalizedJobOffer offer)
    {
        decimal data = 0;
        decimal extraction = 0;

        if (offer.SalaryMin.HasValue || offer.SalaryMax.HasValue || !string.IsNullOrWhiteSpace(offer.SalaryRaw))
        {
            data += 20;
        }

        if (!string.IsNullOrWhiteSpace(offer.Description) && offer.Description.Length > 200 && !string.Equals(offer.DescriptionQuality, "snippet", StringComparison.OrdinalIgnoreCase))
        {
            data += 20;
        }

        if (!string.IsNullOrWhiteSpace(offer.City) || !string.IsNullOrWhiteSpace(offer.LocationName))
        {
            data += 10;
        }

        if (offer.ContractTypes.Any())
        {
            data += 10;
            extraction += 12;
        }

        if (offer.WorkTime.WorkTimeType != "unknown")
        {
            data += 10;
            extraction += 12;
        }

        if (offer.WorkMode.WorkMode != "unknown")
        {
            data += 10;
            extraction += 12;
        }

        if (offer.Classification.Criteria.Any())
        {
            data += 20;
            extraction += 22;
        }

        if (offer.Experience.Level != "Wszystkie")
        {
            extraction += 10;
        }

        if (offer.Education.Level != "Brak wymagań lub niewymagane" || !string.IsNullOrWhiteSpace(offer.Education.Field))
        {
            extraction += 8;
        }

        if (offer.Benefits.Any())
        {
            extraction += 8;
        }

        if (offer.ScheduleFlags.Any())
        {
            extraction += 6;
        }

        if (offer.Domains.Any())
        {
            extraction += 5;
        }

        if (offer.FormalRequirements.Any())
        {
            extraction += 5;
        }

        return (Math.Min(100, data), Math.Min(100, extraction));
    }

    private static ExperienceInfo? DetectExperienceLevelFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        if (Regex.IsMatch(title, @"\b(stazysta|staz|praktyk|intern|trainee)\b"))
        {
            return new ExperienceInfo { Level = "Staż / praktyki", MinYears = 0, MaxYears = 1, IsRequired = false, NoExperienceAllowed = true, Confidence = 0.9m, Evidence = title };
        }

        if (Regex.IsMatch(title, @"\b(junior|jr\.?|mlodszy|berufseinstieg|quereinsteiger)\b"))
        {
            return new ExperienceInfo { Level = "Junior", MinYears = 0, MaxYears = 2, NoExperienceAllowed = true, Confidence = 0.86m, Evidence = title };
        }

        if (Regex.IsMatch(title, @"\b(mid|middle|regular|berufserfahren)\b"))
        {
            return new ExperienceInfo { Level = "Mid / Regular", MinYears = 2, MaxYears = 5, Confidence = 0.84m, Evidence = title };
        }

        if (Regex.IsMatch(title, @"\b(senior|sr\.?|starszy)\b"))
        {
            return new ExperienceInfo { Level = "Senior", MinYears = 5, Confidence = 0.86m, Evidence = title };
        }

        if (Regex.IsMatch(title, @"\b(lead|principal|staff|head of|teamleitung|leitung)\b"))
        {
            return new ExperienceInfo { Level = "Lead / Principal", MinYears = 7, Confidence = 0.86m, Evidence = title };
        }

        if (Regex.IsMatch(title, @"\b(manager|menadzer|kierownik|abteilungsleiter)\b"))
        {
            return new ExperienceInfo { Level = "Menadżer", Confidence = 0.8m, Evidence = title };
        }

        return null;
    }

    private static string MapYearsToExperienceLevel(decimal years)
    {
        if (years <= 0)
        {
            return "Brak doświadczenia";
        }

        if (years < 2)
        {
            return "Junior";
        }

        if (years < 5)
        {
            return "Mid / Regular";
        }

        if (years < 7)
        {
            return "Senior";
        }

        return "Lead / Principal";
    }

    private static EducationInfo BuildEducationInfo(string level, string? field, string text, Match match, decimal confidence)
    {
        return new EducationInfo
        {
            Level = level,
            Field = field,
            IsRequired = ResolveRequirementFromContext(text, match),
            Confidence = confidence,
            Evidence = ExtractEvidence(text, match)
        };
    }

    private static string? DetectEducationField(string text)
    {
        if (Regex.IsMatch(text, @"\b(informatyczne|computer science|software|programowanie|teleinformatyczne)\b"))
        {
            return "informatyczne";
        }

        if (Regex.IsMatch(text, @"\b(techniczne|technical|engineering|inzynierskie|mechaniczne|elektryczne|automatyka|mechatronika)\b"))
        {
            return "techniczne";
        }

        if (Regex.IsMatch(text, @"\b(ekonomiczne|finanse|rachunkowosc|accounting|finance|economics)\b"))
        {
            return "ekonomiczne / finansowe";
        }

        if (Regex.IsMatch(text, @"\b(medyczne|medical|nursing|pielegniarstwo|farmacja)\b"))
        {
            return "medyczne";
        }

        if (Regex.IsMatch(text, @"\b(prawnicze|law|legal)\b"))
        {
            return "prawnicze";
        }

        if (Regex.IsMatch(text, @"\b(pedagogiczne|education|teaching|nauczycielskie)\b"))
        {
            return "pedagogiczne";
        }

        return null;
    }

    private static bool? ResolveRequirementFromContext(string text, Match match)
    {
        var start = Math.Max(0, match.Index - 120);
        var length = Math.Min(text.Length - start, match.Length + 240);
        var context = text.Substring(start, length);

        if (Regex.IsMatch(context, @"\b(mile widziane|dodatkowy atut|atutem bedzie|nice to have|optional|preferred|would be a plus|is a plus)\b"))
        {
            return false;
        }

        if (Regex.IsMatch(context, @"\b(wymagane|wymagamy|oczekujemy|must have|required|requirements|expected|minimum|min\.?|co najmniej|at least)\b"))
        {
            return true;
        }

        return null;
    }

    private static Match MatchFirst(string text, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match;
            }
        }

        return Match.Empty;
    }

    private static bool TryGetDecimal(Match match, string groupName, out decimal value)
    {
        var raw = match.Groups[groupName].Value.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseSalaryAmount(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = Regex.Replace(raw, @"\s+", "");
        if (Regex.IsMatch(normalized, @"^\d{1,3}([,.]\d{3})+$"))
        {
            normalized = normalized.Replace(".", "").Replace(",", "");
        }
        else
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static string? NormalizeCurrency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return NormalizeForDetection(raw) switch
        {
            "zl" or "zł" or "pln" => "PLN",
            "eur" or "€" => "EUR",
            "usd" or "$" => "USD",
            _ => null
        };
    }

    private static string? DetectCurrency(string text)
    {
        if (Regex.IsMatch(text, @"\b(zl|zł|pln)\b"))
        {
            return "PLN";
        }

        if (Regex.IsMatch(text, @"\b(eur|€)\b"))
        {
            return "EUR";
        }

        return Regex.IsMatch(text, @"\b(usd|\$)\b") ? "USD" : null;
    }

    private static string ExtractEvidence(string text, Match match)
    {
        var start = Math.Max(0, match.Index - 60);
        var end = Math.Min(text.Length, match.Index + match.Length + 60);
        return text[start..end].Trim();
    }

    private static string DetectEducationLevelLegacy(string? title, string? description)
    {
        var text = $"{title} {description}";
        if (Regex.IsMatch(text, @"(wyższ|higher education|bachelor|master|degree)", RegexOptions.IgnoreCase))
        {
            return "Wyższe";
        }

        if (Regex.IsMatch(text, @"(średni|secondary education|technikum|liceum)", RegexOptions.IgnoreCase))
        {
            return "Średnie";
        }

        if (Regex.IsMatch(text, @"(student|w trakcie studi|undergraduate)", RegexOptions.IgnoreCase))
        {
            return "Student / w trakcie";
        }

        return "Brak wymagań lub niewymagane";
    }

    public static bool DetectRemote(string? title, string? description, string? location)
    {
        var text = $"{title} {description} {location}";
        return Regex.IsMatch(text, @"(remote|zdal|home office|work from home|telepraca|praca z domu)", RegexOptions.IgnoreCase);
    }

    public static List<OfferLanguage> DetectLanguagesWithLevels(string? title, string? description)
    {
        var text = $"{title} {description}";
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<OfferLanguage>();
        }

        var normalized = NormalizeForDetection(text);
        var titleLength = title?.Length ?? 0;
        var rules = new (string Code, string Name, string Pattern)[]
        {
            ("en", "Angielski", @"\b(angielski|angielskiego|english)\b"),
            ("de", "Niemiecki", @"\b(niemiecki|niemieckiego|german|deutsch)\b"),
            ("uk", "Ukrainski", @"\b(ukrainski|ukrainskiego|ukrainian)\b"),
            ("fr", "Francuski", @"\b(francuski|francuskiego|french|francais)\b")
        };

        var result = new List<OfferLanguage>();
        foreach (var rule in rules)
        {
            var match = Regex.Match(normalized, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var sentence = ExtractSentenceWindow(normalized, match);
            var level = DetectLanguageLevel(sentence);
            var requirement = ResolveLanguageRequirement(sentence);
            var section = ResolveSourceSection(normalized, match);
            result.Add(new OfferLanguage
            {
                Code = rule.Code,
                Name = rule.Name,
                LevelMin = level,
                IsRequired = requirement,
                Confidence = Math.Min(0.96m, (level == "unknown" ? 0.72m : 0.88m) + (requirement == true ? 0.04m : 0m)),
                Evidence = ExtractEvidence(text, match),
                SourceField = match.Index <= titleLength ? "title" : "description",
                SourceSection = section,
                ExtractorVersion = ExtractorVersionProvider.Current
            });
        }

        return result
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.IsRequired == true)
                .ThenByDescending(x => LanguageLevelRank(x.LevelMin))
                .ThenByDescending(x => x.Confidence)
                .First())
            .ToList();
    }

    private static string ExtractSentenceWindow(string text, Match match)
    {
        var sentenceStart = text.LastIndexOfAny(new[] { '.', '!', '?', '\n', ';' }, Math.Max(0, match.Index));
        var sentenceEnd = text.IndexOfAny(new[] { '.', '!', '?', '\n', ';' }, match.Index + match.Length);
        var start = sentenceStart < 0 ? Math.Max(0, match.Index - 60) : sentenceStart + 1;
        var end = sentenceEnd < 0 ? Math.Min(text.Length, match.Index + match.Length + 120) : sentenceEnd;
        return text[start..end];
    }

    private static string DetectLanguageLevel(string window)
    {
        var levelMatch = Regex.Match(window, @"\b(?<level>a1|a2|b1|b2|c1|c2|native|fluent|communicative|komunikatywny|komunikatywna|plynny|biegle|biegly)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!levelMatch.Success)
        {
            return "unknown";
        }

        return levelMatch.Groups["level"].Value.ToLowerInvariant() switch
        {
            "native" => "native",
            "fluent" or "plynny" or "biegle" or "biegly" => "C1",
            "communicative" or "komunikatywny" or "komunikatywna" => "B1",
            var level => level.ToUpperInvariant()
        };
    }

    private static bool? ResolveLanguageRequirement(string window)
    {
        if (Regex.IsMatch(window, @"\b(mile widziane|dodatkowy atut|nice to have|optional|preferred|would be a plus|is a plus)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }

        if (Regex.IsMatch(window, @"\b(wymagane|wymagamy|oczekujemy|must have|required|requirements|expected|minimum|min\.?|co najmniej|at least)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return null;
    }

    private static int LanguageLevelRank(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "A1" => 1,
            "A2" => 2,
            "B1" => 3,
            "B2" => 4,
            "C1" => 5,
            "C2" => 6,
            "NATIVE" => 7,
            _ => 0
        };
    }

    public static List<OfferLanguage> DetectLanguages(string? title, string? description)
    {
        var text = $"{title} {description}";
        var result = new List<OfferLanguage>();

        if (Regex.IsMatch(text, @"(angiel|english)", RegexOptions.IgnoreCase))
        {
            result.Add(new OfferLanguage { Code = "en", Name = "Angielski" });
        }

        if (Regex.IsMatch(text, @"(niemie|german|deutsch)", RegexOptions.IgnoreCase))
        {
            result.Add(new OfferLanguage { Code = "de", Name = "Niemiecki" });
        }

        if (Regex.IsMatch(text, @"(ukrai|ukrainian)", RegexOptions.IgnoreCase))
        {
            result.Add(new OfferLanguage { Code = "uk", Name = "Ukraiński" });
        }

        if (Regex.IsMatch(text, @"(francus|french|français|francais)", RegexOptions.IgnoreCase))
        {
            result.Add(new OfferLanguage { Code = "fr", Name = "Francuski" });
        }

        return result.GroupBy(x => x.Code).Select(x => x.First()).ToList();
    }
}
