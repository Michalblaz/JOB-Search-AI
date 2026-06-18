using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace MauiApp1.Importer;

/// <summary>
/// Pobiera oferty z włączonych źródeł zewnętrznych i normalizuje je do wspólnego modelu importu.
/// </summary>
/// <remarks>
/// Koordynator jest częścią procesu wsadowego: nie zapisuje danych samodzielnie, tylko przygotowuje słownik ofert dla
/// <see cref="PostgresJobRepository"/>. Dzięki temu pobieranie z API i zapis do bazy pozostają osobnymi odpowiedzialnościami.
/// </remarks>
/// <seealso cref="NormalizedJobOffer"/>
/// <seealso cref="ImporterHelpers"/>
public sealed class JobImportCoordinator
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly ImporterSettings _settings;

    /// <summary>
    /// Tworzy koordynator importu na podstawie konfiguracji źródeł i limitów pobierania.
    /// </summary>
    /// <param name="settings">Ustawienia importera odczytane z pliku konfiguracyjnego.</param>
    public JobImportCoordinator(ImporterSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Pobiera oferty ze wszystkich źródeł oznaczonych jako aktywne.
    /// </summary>
    /// <returns>Słownik, w którym kluczem jest kod źródła, a wartością lista znormalizowanych ofert.</returns>
    /// <remarks>
    /// Jeżeli dane źródło nie ma wymaganych kluczy API albo zwróci pustą odpowiedź, wynik dla niego pozostanie pusty lub nie zostanie
    /// dodany. Pozwala to kontynuować import pozostałych źródeł bez przerywania całego procesu.
    /// </remarks>
    public async Task<Dictionary<string, List<NormalizedJobOffer>>> FetchAllAsync()
    {
        var result = new Dictionary<string, List<NormalizedJobOffer>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceCode in GetEnabledSourceCodes())
        {
            result[sourceCode] = await FetchAsync(sourceCode);
        }

        return result;
    }

    public IEnumerable<string> GetEnabledSourceCodes()
    {
        if (_settings.Sources.Adzuna.Enabled)
        {
            yield return _settings.Sources.Adzuna.Code;
        }

        if (_settings.Sources.Jooble.Enabled)
        {
            yield return _settings.Sources.Jooble.Code;
        }

        if (_settings.Sources.Remotive.Enabled)
        {
            yield return _settings.Sources.Remotive.Code;
        }

        if (_settings.Sources.Arbeitnow.Enabled)
        {
            yield return _settings.Sources.Arbeitnow.Code;
        }
    }

    public Task<List<NormalizedJobOffer>> FetchAsync(string sourceCode)
    {
        if (string.Equals(sourceCode, _settings.Sources.Adzuna.Code, StringComparison.OrdinalIgnoreCase))
        {
            return FetchAdzunaAsync();
        }

        if (string.Equals(sourceCode, _settings.Sources.Jooble.Code, StringComparison.OrdinalIgnoreCase))
        {
            return FetchJoobleAsync();
        }

        if (string.Equals(sourceCode, _settings.Sources.Remotive.Code, StringComparison.OrdinalIgnoreCase))
        {
            return FetchRemotiveAsync();
        }

        if (string.Equals(sourceCode, _settings.Sources.Arbeitnow.Code, StringComparison.OrdinalIgnoreCase))
        {
            return FetchArbeitnowAsync();
        }

        return Task.FromResult(new List<NormalizedJobOffer>());
    }

    private async Task<List<NormalizedJobOffer>> FetchAdzunaAsync()
    {
        var offers = new List<NormalizedJobOffer>();
        var queries = GetImportQueries();
        var locations = GetImportLocations();
        if (string.IsNullOrWhiteSpace(_settings.Sources.Adzuna.AppId) || string.IsNullOrWhiteSpace(_settings.Sources.Adzuna.AppKey))
        {
            return offers;
        }

        foreach (var query in queries)
        {
            foreach (var location in locations)
            {
                for (var page = 1; page <= _settings.Import.MaxPagesPerQuery; page++)
                {
                    var url = BuildAdzunaUrl(query, location, page);
                    using var response = await _httpClient.GetAsync(url);
                    if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"[WARN] adzuna: HTTP 429 for query={query}, location={location}, page={page}. Skipping page after delay.");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[WARN] adzuna: HTTP {(int)response.StatusCode} for query={query}, location={location}, page={page}");
                        break;
                    }

                    var json = await ReadUtf8Async(response);
                    var data = JsonConvert.DeserializeObject<AdzunaRoot>(json);
                    var results = data?.Results ?? new List<AdzunaResult>();
                    if (!results.Any())
                    {
                        break;
                    }

                    offers.AddRange(results.Select(MapAdzunaOffer));
                    if (results.Count < _settings.Import.AdzunaResultsPerPage)
                    {
                        break;
                    }
                }
            }
        }

        return Deduplicate(offers);
    }

    private async Task<List<NormalizedJobOffer>> FetchJoobleAsync()
    {
        var offers = new List<NormalizedJobOffer>();
        var queries = GetImportQueries();
        var locations = GetImportLocations();
        if (string.IsNullOrWhiteSpace(_settings.Sources.Jooble.ApiKey))
        {
            return offers;
        }

        foreach (var query in queries)
        {
            foreach (var location in locations)
            {
                for (var page = 1; page <= _settings.Import.MaxPagesPerQuery; page++)
                {
                    var endpoint = $"https://pl.jooble.org/api/{_settings.Sources.Jooble.ApiKey}";
                    var payload = BuildJooblePayload(query, location, page);

                    using var response = await _httpClient.PostAsync(
                        endpoint,
                        new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                    if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"[WARN] jooble: HTTP 429 for query={query}, location={location}, page={page}. Skipping page after delay.");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[WARN] jooble: HTTP {(int)response.StatusCode} for query={query}, location={location}, page={page}");
                        break;
                    }

                    var json = await ReadUtf8Async(response);
                    var data = JsonConvert.DeserializeObject<JoobleRoot>(json);
                    var jobs = data?.Jobs ?? new List<JoobleJob>();
                    if (!jobs.Any())
                    {
                        break;
                    }

                    offers.AddRange(jobs.Select(MapJoobleOffer));
                    if (jobs.Count < _settings.Import.JoobleResultsPerPage)
                    {
                        break;
                    }
                }
            }
        }

        return Deduplicate(offers);
    }

    private Dictionary<string, string> BuildJooblePayload(string query, string location, int page)
    {
        var payload = new Dictionary<string, string>
        {
            ["keywords"] = query,
            ["page"] = page.ToString(),
            ["ResultOnPage"] = _settings.Import.JoobleResultsPerPage.ToString(),
            ["companysearch"] = "false"
        };

        if (!string.IsNullOrWhiteSpace(location))
        {
            payload["location"] = location;
        }

        return payload;
    }

    private string BuildAdzunaUrl(string query, string location, int page)
    {
        var url = new StringBuilder($"https://api.adzuna.com/v1/api/jobs/pl/search/{page}?app_id={_settings.Sources.Adzuna.AppId}&app_key={_settings.Sources.Adzuna.AppKey}&results_per_page={_settings.Import.AdzunaResultsPerPage}");

        if (!string.IsNullOrWhiteSpace(query))
        {
            url.Append("&what=").Append(Uri.EscapeDataString(query));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            url.Append("&where=").Append(Uri.EscapeDataString(location));
        }

        return url.ToString();
    }

    private List<string> GetImportQueries()
    {
        return _settings.Import.DefaultQueries
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetImportLocations()
    {
        var locations = _settings.Import.Locations
            .Select(location => location?.Trim() ?? string.Empty)
            .Where(location => location.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return locations.Count > 0 ? locations : new List<string> { string.Empty };
    }

    private async Task<List<NormalizedJobOffer>> FetchRemotiveAsync()
    {
        using var response = await _httpClient.GetAsync(_settings.Sources.Remotive.Url);
        if ((int)response.StatusCode == 429)
        {
            Console.WriteLine("[WARN] remotive: HTTP 429. Retry after delay.");
            await Task.Delay(TimeSpan.FromSeconds(10));
            using var retryResponse = await _httpClient.GetAsync(_settings.Sources.Remotive.Url);
            if (!retryResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[WARN] remotive: HTTP {(int)retryResponse.StatusCode}");
                return new List<NormalizedJobOffer>();
            }

            var retryJson = await ReadUtf8Async(retryResponse);
            var retryData = JsonConvert.DeserializeObject<RemotiveRoot>(retryJson);
            return Deduplicate((retryData?.Jobs ?? new List<RemotiveJob>()).Select(MapRemotiveOffer).ToList());
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[WARN] remotive: HTTP {(int)response.StatusCode}");
            return new List<NormalizedJobOffer>();
        }

        var json = await ReadUtf8Async(response);
        var data = JsonConvert.DeserializeObject<RemotiveRoot>(json);
        return Deduplicate((data?.Jobs ?? new List<RemotiveJob>()).Select(MapRemotiveOffer).ToList());
    }

    private async Task<List<NormalizedJobOffer>> FetchArbeitnowAsync()
    {
        using var response = await _httpClient.GetAsync(_settings.Sources.Arbeitnow.Url);
        if ((int)response.StatusCode == 429)
        {
            Console.WriteLine("[WARN] arbeitnow: HTTP 429. Retry after delay.");
            await Task.Delay(TimeSpan.FromSeconds(10));
            using var retryResponse = await _httpClient.GetAsync(_settings.Sources.Arbeitnow.Url);
            if (!retryResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[WARN] arbeitnow: HTTP {(int)retryResponse.StatusCode}");
                return new List<NormalizedJobOffer>();
            }

            var retryJson = await ReadUtf8Async(retryResponse);
            var retryData = JsonConvert.DeserializeObject<ArbeitnowRoot>(retryJson);
            return Deduplicate((retryData?.Data ?? new List<ArbeitnowJob>()).Select(MapArbeitnowOffer).ToList());
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[WARN] arbeitnow: HTTP {(int)response.StatusCode}");
            return new List<NormalizedJobOffer>();
        }

        var json = await ReadUtf8Async(response);
        var data = JsonConvert.DeserializeObject<ArbeitnowRoot>(json);
        return Deduplicate((data?.Data ?? new List<ArbeitnowJob>()).Select(MapArbeitnowOffer).ToList());
    }

    private static List<NormalizedJobOffer> Deduplicate(List<NormalizedJobOffer> offers)
    {
        return offers
            .Where(offer => !string.IsNullOrWhiteSpace(offer.ExternalId))
            .GroupBy(offer => $"{offer.SourceCode}|{offer.ExternalId}")
            .Select(group => group.First())
            .ToList();
    }

    private static async Task<string> ReadUtf8Async(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(bytes);
    }

    private static NormalizedJobOffer MapAdzunaOffer(AdzunaResult result)
    {
        var description = result.Description ?? string.Empty;
        var experience = ImporterHelpers.DetectExperienceInfo(result.Title, description);
        var education = ImporterHelpers.DetectEducationInfo(result.Title, description);
        var location = result.Location?.DisplayName;
        var split = ImporterHelpers.SplitLocation(location);
        var countryCode = DetectCountryCodeFromAdzunaLocation(result.Location);
        var currency = GuessCurrency(countryCode);
        var payload = JsonConvert.SerializeObject(result);
        var tags = new List<string> { result.Category?.Label ?? string.Empty, result.Category?.Tag ?? string.Empty, result.Contract_Time ?? string.Empty, result.Contract_Type ?? string.Empty }
            .Concat(result.Location?.Area ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new NormalizedJobOffer
        {
            SourceCode = "adzuna",
            ExternalId = result.Id ?? result.Redirect_Url ?? $"{result.Title}|{result.Company?.DisplayName}|{location}",
            ExternalUrl = result.Redirect_Url,
            Title = result.Title ?? "Bez tytułu",
            CompanyName = result.Company?.DisplayName,
            LocationName = location,
            City = split.City,
            Region = split.Region,
            CountryCode = countryCode,
            Description = description,
            DescriptionQuality = DetectDescriptionQuality(description, "full"),
            DescriptionShort = ImporterHelpers.BuildShortDescription(description),
            SalaryMin = result.Salary_Min is null ? null : Convert.ToDecimal(result.Salary_Min.Value),
            SalaryMax = result.Salary_Max is null ? null : Convert.ToDecimal(result.Salary_Max.Value),
            SalaryCurrency = currency,
            SalaryRaw = BuildSalaryRaw(result.Salary_Min, result.Salary_Max, currency),
            SalaryIsPredicted = ParseAdzunaBoolean(result.Salary_Is_Predicted),
            EmploymentType = result.Contract_Time,
            ContractType = result.Contract_Type,
            ExperienceLevel = experience.Level,
            EducationLevel = education.Level,
            Experience = experience,
            Education = education,
            IsRemote = ImporterHelpers.DetectRemote(result.Title, description, location),
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            PublishedAt = ParseDate(result.Created),
            Languages = ImporterHelpers.DetectLanguages(result.Title, description),
            Tags = tags,
            ExternalReference = result.Adref,
            RawPayloadJson = payload,
            Classification = JobClassificationRules.Classify(result.Title, description, tags),
            ContentHash = ImporterHelpers.ComputeContentHash(payload)
        };
    }

    private static NormalizedJobOffer MapJoobleOffer(JoobleJob job)
    {
        var description = job.Snippet ?? string.Empty;
        var experience = ImporterHelpers.DetectExperienceInfo(job.Title, description);
        var education = ImporterHelpers.DetectEducationInfo(job.Title, description);
        var split = ImporterHelpers.SplitLocation(job.Location);
        var payload = JsonConvert.SerializeObject(job);
        var tags = new List<string> { job.Type ?? string.Empty, job.Source ?? string.Empty }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var classification = JobClassificationRules.Classify(job.Title, description, tags);
        ApplyDescriptionQualityPenalty(classification, "snippet");
        ApplyDescriptionQualityPenalty(experience, education, "snippet");

        return new NormalizedJobOffer
        {
            SourceCode = "jooble",
            ExternalId = (job.Id?.ToString()) ?? job.Link ?? $"{job.Title}|{job.Company}",
            ExternalUrl = job.Link,
            Title = job.Title ?? "Bez tytułu",
            CompanyName = job.Company,
            LocationName = job.Location,
            City = split.City,
            Region = split.Region,
            CountryCode = ImporterHelpers.NormalizeCountryCode("PL"),
            Description = description,
            DescriptionQuality = DetectDescriptionQuality(description, "snippet"),
            DescriptionShort = ImporterHelpers.BuildShortDescription(description),
            SalaryRaw = job.Salary,
            EmploymentType = job.Type,
            ContractType = job.Type,
            ExperienceLevel = experience.Level,
            EducationLevel = education.Level,
            Experience = experience,
            Education = education,
            IsRemote = ImporterHelpers.DetectRemote(job.Title, description, job.Location),
            PublishedAt = ParseDate(job.Updated),
            Languages = ImporterHelpers.DetectLanguages(job.Title, description),
            Tags = tags,
            RawPayloadJson = payload,
            Classification = classification,
            ContentHash = ImporterHelpers.ComputeContentHash(payload)
        };
    }

    private static NormalizedJobOffer MapRemotiveOffer(RemotiveJob job)
    {
        var description = job.Description ?? string.Empty;
        var experience = ImporterHelpers.DetectExperienceInfo(job.Title, description);
        var education = ImporterHelpers.DetectEducationInfo(job.Title, description);
        var split = ImporterHelpers.SplitLocation(job.CandidateRequiredLocation);
        var payload = JsonConvert.SerializeObject(job);
        var tags = new List<string> { "Zdalna", job.Category ?? string.Empty, job.JobType ?? string.Empty }
            .Concat(job.Tags ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new NormalizedJobOffer
        {
            SourceCode = "remotive",
            ExternalId = job.Id.ToString(),
            ExternalUrl = job.Url,
            Title = job.Title ?? "Bez tytułu",
            CompanyName = job.CompanyName,
            CompanyLogoUrl = job.CompanyLogoUrl ?? job.CompanyLogo,
            LocationName = job.CandidateRequiredLocation,
            City = split.City,
            Region = split.Region,
            CountryCode = ImporterHelpers.NormalizeCountryCode(split.Country),
            Description = description,
            DescriptionQuality = DetectDescriptionQuality(description, "full"),
            DescriptionShort = ImporterHelpers.BuildShortDescription(description),
            SalaryRaw = job.Salary,
            SalaryCurrency = GuessCurrencyFromSalaryText(job.Salary),
            EmploymentType = job.JobType,
            ContractType = job.JobType,
            ExperienceLevel = experience.Level,
            EducationLevel = education.Level,
            Experience = experience,
            Education = education,
            IsRemote = true,
            PublishedAt = ParseDate(job.PublicationDate),
            Languages = ImporterHelpers.DetectLanguages(job.Title, description),
            Tags = tags,
            RawPayloadJson = payload,
            Classification = JobClassificationRules.Classify(job.Title, description, tags),
            ContentHash = ImporterHelpers.ComputeContentHash(payload)
        };
    }

    private static NormalizedJobOffer MapArbeitnowOffer(ArbeitnowJob job)
    {
        var description = job.Description ?? string.Empty;
        var experience = ImporterHelpers.DetectExperienceInfo(job.Title, description);
        var education = ImporterHelpers.DetectEducationInfo(job.Title, description);
        var split = ImporterHelpers.SplitLocation(job.Location);
        var payload = JsonConvert.SerializeObject(job);
        var tags = (job.Tags ?? new List<string>())
            .Concat(job.JobTypes ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new NormalizedJobOffer
        {
            SourceCode = "arbeitnow",
            ExternalId = job.Slug ?? job.Url ?? $"{job.Title}|{job.CompanyName}",
            ExternalUrl = job.Url,
            Title = job.Title ?? "Bez tytułu",
            CompanyName = job.CompanyName,
            LocationName = job.Location,
            City = split.City,
            Region = split.Region,
            CountryCode = ImporterHelpers.NormalizeCountryCode(split.Country ?? "DE"),
            Description = description,
            DescriptionQuality = DetectDescriptionQuality(description, "full"),
            DescriptionShort = ImporterHelpers.BuildShortDescription(description),
            SalaryRaw = "Do uzgodnienia",
            SalaryCurrency = "EUR",
            EmploymentType = string.Join(", ", job.JobTypes ?? new List<string>()),
            ContractType = string.Join(", ", job.JobTypes ?? new List<string>()),
            ExperienceLevel = experience.Level,
            EducationLevel = education.Level,
            Experience = experience,
            Education = education,
            IsRemote = job.Remote || ImporterHelpers.DetectRemote(job.Title, description, job.Location),
            PublishedAt = job.CreatedAtUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(job.CreatedAtUnix.Value) : null,
            Languages = ImporterHelpers.DetectLanguages(job.Title, description),
            Tags = tags,
            RawPayloadJson = payload,
            Classification = JobClassificationRules.Classify(job.Title, description, tags),
            ContentHash = ImporterHelpers.ComputeContentHash(payload)
        };
    }

    private static void ApplyDescriptionQualityPenalty(OfferClassification classification, string descriptionQuality)
    {
        if (!string.Equals(descriptionQuality, "snippet", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var criterion in classification.Criteria.Concat(classification.CriterionHits))
        {
            criterion.Confidence = Math.Max(0.1m, criterion.Confidence - 0.15m);
        }

        classification.RoleConfidence = Math.Max(0m, classification.RoleConfidence - 0.15m);
    }

    private static void ApplyDescriptionQualityPenalty(ExperienceInfo experience, EducationInfo education, string descriptionQuality)
    {
        if (!string.Equals(descriptionQuality, "snippet", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        experience.Confidence = Math.Max(0.1m, experience.Confidence - 0.15m);
        education.Confidence = Math.Max(0.1m, education.Confidence - 0.15m);
    }

    private static string DetectDescriptionQuality(string? description, string defaultQuality)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "missing";
        }

        if (string.Equals(defaultQuality, "snippet", StringComparison.OrdinalIgnoreCase))
        {
            return "snippet";
        }

        return Regex.IsMatch(description, "<[a-z][\\s\\S]*>", RegexOptions.IgnoreCase)
            ? "html"
            : defaultQuality;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? BuildSalaryRaw(double? salaryMin, double? salaryMax, string currency = "PLN")
    {
        if (salaryMin.HasValue && salaryMax.HasValue)
        {
            return $"{salaryMin.Value:0} - {salaryMax.Value:0} {currency}";
        }

        if (salaryMin.HasValue)
        {
            return $"od {salaryMin.Value:0} {currency}";
        }

        if (salaryMax.HasValue)
        {
            return $"do {salaryMax.Value:0} {currency}";
        }

        return null;
    }

    private static string? DetectCountryCodeFromAdzunaLocation(AdzunaLocation? location)
    {
        var country = location?.Area?.FirstOrDefault();
        return ImporterHelpers.NormalizeCountryCode(country ?? "PL");
    }

    private static string GuessCurrency(string? countryCode)
    {
        return countryCode?.ToUpperInvariant() switch
        {
            "UK" or "GB" => "GBP",
            "DE" or "FR" => "EUR",
            "PL" => "PLN",
            _ => "PLN"
        };
    }

    private static string? GuessCurrencyFromSalaryText(string? salary)
    {
        if (string.IsNullOrWhiteSpace(salary))
        {
            return null;
        }

        if (salary.Contains('$'))
        {
            return "USD";
        }

        if (salary.Contains('€') || salary.Contains("EUR", StringComparison.OrdinalIgnoreCase))
        {
            return "EUR";
        }

        if (salary.Contains('£') || salary.Contains("GBP", StringComparison.OrdinalIgnoreCase))
        {
            return "GBP";
        }

        if (salary.Contains("PLN", StringComparison.OrdinalIgnoreCase))
        {
            return "PLN";
        }

        return null;
    }

    private static bool? ParseAdzunaBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ when bool.TryParse(value, out var parsed) => parsed,
            _ => null
        };
    }
}

public sealed class AdzunaRoot
{
    [JsonProperty("results")] public List<AdzunaResult> Results { get; set; } = new();
}

public sealed class AdzunaResult
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("company")] public AdzunaCompany? Company { get; set; }
    [JsonProperty("location")] public AdzunaLocation? Location { get; set; }
    [JsonProperty("salary_min")] public double? Salary_Min { get; set; }
    [JsonProperty("salary_max")] public double? Salary_Max { get; set; }
    [JsonProperty("salary_is_predicted")] public string? Salary_Is_Predicted { get; set; }
    [JsonProperty("redirect_url")] public string? Redirect_Url { get; set; }
    [JsonProperty("adref")] public string? Adref { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("created")] public string? Created { get; set; }
    [JsonProperty("contract_time")] public string? Contract_Time { get; set; }
    [JsonProperty("contract_type")] public string? Contract_Type { get; set; }
    [JsonProperty("category")] public AdzunaCategory? Category { get; set; }
    [JsonProperty("latitude")] public double? Latitude { get; set; }
    [JsonProperty("longitude")] public double? Longitude { get; set; }
}

public sealed class AdzunaCompany
{
    [JsonProperty("display_name")] public string? DisplayName { get; set; }
}

public sealed class AdzunaLocation
{
    [JsonProperty("display_name")] public string? DisplayName { get; set; }
    [JsonProperty("area")] public List<string>? Area { get; set; }
}

public sealed class AdzunaCategory
{
    [JsonProperty("label")] public string? Label { get; set; }
    [JsonProperty("tag")] public string? Tag { get; set; }
}

public sealed class JoobleRoot
{
    [JsonProperty("jobs")] public List<JoobleJob> Jobs { get; set; } = new();
}

public sealed class JoobleJob
{
    [JsonProperty("id")] public long? Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("location")] public string? Location { get; set; }
    [JsonProperty("snippet")] public string? Snippet { get; set; }
    [JsonProperty("salary")] public string? Salary { get; set; }
    [JsonProperty("source")] public string? Source { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("link")] public string? Link { get; set; }
    [JsonProperty("company")] public string? Company { get; set; }
    [JsonProperty("updated")] public string? Updated { get; set; }
}

public sealed class RemotiveRoot
{
    [JsonProperty("jobs")] public List<RemotiveJob> Jobs { get; set; } = new();
}

public sealed class RemotiveJob
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("url")] public string? Url { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("company_name")] public string? CompanyName { get; set; }
    [JsonProperty("company_logo")] public string? CompanyLogo { get; set; }
    [JsonProperty("company_logo_url")] public string? CompanyLogoUrl { get; set; }
    [JsonProperty("category")] public string? Category { get; set; }
    [JsonProperty("tags")] public List<string>? Tags { get; set; }
    [JsonProperty("job_type")] public string? JobType { get; set; }
    [JsonProperty("publication_date")] public string? PublicationDate { get; set; }
    [JsonProperty("candidate_required_location")] public string? CandidateRequiredLocation { get; set; }
    [JsonProperty("salary")] public string? Salary { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
}

public sealed class ArbeitnowRoot
{
    [JsonProperty("data")] public List<ArbeitnowJob> Data { get; set; } = new();
}

public sealed class ArbeitnowJob
{
    [JsonProperty("slug")] public string? Slug { get; set; }
    [JsonProperty("company_name")] public string? CompanyName { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("remote")] public bool Remote { get; set; }
    [JsonProperty("url")] public string? Url { get; set; }
    [JsonProperty("tags")] public List<string>? Tags { get; set; }
    [JsonProperty("job_types")] public List<string>? JobTypes { get; set; }
    [JsonProperty("location")] public string? Location { get; set; }
    [JsonProperty("created_at")] public long? CreatedAtUnix { get; set; }
}
