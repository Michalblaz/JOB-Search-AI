using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;

namespace MauiApp1.testowe
{
    public class AdzunaResult
    {
        [JsonProperty("title")] public string? Title { get; set; }
        [JsonProperty("company")] public CompanyInfo? Company { get; set; }
        [JsonProperty("location")] public LocationInfo? Location { get; set; }
        [JsonProperty("salary_min")] public double? Salary_Min { get; set; }
        [JsonProperty("salary_max")] public double? Salary_Max { get; set; }
        [JsonProperty("redirect_url")] public string? Redirect_Url { get; set; }
        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("created")] public string? Created { get; set; }
        [JsonProperty("contract_time")] public string? Contract_Time { get; set; }
        [JsonProperty("contract_type")] public string? Contract_Type { get; set; }
        [JsonProperty("category")] public CategoryInfo? Category { get; set; }
    }

    public class CompanyInfo
    {
        [JsonProperty("display_name")] public string? DisplayName { get; set; }
    }

    public class LocationInfo
    {
        [JsonProperty("display_name")] public string? DisplayName { get; set; }
    }

    public class CategoryInfo
    {
        [JsonProperty("label")] public string? Label { get; set; }
    }

    public class AdzunaRoot
    {
        [JsonProperty("results")] public List<AdzunaResult> Results { get; set; } = new();
    }

    public class RemotiveRoot
    {
        [JsonProperty("jobs")] public List<RemotiveJob> Jobs { get; set; } = new();
    }

    public class RemotiveJob
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("url")] public string? Url { get; set; }
        [JsonProperty("title")] public string? Title { get; set; }
        [JsonProperty("company_name")] public string? CompanyName { get; set; }
        [JsonProperty("category")] public string? Category { get; set; }
        [JsonProperty("job_type")] public string? JobType { get; set; }
        [JsonProperty("publication_date")] public string? PublicationDate { get; set; }
        [JsonProperty("candidate_required_location")] public string? CandidateRequiredLocation { get; set; }
        [JsonProperty("salary")] public string? Salary { get; set; }
        [JsonProperty("description")] public string? Description { get; set; }
    }

    public class ArbeitnowRoot
    {
        [JsonProperty("data")] public List<ArbeitnowJob> Data { get; set; } = new();
    }

    public class ArbeitnowJob
    {
        [JsonProperty("slug")] public string? Slug { get; set; }
        [JsonProperty("company_name")] public string? CompanyName { get; set; }
        [JsonProperty("title")] public string? Title { get; set; }
        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("remote")] public bool Remote { get; set; }
        [JsonProperty("url")] public string? Url { get; set; }
        [JsonProperty("tags")] public List<string> Tags { get; set; } = new();
        [JsonProperty("job_types")] public List<string> JobTypes { get; set; } = new();
        [JsonProperty("location")] public string? Location { get; set; }
        [JsonProperty("created_at")] public long? CreatedAtUnix { get; set; }
    }

    public class JoobleRoot
    {
        [JsonProperty("totalCount")] public int TotalCount { get; set; }
        [JsonProperty("jobs")] public List<JoobleJob> Jobs { get; set; } = new();
    }

    public class JoobleJob
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

    public class AdzunaErrorResponse
    {
        [JsonProperty("exception")] public string? ExceptionCode { get; set; }
        [JsonProperty("display")] public string? DisplayMessage { get; set; }
    }

    /// <summary>
    /// Koordynuje wyszukiwanie ofert pracy, filtrowanie wyników, konta użytkowników oraz listę ulubionych.
    /// </summary>
    /// <remarks>
    /// Serwis pełni rolę stanu aplikacji dla ekranów Blazor/MAUI. Łączy dane z wielu źródeł, normalizuje je do
    /// <see cref="JobOffer"/>, a następnie oblicza dopasowanie względem aktywnych filtrów i profilu użytkownika.
    /// </remarks>
    /// <seealso cref="JobOffer"/>
    /// <seealso cref="GeminiMatchService"/>
    /// <seealso cref="IUserStore"/>
    public class JobSearchService
    {
        private const int DisplayedResultsPerPage = 20;
        private const int RemoteResultsPerQuery = 7;
        private const int AverageWorkHoursPerMonth = 168;
        private const string SessionLoginKey = "session_login";
        private const string SessionIsLoggedInKey = "session_is_logged_in";
        private const string LegacyAuthUsernameKey = "auth_username";
        private const string LegacyAuthPasswordKey = "auth_password";
        private const string EPracaSourceName = "ePraca (CBOP)";
        private static readonly Dictionary<string, string[]> SearchSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = new[] { "informatyk", "technik informatyk", "helpdesk", "administrator", "programista", "developer", "tester", "devops", "analityk danych", "support it", "wsparcie it" },
            ["informatyk"] = new[] { "it", "helpdesk", "administrator", "wsparcie it", "technik informatyk", "programista", "developer", "tester", "devops" },
            ["informatyka"] = new[] { "informatyk", "it", "helpdesk", "administrator", "technik informatyk", "programista", "developer", "tester", "devops" },
            ["sklepikarz"] = new[] { "sprzedawca", "sprzedawczyni", "kasjer", "kasjer sprzedawca", "ekspedient", "pracownik sklepu", "doradca klienta" },
            ["sprzedawca"] = new[] { "sklepikarz", "sprzedawczyni", "kasjer", "ekspedient", "pracownik sklepu" },
            ["kasjer"] = new[] { "sprzedawca", "kasjer sprzedawca", "pracownik sklepu", "ekspedient" },
            ["magazynier"] = new[] { "pracownik magazynu", "operator magazynu", "warehouse", "logistyka" },
            ["kierowca"] = new[] { "kurier", "dostawca", "kierowca kat", "transport" },
            ["programista"] = new[] { "developer", "software engineer", "backend", "frontend", ".net", "java", "python" },
            ["budowlaniec"] = new[] { "pracownik budowlany", "robotnik budowlany", "murarz", "cieśla", "zbrojarz", "tynkarz", "dekarz", "operator koparki" },
            ["budowa"] = new[] { "pracownik budowlany", "robotnik budowlany", "murarz", "cieśla", "zbrojarz", "tynkarz", "dekarz", "operator koparki" },
            ["kelner"] = new[] { "kelnerka", "obsługa sali", "pracownik restauracji", "serwisant restauracji" },
            ["kucharz"] = new[] { "pomoc kuchenna", "szef kuchni", "cook", "chef" },
            ["recepcjonista"] = new[] { "recepcjonistka", "pracownik recepcji", "front desk" },
            ["księgowy"] = new[] { "księgowa", "samodzielna księgowa", "junior accountant", "accountant" },
            ["księgowa"] = new[] { "księgowy", "samodzielna księgowa", "junior accountant", "accountant" },
            ["mechanik"] = new[] { "mechanik samochodowy", "technik serwisu", "serwisant", "technik utrzymania ruchu" },
            ["elektryk"] = new[] { "elektromonter", "technik elektryk", "automatyk", "technik utrzymania ruchu" },
            ["hydraulik"] = new[] { "instalator sanitarny", "monter instalacji", "technik instalacji" },
            ["spawacz"] = new[] { "monter konstrukcji", "ślusarz", "spawacz mig", "spawacz tig" },
            ["ślusarz"] = new[] { "spawacz", "monter konstrukcji", "pracownik produkcji metalowej" },
            ["pracownik biurowy"] = new[] { "asystent biura", "asystentka biura", "office assistant", "pracownik administracyjny", "administracja biurowa" },
            ["biuro"] = new[] { "pracownik biurowy", "asystent biura", "pracownik administracyjny", "administracja biurowa" },
            ["sekretarka"] = new[] { "asystentka", "asystent zarządu", "pracownik biurowy", "recepcjonistka" },
            ["asystent"] = new[] { "asystentka", "assistant", "pracownik biurowy", "office assistant" },
            ["obsługa klienta"] = new[] { "doradca klienta", "customer service", "konsultant klienta", "pracownik obsługi klienta" },
            ["kurier"] = new[] { "dostawca", "kierowca", "kierowca kat b", "driver" },
            ["ochroniarz"] = new[] { "pracownik ochrony", "ochrona", "security officer" },
            ["ochrona"] = new[] { "pracownik ochrony", "ochroniarz", "security officer" },
            ["sprzątaczka"] = new[] { "sprzątanie", "pracownik sprzątający", "serwis sprzątający", "pokojowa" },
            ["sprzątanie"] = new[] { "sprzątaczka", "pracownik sprzątający", "serwis sprzątający", "pokojowa" },
            ["pielęgniarka"] = new[] { "pielęgniarz", "ratownik medyczny", "opiekun medyczny" },
            ["opiekun"] = new[] { "opiekunka", "opiekun osób starszych", "caregiver", "opiekun medyczny" },
            ["fryzjer"] = new[] { "barber", "stylista fryzur", "hair stylist" },
            ["kosmetyczka"] = new[] { "kosmetolog", "stylistka paznokci", "beautician" },
            ["handlowiec"] = new[] { "przedstawiciel handlowy", "sales representative", "doradca handlowy" },
            ["handel"] = new[] { "przedstawiciel handlowy", "handlowiec", "doradca handlowy", "sales representative" },
            ["produkcja"] = new[] { "pracownik produkcji", "operator produkcji", "operator maszyn", "monter" },
            ["operator"] = new[] { "operator maszyn", "operator wózka widłowego", "operator produkcji", "operator koparki" },
            ["wózek"] = new[] { "operator wózka widłowego", "magazynier", "forklift operator" },
            ["wózki"] = new[] { "operator wózka widłowego", "magazynier", "forklift operator" },
            ["nauczyciel"] = new[] { "pedagog", "wychowawca", "teacher", "lektor" },
            ["lektor"] = new[] { "nauczyciel języka", "teacher", "native speaker" },
            ["farmaceuta"] = new[] { "technik farmaceutyczny", "apteka", "pharmacist" },
            ["dentysta"] = new[] { "stomatolog", "lekarz dentysta" },
            ["lekarz"] = new[] { "medyk", "doctor", " medyczny" }
        };
        private static readonly string[] PolishTextIndicators =
        {
            @"\b(i|oraz|lub|ale|dla|praca|prac[aeęy]|umowa|etat|zatrudnienie|wynagrodzenie|wymagania|obowiazki|obowiązki|oferujemy|szukamy|kandydat|klienta|pracownik|doswiadczenie|doświadczenie|pelny|pełny|zdalna|stacjonarna)\b"
        };

        private static readonly string[] EnglishTextIndicators =
        {
            @"\b(the|and|or|but|for|with|from|your|you|we|our|their|this|that|will|are|is|have|has|work|working|job|role|position|requirements|responsibilities|experience|required|skills|candidate|company|team|apply|benefits|full[- ]time|part[- ]time|remote|onsite|hybrid|customer|business|support|engineer|developer)\b"
        };

        private static readonly string[] GermanTextIndicators =
        {
            @"\b(und|oder|aber|nicht|mit|fur|für|uber|über|wir|sie|ihre|deine|diese|arbeit|stelle|vollzeit|teilzeit|aufgaben|anforderungen|kenntnisse|erfahrung|berufserfahrung|bewerbung|deutschland|deutsch|kunden|teamfahigkeit|teamfähigkeit|verantwortung|arbeitszeit|leistungen|m\/w\/d|entwickler)\b"
        };
        private readonly string appId;
        private readonly string appKey;
        private readonly string joobleApiKey;
        private readonly string ePracaPartnerName;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly IUserStore _userStore;
        private readonly IAppSessionStore _sessionStore;
        private readonly IUrlLauncher _urlLauncher;
        private readonly PostgresJobReader _jobReader;
        private string _lastSearchQuery = string.Empty;
        private string _lastSearchLocation = string.Empty;
        private readonly List<JobOffer> _loadedSearchResults = new();
        private string _currentLogin = string.Empty;

        public bool IsLoading { get; set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public string LastApiError { get; private set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string Location { get; set; } = "Rzeszów";
        public int Distance { get; set; } = 10;
        public int MinSalary { get; set; }
        public int MaxSalary { get; set; }
        public string JobRange { get; set; } = "Wszystkie";
        public bool RemoteOnly { get; set; } = false;
        public bool NotificationsEnabled { get; set; }
        public bool DarkModeEnabled { get; set; }
        /// <summary>
        /// Aktualnie zalogowany profil albo profil tymczasowy użytkownika niezalogowanego.
        /// </summary>
        /// <value>Wartość jest odświeżana po rejestracji, logowaniu, wylogowaniu i wczytaniu danych z magazynu.</value>
        public UserProfile CurrentUser { get; private set; } = new UserProfile();
        public bool IsLoggedIn { get; private set; }

        public List<string> SelectedContracts { get; set; } = new();
        public string SelectedExperience { get; set; } = "Wszystkie";
        public string SelectedEducation { get; set; } = "Brak wymagań lub niewymagane";
        public List<JobOffer> AllJobOffers { get; set; } = new();
        public List<JobOffer> FavoriteOffers { get; set; } = new();
        public int CurrentResultsPage { get; private set; } = 1;
        public bool HasNextPage { get; private set; }
        public bool HasPreviousPage => CurrentResultsPage > 1;
        public int ResultsPerPage => DisplayedResultsPerPage;
        public bool HasRegisteredAccount => _userStore.AnyAccounts();
        /// <summary>
        /// Sprawdza, czy dane źródło obsługuje wyszukiwanie z promieniem odległości.
        /// </summary>
        /// <param name="source">Nazwa źródła widoczna w filtrach aplikacji.</param>
        /// <returns><see langword="true"/>, gdy źródło przyjmuje filtr odległości bez lokalnego dopasowywania tekstu.</returns>
        public bool SourceSupportsDistance(string source) =>
            string.Equals(source, "Adzuna", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "Jooble", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Określa, czy oferta spełnia bieżące kryteria lokalizacji i trybu zdalnego.
        /// </summary>
        /// <param name="offer">Oferta sprawdzana względem aktualnych filtrów usługi.</param>
        /// <returns><see langword="true"/>, gdy oferta powinna pozostać na liście wyników.</returns>
        public bool MatchesLocationFilter(JobOffer offer)
        {
            if (RemoteOnly)
            {
                return offer.IsRemote;
            }

            if (string.IsNullOrWhiteSpace(Location) || string.Equals(Location, "Wszystkie", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!SourceSupportsDistance(offer.Source))
            {
                return offer.Location.Contains(Location, StringComparison.OrdinalIgnoreCase);
            }

            if (Distance <= 0)
            {
                return offer.Location.Contains(Location, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        public event Action? OnChange;

        /// <summary>
        /// Tworzy usługę wyszukiwania z zależnościami odpowiednimi dla aktualnej platformy.
        /// </summary>
        /// <param name="userStore">Magazyn kont użytkowników, np. lokalny JSON albo PostgreSQL.</param>
        /// <param name="settingsProvider">Provider ustawień zawierający klucze API i konfigurację bazy.</param>
        /// <param name="sessionStore">Magazyn krótkotrwałej sesji logowania.</param>
        /// <param name="urlLauncher">Usługa otwierająca linki zewnętrzne do ofert.</param>
        /// <param name="jobReader">Czytnik ofert zaimportowanych wcześniej do PostgreSQL.</param>
        public JobSearchService(IUserStore userStore, AppSettingsProvider settingsProvider, IAppSessionStore sessionStore, IUrlLauncher urlLauncher, PostgresJobReader jobReader)
        {
            _userStore = userStore;
            _sessionStore = sessionStore;
            _urlLauncher = urlLauncher;
            _jobReader = jobReader;
            var settings = settingsProvider.GetSettings();
            appId = settings.JobSources.Adzuna.AppId;
            appKey = settings.JobSources.Adzuna.AppKey;
            joobleApiKey = settings.JobSources.Jooble.ApiKey;
            ePracaPartnerName = settings.JobSources.EPraca.PartnerName;
        }

        public Dictionary<string, bool> LanguageFilters { get; set; } = new()
        {
            { "Angielski", false },
            { "Niemiecki", false },
            { "Ukraiński", false },
            { "Francuski", false }
        };

        public Dictionary<string, bool> SourceFilters { get; set; } = new()
        {
            { "Adzuna", true },
            { "Jooble", true },
            { "Remotive", true },
            { "Arbeitnow", true },
            { EPracaSourceName, false }
        };

        public List<JobRoleOption> RoleOptions { get; private set; } = new();
        public List<JobCriterionOption> CriteriaOptions { get; private set; } = new();
        public string SelectedCategoryCode { get; private set; } = string.Empty;
        public string SelectedRoleCode { get; private set; } = string.Empty;
        public Dictionary<string, bool> CriterionFilters { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> StructuredFilters { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool HideLowQualityOffers { get; set; }
        public Dictionary<string, int> CriterionMatchThresholds { get; private set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            { "technology", 50 },
            { "trait", 50 },
            { "work_activity", 50 },
            { "certification", 50 }
        };

        public IEnumerable<JobRoleOption> CategoryOptions => RoleOptions
            .GroupBy(option => option.CategoryCode, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group
                    .OrderBy(option => option.CategoryName)
                    .First();

                return new JobRoleOption
                {
                    CategoryCode = first.CategoryCode,
                    CategoryName = first.CategoryName,
                    ActiveOfferCount = group.Sum(option => option.ActiveOfferCount)
                };
            })
            .OrderBy(option => option.CategoryName);

        public IEnumerable<JobRoleOption> VisibleRoleOptions => string.IsNullOrWhiteSpace(SelectedCategoryCode)
            ? Enumerable.Empty<JobRoleOption>()
            : RoleOptions
                .Where(option => string.Equals(option.CategoryCode, SelectedCategoryCode, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(option => option.ActiveOfferCount)
                .ThenBy(option => option.RoleName);

        /// <summary>
        /// Powiadamia widoki, że stan usługi zmienił się i należy odświeżyć interfejs.
        /// </summary>
        public void NotifyStateChanged() => OnChange?.Invoke();

        public List<JobOffer> FilteredOffers => AllJobOffers;

        public async Task LoadSearchOptionsAsync()
        {
            try
            {
                if (!_jobReader.IsConfigured)
                {
                    RoleOptions = new List<JobRoleOption>();
                    CriteriaOptions = new List<JobCriterionOption>();
                    PruneCriterionFilters();
                    NotifyStateChanged();
                    return;
                }

                var selectedSources = GetSelectedSources();
                RoleOptions = await _jobReader.LoadRoleOptionsAsync(selectedSources);

                if (!string.IsNullOrWhiteSpace(SelectedCategoryCode) &&
                    !RoleOptions.Any(option => string.Equals(option.CategoryCode, SelectedCategoryCode, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedCategoryCode = string.Empty;
                    SelectedRoleCode = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(SelectedRoleCode) &&
                    !RoleOptions.Any(option => string.Equals(option.RoleCode, SelectedRoleCode, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedRoleCode = string.Empty;
                }

                CriteriaOptions = await _jobReader.LoadCriteriaOptionsAsync(SelectedCategoryCode, SelectedRoleCode, selectedSources);
                PruneCriterionFilters();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd pobierania opcji wyszukiwania: {ex.Message}");
                RoleOptions = new List<JobRoleOption>();
                CriteriaOptions = new List<JobCriterionOption>();
                PruneCriterionFilters();
            }

            NotifyStateChanged();
        }

        public async Task SelectCategoryAsync(string categoryCode)
        {
            SelectedCategoryCode = categoryCode?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(SelectedCategoryCode))
            {
                SelectedRoleCode = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(SelectedRoleCode))
            {
                var roleStillVisible = VisibleRoleOptions.Any(option =>
                    string.Equals(option.RoleCode, SelectedRoleCode, StringComparison.OrdinalIgnoreCase));

                if (!roleStillVisible)
                {
                    SelectedRoleCode = string.Empty;
                }
            }

            CriterionFilters.Clear();
            await ReloadCriteriaOptionsAsync();
            RefreshLocalMatchScores();
            NotifyStateChanged();
        }

        public async Task SelectRoleAsync(string roleCode)
        {
            SelectedRoleCode = roleCode?.Trim() ?? string.Empty;

            var selectedRole = RoleOptions.FirstOrDefault(option =>
                string.Equals(option.RoleCode, SelectedRoleCode, StringComparison.OrdinalIgnoreCase));

            if (selectedRole != null)
            {
                SelectedCategoryCode = selectedRole.CategoryCode;
            }

            CriterionFilters.Clear();
            await ReloadCriteriaOptionsAsync();
            RefreshLocalMatchScores();
            NotifyStateChanged();
        }

        public async Task SetSourceFilterAsync(string source, bool enabled)
        {
            if (SourceFilters.ContainsKey(source))
            {
                SourceFilters[source] = enabled;
            }

            await LoadSearchOptionsAsync();
            RefreshLocalMatchScores();
        }

        public bool IsCriterionSelected(string code)
        {
            return CriterionFilters.TryGetValue(code, out var selected) && selected;
        }

        public void SetCriterionFilter(string code, bool selected)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            if (selected)
            {
                CriterionFilters[code] = true;
            }
            else
            {
                CriterionFilters.Remove(code);
            }

            RefreshLocalMatchScores();
            NotifyStateChanged();
        }

        public bool IsStructuredFilterSelected(string kind, string code)
        {
            return StructuredFilters.TryGetValue(BuildStructuredFilterKey(kind, code), out var selected) && selected;
        }

        public void SetStructuredFilter(string kind, string code, bool selected)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var key = BuildStructuredFilterKey(kind, code);
            if (selected)
            {
                StructuredFilters[key] = true;
            }
            else
            {
                StructuredFilters.Remove(key);
            }

            RefreshLocalMatchScores();
            NotifyStateChanged();
        }

        public bool MatchesStructuredFilters(JobOffer offer)
        {
            if (HideLowQualityOffers && (offer.DataQualityScore < 40 || offer.ExtractionScore < 25 || offer.DescriptionQuality == "missing"))
            {
                return false;
            }

            var selected = StructuredFilters
                .Where(filter => filter.Value)
                .Select(filter => filter.Key.Split('|', 2))
                .Where(parts => parts.Length == 2)
                .GroupBy(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

            foreach (var group in selected)
            {
                var codes = group.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var matched = group.Key switch
                {
                    "work_mode" => codes.Contains(offer.WorkMode),
                    "work_time" => codes.Contains(offer.WorkTimeType),
                    "contract" => offer.ContractTypes.Any(codes.Contains) || codes.Contains(offer.ContractType),
                    "benefit" => offer.BenefitCodes.Any(codes.Contains),
                    "schedule_exclude" => !offer.ScheduleFlags.Any(codes.Contains),
                    _ => true
                };

                if (!matched)
                {
                    return false;
                }
            }

            return true;
        }

        public string GetStructuredFilterLabel(string kind)
        {
            return kind switch
            {
                "work_mode" => "Tryb pracy",
                "work_time" => "Wymiar pracy",
                "contract" => "Umowa",
                "benefit" => "Benefity",
                "schedule_exclude" => "Harmonogram bez",
                _ => "Filtry"
            };
        }

        private static string BuildStructuredFilterKey(string kind, string code)
            => $"{kind}|{code}";

        public int GetCriterionThreshold(string kind)
        {
            return CriterionMatchThresholds.TryGetValue(kind, out var threshold) ? threshold : 50;
        }

        public void SetCriterionThreshold(string kind, int threshold)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return;
            }

            CriterionMatchThresholds[kind] = Math.Clamp((threshold / 10) * 10, 10, 100);
            RefreshLocalMatchScores();
            NotifyStateChanged();
        }

        public bool MatchesSelectedRoleAndCriteria(JobOffer offer)
        {
            if (!string.IsNullOrWhiteSpace(SelectedRoleCode) &&
                !offer.RoleCodes.Contains(SelectedRoleCode, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedRoleCode) &&
                !string.IsNullOrWhiteSpace(SelectedCategoryCode) &&
                !offer.CategoryCodes.Contains(SelectedCategoryCode, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var group in offer.Criteria.Where(criterion => criterion.IsRequired).GroupBy(criterion => criterion.Kind))
            {
                var requiredCodes = group
                    .Select(criterion => criterion.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!requiredCodes.Any())
                {
                    continue;
                }

                var selectedCodes = GetKnownCriterionCodesForMatching();

                var matchedCount = requiredCodes.Count(code => selectedCodes.Contains(code));
                var requiredPercent = GetCriterionThreshold(group.Key);
                var matchedPercent = (int)Math.Floor(matchedCount * 100m / requiredCodes.Count);

                if (matchedPercent < requiredPercent)
                {
                    return false;
                }
            }

            return true;
        }

        public bool MatchesOfferTextLanguage(JobOffer offer)
        {
            var detectedLanguage = DetectOfferTextLanguage($"{offer.Title} {offer.Description}");
            if (string.IsNullOrWhiteSpace(detectedLanguage))
            {
                return true;
            }

            return LanguageFilters.TryGetValue(detectedLanguage, out var isSelected) && isSelected;
        }

        private HashSet<string> GetKnownCriterionCodesForMatching()
        {
            return CriterionFilters
                .Where(filter => filter.Value)
                .Select(filter => filter.Key)
                .Concat(CurrentUser.Settings.GetProfileCriterionCodes())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public string GetCriterionGroupLabel(string kind)
        {
            return kind switch
            {
                "technology" => "Znane technologie",
                "programming_language" => "Znane języki programowania",
                "framework" => "Znane frameworki",
                "database" => "Znane bazy danych",
                "cloud" => "Chmura",
                "devops_tool" => "Narzędzia DevOps",
                "testing_tool" => "Narzędzia testowe",
                "methodology" => "Metodyki pracy",
                "data_tool" => "Narzędzia danych",
                "ai_tool" => "AI i uczenie maszynowe",
                "integration_tool" => "Integracje i API",
                "business_tool" => "Narzędzia biznesowe",
                "industrial_tool" => "Automatyka i przemysł",
                "trait" => "Cechy, które posiadasz",
                "work_activity" => "Rodzaj pracy, który pasuje",
                "certification" => "Uprawnienia i certyfikaty",
                _ => "Dodatkowe kryteria"
            };
        }

        public string GetCriterionThresholdLabel(string kind)
        {
            return kind switch
            {
                "technology" => "Minimalnie znanych technologii",
                "programming_language" => "Minimalnie znanych języków",
                "framework" => "Minimalnie znanych frameworków",
                "database" => "Minimalnie znanych baz danych",
                "cloud" => "Minimalnie znanych chmur",
                "devops_tool" => "Minimalnie znanych narzędzi",
                "testing_tool" => "Minimalnie znanych narzędzi",
                "methodology" => "Minimalnie zgodnych metodyk",
                "data_tool" => "Minimalnie znanych narzędzi",
                "ai_tool" => "Minimalnie znanych narzędzi AI",
                "integration_tool" => "Minimalnie znanych integracji",
                "business_tool" => "Minimalnie znanych narzędzi",
                "industrial_tool" => "Minimalnie znanych narzędzi",
                "trait" => "Minimalnie zgodnych cech",
                "work_activity" => "Minimalnie zgodnych czynności",
                "certification" => "Minimalnie posiadanych uprawnień",
                _ => "Minimalne dopasowanie"
            };
        }

        private async Task ReloadCriteriaOptionsAsync()
        {
            if (!_jobReader.IsConfigured)
            {
                CriteriaOptions = new List<JobCriterionOption>();
                PruneCriterionFilters();
                return;
            }

            CriteriaOptions = await _jobReader.LoadCriteriaOptionsAsync(SelectedCategoryCode, SelectedRoleCode, GetSelectedSources());
            PruneCriterionFilters();
        }

        private List<string> GetSelectedSources()
        {
            return SourceFilters
                .Where(source => source.Value)
                .Select(source => source.Key)
                .ToList();
        }

        private void PruneCriterionFilters()
        {
            var availableCodes = CriteriaOptions
                .Select(option => option.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var code in CriterionFilters.Keys.ToList())
            {
                if (!availableCodes.Contains(code))
                {
                    CriterionFilters.Remove(code);
                }
            }
        }

        private void RefreshLocalMatchScores()
        {
            if (!AllJobOffers.Any())
            {
                return;
            }

            foreach (var offer in AllJobOffers)
            {
                offer.MatchScore = CalculateMatchingScore(offer);
            }

            AllJobOffers = AllJobOffers
                .OrderByDescending(offer => offer.MatchScore)
                .ToList();

            ResetOfferMatchInsights();
        }

        /// <summary>
        /// Rejestruje nowe konto i natychmiast ustawia je jako aktywną sesję.
        /// </summary>
        /// <param name="username">Login podany w formularzu rejestracji.</param>
        /// <param name="password">Hasło użytkownika, które zostanie zapisane wyłącznie w postaci skrótu.</param>
        /// <param name="errorMessage">Komunikat walidacyjny gotowy do pokazania w UI, gdy operacja się nie powiedzie.</param>
        /// <returns><see langword="true"/>, gdy konto zostało utworzone i zapisane.</returns>
        /// <remarks>
        /// Metoda resetuje stan wyszukiwania, aby nowy użytkownik nie odziedziczył historii ani ulubionych z poprzedniej sesji.
        /// </remarks>
        public bool Register(string username, string password, out string errorMessage)
        {
            var normalizedUsername = username?.Trim() ?? string.Empty;
            var normalizedPassword = password?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                errorMessage = "Podaj login.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(normalizedPassword))
            {
                errorMessage = "Podaj hasło.";
                return false;
            }

            if (_userStore.FindByLogin(normalizedUsername) != null)
            {
                errorMessage = "Konto z takim loginem już istnieje.";
                return false;
            }

            CurrentUser = new UserProfile
            {
                AccountLogin = normalizedUsername,
                Username = normalizedUsername
            };
            FavoriteOffers = new List<JobOffer>();
            ResetTransientSearchState();
            _currentLogin = normalizedUsername;
            IsLoggedIn = true;
            SaveAccount(password: normalizedPassword);
            errorMessage = string.Empty;
            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// Loguje użytkownika po porównaniu skrótu hasła z rekordem zapisanym w <see cref="IUserStore"/>.
        /// </summary>
        /// <param name="username">Login wpisany w formularzu logowania.</param>
        /// <param name="password">Hasło wprowadzone przez użytkownika.</param>
        /// <param name="errorMessage">Powód odmowy logowania albo pusty tekst po sukcesie.</param>
        /// <returns><see langword="true"/>, gdy konto istnieje i hasło jest poprawne.</returns>
        public bool Login(string username, string password, out string errorMessage)
        {
            var normalizedUsername = username?.Trim() ?? string.Empty;
            var normalizedPassword = password?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                errorMessage = "Podaj login.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(normalizedPassword))
            {
                errorMessage = "Podaj hasło.";
                return false;
            }

            var account = _userStore.FindByLogin(normalizedUsername);
            if (account == null || !string.Equals(account.PasswordHash, HashPassword(normalizedUsername, normalizedPassword), StringComparison.Ordinal))
            {
                errorMessage = "Nieprawidłowy login lub hasło.";
                return false;
            }

            LoadAccount(account);
            IsLoggedIn = true;
            _currentLogin = account.Login;
            ResetTransientSearchState();
            SaveSession();
            errorMessage = string.Empty;
            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// Kończy sesję użytkownika i usuwa z pamięci dane profilu oraz wyniki powiązane z kontem.
        /// </summary>
        public void Logout()
        {
            IsLoggedIn = false;
            _currentLogin = string.Empty;
            CurrentUser = new UserProfile();
            FavoriteOffers = new List<JobOffer>();
            ResetTransientSearchState();
            SaveSession();
            NotifyStateChanged();
        }

        /// <summary>
        /// Zmienia hasło aktualnie zalogowanego konta po walidacji obecnego hasła.
        /// </summary>
        /// <param name="currentPassword">Aktualne hasło służące do potwierdzenia tożsamości.</param>
        /// <param name="newPassword">Nowe hasło do zapisania.</param>
        /// <param name="confirmPassword">Powtórzenie nowego hasła z formularza.</param>
        /// <param name="errorMessage">Komunikat błędu walidacji albo pusty tekst po sukcesie.</param>
        /// <returns><see langword="true"/>, gdy hasło zostało zaktualizowane.</returns>
        public bool ChangePassword(string currentPassword, string newPassword, string confirmPassword, out string errorMessage)
        {
            if (!IsLoggedIn || string.IsNullOrWhiteSpace(CurrentUser.AccountLogin))
            {
                errorMessage = "Musisz być zalogowany.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                errorMessage = "Wypełnij wszystkie pola hasła.";
                return false;
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                errorMessage = "Nowe hasła nie są takie same.";
                return false;
            }

            var account = _userStore.FindByLogin(CurrentUser.AccountLogin);
            if (account == null)
            {
                errorMessage = "Nie znaleziono konta użytkownika.";
                return false;
            }

            if (!string.Equals(account.PasswordHash, HashPassword(account.Login, currentPassword), StringComparison.Ordinal))
            {
                errorMessage = "Obecne hasło jest nieprawidłowe.";
                return false;
            }

            SaveAccount(newPassword);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Dodaje ofertę do historii przeglądania bieżącego użytkownika.
        /// </summary>
        /// <param name="offer">Oferta otwarta przez użytkownika.</param>
        /// <remarks>
        /// Historia jest deduplikowana po tytule i firmie oraz ograniczana, aby nie rozrastała się bez kontroli w magazynie konta.
        /// </remarks>
        public void AddToHistory(JobOffer offer)
        {
            var existing = CurrentUser.SearchHistory.FirstOrDefault(o => o.Title == offer.Title && o.Company == offer.Company);
            if (existing != null)
            {
                return;
            }

            CurrentUser.SearchHistory.Insert(0, offer);
            if (CurrentUser.SearchHistory.Count > 20)
            {
                CurrentUser.SearchHistory.RemoveAt(20);
            }

            SaveDataToDevice();
            NotifyStateChanged();
        }

        /// <summary>
        /// Otwiera zewnętrzny adres oferty i zapisuje ją w historii użytkownika.
        /// </summary>
        /// <param name="offer">Oferta, której szczegóły mają zostać otwarte w przeglądarce.</param>
        /// <returns>Zadanie reprezentujące operację uruchomienia adresu URL.</returns>
        public async Task OpenJobDetails(JobOffer offer)
        {
            AddToHistory(offer);

            if (!string.IsNullOrWhiteSpace(offer.Url))
            {
                await _urlLauncher.OpenAsync(offer.Url);
            }
        }

        /// <summary>
        /// Przenosi preferencje zapisane w profilu użytkownika na bieżące filtry wyszukiwania.
        /// </summary>
        /// <remarks>
        /// Metoda jest wywoływana wtedy, gdy użytkownik chce szybko wrócić do swoich długoterminowych preferencji bez ręcznego
        /// ustawiania lokalizacji, wynagrodzenia, doświadczenia, wykształcenia, języków i typów umowy.
        /// </remarks>
        public void ApplyProfileFilters()
        {
            var settings = CurrentUser.Settings;
            Location = settings.DefaultLocation;
            SearchText = settings.JobTitle;
            MinSalary = settings.ExpectedSalaryMin.HasValue
                ? (int)Math.Round(settings.ExpectedSalaryMin.Value)
                : SafeParseSalary(settings.ExpectedSalary);
            MaxSalary = 0;
            SelectedExperience = settings.Experience;
            SelectedEducation = settings.Education;
            JobRange = NormalizeJobRangeFromProfile(settings.PreferredContractTypes);
            Distance = settings.MaxDistanceKm ?? Distance;
            RemoteOnly = string.Equals(settings.WorkMode, "remote", StringComparison.OrdinalIgnoreCase);

            StructuredFilters.Clear();
            if (!string.IsNullOrWhiteSpace(settings.WorkMode) && !string.Equals(settings.WorkMode, "any", StringComparison.OrdinalIgnoreCase))
            {
                StructuredFilters[BuildStructuredFilterKey("work_mode", settings.WorkMode)] = true;
            }

            if (!string.IsNullOrWhiteSpace(settings.WorkTimeType) && !string.Equals(settings.WorkTimeType, "any", StringComparison.OrdinalIgnoreCase))
            {
                StructuredFilters[BuildStructuredFilterKey("work_time", settings.WorkTimeType)] = true;
            }

            foreach (var contractCode in settings.PreferredContractTypes.Select(MapProfileContractToCode).Where(code => !string.IsNullOrWhiteSpace(code)))
            {
                StructuredFilters[BuildStructuredFilterKey("contract", contractCode)] = true;
            }

            CriterionFilters.Clear();
            foreach (var code in settings.GetProfileCriterionCodes())
            {
                CriterionFilters[code] = true;
            }

            var threshold = Math.Clamp((settings.RequiredCriteriaMatchPercent / 10) * 10, 10, 100);
            foreach (var kind in CriterionMatchThresholds.Keys.ToList())
            {
                CriterionMatchThresholds[kind] = threshold;
            }

            foreach (var language in LanguageFilters.Keys.ToList())
            {
                LanguageFilters[language] = settings.KnownLanguages.Contains(language);
            }

            RefreshLocalMatchScores();
            NotifyStateChanged();
        }

        private static string MapProfileContractToCode(string contract)
        {
            return contract switch
            {
                "Umowa o Pracę" or "Umowa o pracę" => "employment_contract",
                "Zlecenie" or "Umowa zlecenie" => "mandate_contract",
                "B2B" => "b2b",
                _ => string.Empty
            };
        }

        private static string NormalizeJobRangeFromProfile(List<string>? preferredContractTypes)
        {
            if (preferredContractTypes == null || preferredContractTypes.Count != 1)
            {
                return "Wszystkie";
            }

            return preferredContractTypes[0] switch
            {
                "Umowa o Pracę" => "Umowa o pracę",
                "Zlecenie" => "Umowa zlecenie",
                "Pełny etat" => "Pełny etat",
                "1/2 etatu" => "Część etatu",
                "1/4 etatu" => "Część etatu",
                _ => "Wszystkie"
            };
        }

        /// <summary>
        /// Próbuje odczytać liczbową wartość wynagrodzenia z tekstu oferty.
        /// </summary>
        /// <param name="salary">Tekst wynagrodzenia zwrócony przez źródło zewnętrzne.</param>
        /// <returns>Pierwsza sensowna kwota wykryta w tekście albo <c>0</c>, gdy nie da się jej odczytać.</returns>
        public int SafeParseSalary(string salary)
        {
            if (string.IsNullOrEmpty(salary))
            {
                return 0;
            }

            try
            {
                var digits = new string(salary.Where(char.IsDigit).ToArray());
                return string.IsNullOrEmpty(digits) ? 0 : int.Parse(digits);
            }
            catch
            {
                return 0;
            }
        }

        public int ConvertHourlyRateToMonthlySalary(int hourlyRate)
        {
            if (hourlyRate <= 0)
            {
                return 0;
            }

            return hourlyRate * AverageWorkHoursPerMonth;
        }

        public bool MatchesMinimumSalary(string salary, int minimumHourlyRate)
        {
            if (minimumHourlyRate <= 0)
            {
                return true;
            }

            var offerSalary = SafeParseSalary(salary);
            if (offerSalary == 0)
            {
                return true;
            }

            return offerSalary >= ConvertHourlyRateToMonthlySalary(minimumHourlyRate);
        }

        public bool MatchesMaximumSalary(string salary, int maximumHourlyRate)
        {
            if (maximumHourlyRate <= 0)
            {
                return true;
            }

            var offerSalary = SafeParseSalary(salary);
            if (offerSalary == 0)
            {
                return true;
            }

            return offerSalary <= ConvertHourlyRateToMonthlySalary(maximumHourlyRate);
        }

        /// <summary>
        /// Oblicza lokalny wynik dopasowania oferty do aktualnych filtrów i preferencji użytkownika.
        /// </summary>
        /// <param name="offer">Oferta oceniana bez udziału usługi AI.</param>
        /// <returns>Wynik w skali 0-100, gdzie większa wartość oznacza lepsze dopasowanie.</returns>
        /// <remarks>
        /// Algorytm celowo łączy kilka sygnałów: wyszukiwaną frazę, lokalizację, widełki wynagrodzenia, doświadczenie,
        /// wykształcenie, języki, rodzaj umowy i tryb zdalny. Dzięki temu ranking nadal działa, gdy <see cref="GeminiMatchService"/>
        /// nie jest skonfigurowany albo zewnętrzne API AI zwróci błąd.
        /// </remarks>
        public int CalculateMatchingScore(JobOffer offer)
        {
            var score = 0;
            var query = SearchText?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(query) && MatchesSearchQuery(offer, query))
            {
                score += 20;
            }

            if (!string.IsNullOrWhiteSpace(SelectedRoleCode) &&
                offer.RoleCodes.Contains(SelectedRoleCode, StringComparer.OrdinalIgnoreCase))
            {
                score += 25;
            }
            else if (!string.IsNullOrWhiteSpace(SelectedCategoryCode) &&
                     offer.CategoryCodes.Contains(SelectedCategoryCode, StringComparer.OrdinalIgnoreCase))
            {
                score += 15;
            }

            var selectedCriteria = GetKnownCriterionCodesForMatching().ToList();

            if (selectedCriteria.Any())
            {
                var matchedCriteria = selectedCriteria.Count(code =>
                    offer.CriterionCodes.Contains(code, StringComparer.OrdinalIgnoreCase));
                score += Math.Min(25, matchedCriteria * 8);
            }

            if (!string.IsNullOrWhiteSpace(Location) &&
                !string.Equals(Location, "Wszystkie", StringComparison.OrdinalIgnoreCase) &&
                offer.Location.Contains(Location, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }

            if (!string.Equals(JobRange, "Wszystkie", StringComparison.OrdinalIgnoreCase) &&
                (offer.ContractTime.Contains(JobRange, StringComparison.OrdinalIgnoreCase) ||
                 offer.ContractType.Contains(JobRange, StringComparison.OrdinalIgnoreCase)))
            {
                score += 20;
            }

            if (MinSalary > 0)
            {
                var salaryText = GetEffectiveSalaryText(offer);
                var offerSalary = SafeParseSalary(salaryText);
                if (offerSalary == 0)
                {
                    score += 5;
                }
                else if (MatchesMinimumSalary(salaryText, MinSalary))
                {
                    score += 10;
                }
            }

            if (!string.Equals(SelectedEducation, "Brak wymagań lub niewymagane", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(offer.Education, SelectedEducation, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }
                else if (string.Equals(offer.Education, "Brak wymagań lub niewymagane", StringComparison.OrdinalIgnoreCase) ||
                         IsLowerEducationMatch(offer.Education, SelectedEducation))
                {
                    score += 5;
                }
            }

            if (!string.Equals(SelectedExperience, "Wszystkie", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(offer.Experience, SelectedExperience, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }
                else if (string.Equals(offer.Experience, "Wszystkie", StringComparison.OrdinalIgnoreCase) ||
                         IsLowerExperienceMatch(offer.Experience, SelectedExperience))
                {
                    score += 5;
                }
            }

            var selectedLanguages = LanguageFilters
                .Where(language => language.Value)
                .Select(language => language.Key)
                .ToList();

            if (selectedLanguages.Any() &&
                offer.Languages.Any(language => selectedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)))
            {
                score += 10;
            }

            return Math.Clamp(score, 0, 100);
        }

        /// <summary>
        /// Sprawdza, czy oferta pasuje do wyszukiwanej frazy lub jej synonimów branżowych.
        /// </summary>
        /// <param name="offer">Oferta sprawdzana względem zapytania.</param>
        /// <param name="query">Fraza wpisana przez użytkownika.</param>
        /// <returns><see langword="true"/>, gdy tytuł, firma, opis lub tagi zawierają dopasowanie.</returns>
        public bool MatchesSearchQuery(JobOffer offer, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return TitleMatchesSearchQuery(offer, query) || DescriptionMatchesSearchQuery(offer, query);
        }

        public bool TitleMatchesSearchQuery(JobOffer offer, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return TextMatchesSearchVariants(offer.Title ?? string.Empty, query, includeStemVariants: false);
        }

        public bool DescriptionMatchesSearchQuery(JobOffer offer, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return TextMatchesSearchVariants(offer.Description ?? string.Empty, query, includeStemVariants: false);
        }

        public bool HasAnyTitleMatch(IEnumerable<JobOffer> offers, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            return offers.Any(offer => TitleMatchesSearchQuery(offer, query));
        }

        public bool IsSearchMatchForDisplay(JobOffer offer, string query, bool hasAnyTitleMatch)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            if (hasAnyTitleMatch)
            {
                return TitleMatchesSearchQuery(offer, query);
            }

            return DescriptionMatchesSearchQuery(offer, query);
        }

        private static bool TextMatchesSearchVariants(string text, string query, bool includeStemVariants)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            var variants = BuildSearchQueries(query, includeStemVariants);
            var tokens = Tokenize(text);
            var tokenSet = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
            return variants.Any(variant => VariantMatchesHaystack(variant, text, tokenSet));
        }

        private static bool VariantMatchesHaystack(string variant, string haystack, HashSet<string> haystackTokenSet)
        {
            if (string.IsNullOrWhiteSpace(variant))
            {
                return false;
            }

            var normalizedVariant = variant.Trim();
            if (normalizedVariant.Contains(' ', StringComparison.Ordinal))
            {
                return haystack.Contains(normalizedVariant, StringComparison.OrdinalIgnoreCase);
            }

            if (normalizedVariant.Length <= 2)
            {
                return haystackTokenSet.Contains(normalizedVariant);
            }

            return haystack.Contains(normalizedVariant, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Usuwa poprzednie uzasadnienia i flagi AI z aktualnej listy wyników.
        /// </summary>
        /// <remarks>
        /// Reset jest wykonywany przed ponownym rankingiem, aby użytkownik nie widział starego uzasadnienia przy wynikach
        /// przeliczonych już według innych filtrów.
        /// </remarks>
        public void ResetOfferMatchInsights()
        {
            foreach (var offer in AllJobOffers)
            {
                offer.HasAiMatchScore = false;
                offer.MatchReason = BuildFallbackMatchReason(offer, null);
            }
        }

        public void MarkAiFallback(string? reason)
        {
            foreach (var offer in AllJobOffers)
            {
                offer.HasAiMatchScore = false;
                offer.MatchReason = BuildFallbackMatchReason(offer, reason);
            }
        }

        public void RefreshOfferOrdering()
        {
            AllJobOffers = AllJobOffers
                .OrderByDescending(offer => offer.MatchScore)
                .ToList();

            HasNextPage = AllJobOffers.Count > DisplayedResultsPerPage;
            NotifyStateChanged();
        }

        private string BuildFallbackMatchReason(JobOffer offer, string? aiFailureReason)
        {
            var reasons = new List<string>();
            var query = SearchText?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(query) && MatchesSearchQuery(offer, query))
            {
                reasons.Add("+20 za zgodne słowa kluczowe lub stanowisko");
            }

            if (!string.IsNullOrWhiteSpace(SelectedRoleCode) &&
                offer.RoleCodes.Contains(SelectedRoleCode, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("+25 za zgodną rolę z bazy");
            }
            else if (!string.IsNullOrWhiteSpace(SelectedCategoryCode) &&
                     offer.CategoryCodes.Contains(SelectedCategoryCode, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("+15 za zgodną kategorię z bazy");
            }

            var selectedCriteria = GetKnownCriterionCodesForMatching().ToList();

            if (selectedCriteria.Any())
            {
                var matchedCriteria = selectedCriteria.Count(code =>
                    offer.CriterionCodes.Contains(code, StringComparer.OrdinalIgnoreCase));

                if (matchedCriteria > 0)
                {
                    reasons.Add($"+{Math.Min(25, matchedCriteria * 8)} za wybrane kryteria");
                }
            }

            var requirementSummary = BuildRequirementMatchSummary(offer);
            if (!string.IsNullOrWhiteSpace(requirementSummary))
            {
                reasons.Add(requirementSummary);
            }

            if (!string.Equals(JobRange, "Wszystkie", StringComparison.OrdinalIgnoreCase) &&
                (offer.ContractTime.Contains(JobRange, StringComparison.OrdinalIgnoreCase) ||
                 offer.ContractType.Contains(JobRange, StringComparison.OrdinalIgnoreCase)))
            {
                reasons.Add("+20 za zgodny zakres pracy");
            }

            if (MinSalary > 0)
            {
                var salaryText = GetEffectiveSalaryText(offer);
                var offerSalary = SafeParseSalary(salaryText);
                if (offerSalary == 0)
                {
                    reasons.Add("+5 za wynagrodzenie do uzgodnienia");
                }
                else if (MatchesMinimumSalary(salaryText, MinSalary))
                {
                    reasons.Add("+10 za wynagrodzenie powyżej minimum");
                }
            }

            if (!string.Equals(SelectedEducation, "Brak wymagań lub niewymagane", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(offer.Education, SelectedEducation, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("+10 za zgodne wykształcenie");
                }
                else if (string.Equals(offer.Education, "Brak wymagań lub niewymagane", StringComparison.OrdinalIgnoreCase) ||
                         IsLowerEducationMatch(offer.Education, SelectedEducation))
                {
                    reasons.Add("+5 za niższe lub niewymagane wykształcenie");
                }
            }

            if (!string.Equals(SelectedExperience, "Wszystkie", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(offer.Experience, SelectedExperience, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("+10 za zgodne doświadczenie");
                }
                else if (string.Equals(offer.Experience, "Wszystkie", StringComparison.OrdinalIgnoreCase) ||
                         IsLowerExperienceMatch(offer.Experience, SelectedExperience))
                {
                    reasons.Add("+5 za niższe lub niepodane doświadczenie");
                }
            }

            var selectedLanguages = LanguageFilters
                .Where(item => item.Value)
                .Select(item => item.Key)
                .ToList();

            if (selectedLanguages.Any() &&
                offer.Languages.Any(language => selectedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)))
            {
                reasons.Add("+10 za wymagany znany język");
            }

            if (!string.IsNullOrWhiteSpace(Location) &&
                !string.Equals(Location, "Wszystkie", StringComparison.OrdinalIgnoreCase) &&
                offer.Location.Contains(Location, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("+20 za zgodną lokalizację");
            }

            var localReason = reasons.Any()
                ? $"Dopasowanie obliczone lokalnie: {string.Join(", ", reasons)}."
                : "Dopasowanie obliczone lokalnie: oferta nie dostała dodatkowych punktów z wybranych kryteriów.";

            if (string.IsNullOrWhiteSpace(aiFailureReason))
            {
                return localReason;
            }

            return $"{localReason} Gemini było chwilowo niedostępne, więc użyto dopasowania lokalnego.";
        }

        public string BuildRequirementMatchSummary(JobOffer offer)
        {
            var requiredCodes = offer.Criteria
                .Where(criterion => criterion.IsRequired)
                .Select(criterion => criterion.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!requiredCodes.Any())
            {
                return "Nie wykryto twardych wymagań w kryteriach oferty";
            }

            var knownCodes = GetKnownCriterionCodesForMatching();
            var matched = requiredCodes.Where(knownCodes.Contains).ToList();
            var missing = requiredCodes.Where(code => !knownCodes.Contains(code)).Take(6).ToList();

            var summary = $"masz {matched.Count}/{requiredCodes.Count} wymaganych kryteriów";
            if (missing.Any())
            {
                summary += $"; brakuje: {string.Join(", ", missing)}";
            }

            return summary;
        }

        private static double CalculateTokenOverlapScore(string query, string text, double maxPoints)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(text) || maxPoints <= 0)
            {
                return 0;
            }

            var queryTokens = Tokenize(query);
            if (queryTokens.Count == 0)
            {
                return 0;
            }

            var textTokens = Tokenize(text);
            if (textTokens.Count == 0)
            {
                return 0;
            }

            var matchedTokens = queryTokens.Count(token =>
                textTokens.Any(textToken => textToken.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                            token.Contains(textToken, StringComparison.OrdinalIgnoreCase)));

            var ratio = matchedTokens / (double)queryTokens.Count;
            return Math.Round(ratio * maxPoints, 1);
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return Regex.Split(text.ToLowerInvariant(), @"[^a-zA-Z0-9ąćęłńóśźż]+")
                .Where(token => token.Length >= 3)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Dodaje ofertę do ulubionych albo usuwa ją, jeżeli już była zapisana.
        /// </summary>
        /// <param name="offer">Oferta przełączana na liście ulubionych bieżącego użytkownika.</param>
        public void ToggleFavorite(JobOffer offer)
        {
            var existing = FavoriteOffers.FirstOrDefault(o => o.Title == offer.Title && o.Company == offer.Company);
            if (existing != null)
            {
                FavoriteOffers.Remove(existing);
            }
            else
            {
                FavoriteOffers.Add(offer);
            }

            SaveDataToDevice();
            NotifyStateChanged();
        }

        public void SaveDataToDevice()
        {
            try
            {
                SaveAccount();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public void LoadDataFromDevice()
        {
            try
            {
                TryMigrateLegacyAccount();

                _currentLogin = _sessionStore.GetString(SessionLoginKey, string.Empty);
                IsLoggedIn = _sessionStore.GetBool(SessionIsLoggedInKey, false);

                if (IsLoggedIn && !string.IsNullOrWhiteSpace(_currentLogin))
                {
                    var account = _userStore.FindByLogin(_currentLogin);
                    if (account != null)
                    {
                        LoadAccount(account);
                    }
                    else
                    {
                        IsLoggedIn = false;
                        _currentLogin = string.Empty;
                        ResetTransientSearchState();
                    }
                }
                else
                {
                    ResetTransientSearchState();
                }

                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void SaveAccount(string? password = null)
        {
            if (string.IsNullOrWhiteSpace(CurrentUser.AccountLogin))
            {
                SaveSession();
                return;
            }

            var existing = _userStore.FindByLogin(CurrentUser.AccountLogin);
            var account = new UserAccountRecord
            {
                Login = CurrentUser.AccountLogin,
                PasswordHash = password != null
                    ? HashPassword(CurrentUser.AccountLogin, password)
                    : existing?.PasswordHash ?? string.Empty,
                Profile = CurrentUser,
                FavoriteOffers = FavoriteOffers,
                CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _userStore.SaveAccount(account);
            _currentLogin = account.Login;
            SaveSession();
        }

        private void SaveSession()
        {
            _sessionStore.SetString(SessionLoginKey, _currentLogin);
            _sessionStore.SetBool(SessionIsLoggedInKey, IsLoggedIn);
        }

        private void ResetTransientSearchState()
        {
            SearchText = string.Empty;
            MinSalary = 0;
            MaxSalary = 0;
            JobRange = "Wszystkie";
            RemoteOnly = false;
            SelectedExperience = "Wszystkie";
            SelectedEducation = "Brak wymagań lub niewymagane";
            CurrentResultsPage = 1;
            HasNextPage = false;
            StatusMessage = string.Empty;
            LastApiError = string.Empty;
            IsLoading = false;
            SelectedCategoryCode = string.Empty;
            SelectedRoleCode = string.Empty;
            CriterionFilters.Clear();
            AllJobOffers = new List<JobOffer>();
            _loadedSearchResults.Clear();

            foreach (var language in LanguageFilters.Keys.ToList())
            {
                LanguageFilters[language] = false;
            }
        }

        private void LoadAccount(UserAccountRecord account)
        {
            CurrentUser = account.Profile ?? new UserProfile();
            CurrentUser.AccountLogin = account.Login;
            if (string.IsNullOrWhiteSpace(CurrentUser.Username))
            {
                CurrentUser.Username = account.Login;
            }

            FavoriteOffers = account.FavoriteOffers ?? new List<JobOffer>();
        }

        private void TryMigrateLegacyAccount()
        {
            if (_userStore.AnyAccounts())
            {
                return;
            }

            var legacyLogin = _sessionStore.GetString(LegacyAuthUsernameKey, string.Empty);
            var legacyPassword = _sessionStore.GetString(LegacyAuthPasswordKey, string.Empty);
            if (string.IsNullOrWhiteSpace(legacyLogin) || string.IsNullOrWhiteSpace(legacyPassword))
            {
                return;
            }

            var userJson = _sessionStore.GetString("user_profile", string.Empty);
            var profile = string.IsNullOrEmpty(userJson)
                ? new UserProfile()
                : JsonConvert.DeserializeObject<UserProfile>(userJson) ?? new UserProfile();

            profile.AccountLogin = legacyLogin;
            if (string.IsNullOrWhiteSpace(profile.Username) || profile.Username == "Gość")
            {
                profile.Username = legacyLogin;
            }

            var favsJson = _sessionStore.GetString("user_favorites", string.Empty);
            var favorites = string.IsNullOrEmpty(favsJson)
                ? new List<JobOffer>()
                : JsonConvert.DeserializeObject<List<JobOffer>>(favsJson) ?? new List<JobOffer>();

            _userStore.SaveAccount(new UserAccountRecord
            {
                Login = legacyLogin,
                PasswordHash = HashPassword(legacyLogin, legacyPassword),
                Profile = profile,
                FavoriteOffers = favorites,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        private static string HashPassword(string login, string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{login}:{password}"));
            return Convert.ToHexString(bytes);
        }

        /// <summary>
        /// Pobiera oferty pracy z aktywnych źródeł i zapisuje pierwszą stronę wyników w stanie aplikacji.
        /// </summary>
        /// <param name="query">Fraza stanowiska lub branży wpisana przez użytkownika.</param>
        /// <param name="location">Lokalizacja przekazana do źródeł wspierających wyszukiwanie geograficzne.</param>
        /// <returns>Zadanie kończące się po pobraniu, filtrowaniu i uporządkowaniu wyników.</returns>
        /// <remarks>
        /// Metoda łączy wyniki z bazy PostgreSQL oraz zewnętrznych API. Przy błędach pojedynczych źródeł zachowuje możliwe wyniki
        /// z pozostałych źródeł i zapisuje komunikat w <see cref="LastApiError"/>.
        /// </remarks>
        public async Task SearchJobsOnlineAsync(string query, string location)
        {
            _lastSearchQuery = query;
            _lastSearchLocation = location;
            _loadedSearchResults.Clear();
            AllJobOffers = new List<JobOffer>();
            CurrentResultsPage = 1;
            HasNextPage = false;
            IsLoading = true;
            StatusMessage = "Pobieram oferty z bazy danych...";
            LastApiError = string.Empty;
            NotifyStateChanged();

            try
            {
                var normalizedQuery = _lastSearchQuery?.Trim() ?? string.Empty;
                var normalizedLocation = string.IsNullOrWhiteSpace(_lastSearchLocation) || _lastSearchLocation == "Wszystkie" ? string.Empty : _lastSearchLocation.Trim();
                var remoteOnly = RemoteOnly;
                var selectedSources = SourceFilters
                    .Where(source => source.Value)
                    .Select(source => source.Key)
                    .ToList();

                if (!selectedSources.Any())
                {
                    StatusMessage = "Wybierz przynajmniej jedno źródło ofert.";
                    return;
                }

                if (!_jobReader.IsConfigured)
                {
                    StatusMessage = "Brak konfiguracji połączenia z bazą danych.";
                    return;
                }

                await LoadSearchOptionsAsync();
                var offers = await _jobReader.LoadActiveOffersAsync(selectedSources);

                _loadedSearchResults.AddRange(offers);
                AllJobOffers = _loadedSearchResults.ToList();

                foreach (var offer in AllJobOffers)
                {
                    offer.MatchScore = CalculateMatchingScore(offer);
                }

                AllJobOffers = AllJobOffers
                    .OrderByDescending(offer => offer.MatchScore)
                    .ToList();

                ResetOfferMatchInsights();
                HasNextPage = _loadedSearchResults.Count > DisplayedResultsPerPage;
                StatusMessage = _loadedSearchResults.Count > 0
                    ? BuildStatusMessage(_loadedSearchResults.Count)
                    : "Nie znaleziono ofert w bazie danych dla wybranych kryteriów.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd: {ex.Message}");
                StatusMessage = string.IsNullOrWhiteSpace(LastApiError)
                    ? $"Wystąpił błąd podczas wyszukiwania: {ex.Message}"
                    : LastApiError;
            }
            finally
            {
                IsLoading = false;
                NotifyStateChanged();
            }
        }

        public async Task LoadNextPageAsync()
        {
            if (IsLoading)
            {
                return;
            }

            CurrentResultsPage += 1;
            NotifyStateChanged();
            await Task.CompletedTask;
        }

        public async Task LoadPreviousPageAsync()
        {
            if (!HasPreviousPage || IsLoading)
            {
                return;
            }

            CurrentResultsPage -= 1;
            NotifyStateChanged();
            await Task.CompletedTask;
        }

        public void SetCurrentResultsPage(int pageNumber)
        {
            CurrentResultsPage = Math.Max(1, pageNumber);
            NotifyStateChanged();
        }

        private async Task<List<JobOffer>> FetchAllOffersAsync(string query, string location, bool remoteOnly, List<string> selectedSources)
        {
            var providerTasks = new List<Task<List<JobOffer>>>();

            if (selectedSources.Contains("Adzuna", StringComparer.OrdinalIgnoreCase))
            {
                providerTasks.Add(FetchSourceSafelyAsync("Adzuna", () => FetchAdzunaOffersAsync(query, location, remoteOnly)));
            }

            if (selectedSources.Contains("Jooble", StringComparer.OrdinalIgnoreCase))
            {
                providerTasks.Add(FetchSourceSafelyAsync("Jooble", () => FetchJoobleOffersAsync(query, location, remoteOnly)));
            }

            if (selectedSources.Contains("Remotive", StringComparer.OrdinalIgnoreCase))
            {
                providerTasks.Add(FetchSourceSafelyAsync("Remotive", () => FetchRemotiveOffersAsync(query, remoteOnly)));
            }

            if (selectedSources.Contains("Arbeitnow", StringComparer.OrdinalIgnoreCase))
            {
                providerTasks.Add(FetchSourceSafelyAsync("Arbeitnow", () => FetchArbeitnowOffersAsync(query, remoteOnly)));
            }

            if (selectedSources.Contains(EPracaSourceName, StringComparer.OrdinalIgnoreCase))
            {
                providerTasks.Add(FetchSourceSafelyAsync(EPracaSourceName, () => FetchEPracaOffersAsync(query, location)));
            }

            var providerResults = await Task.WhenAll(providerTasks);

            return providerResults
                .SelectMany(result => result)
                .GroupBy(GetOfferKey)
                .Select(group => group.First())
                .OrderByDescending(offer => offer.MatchScore)
                .ToList();
        }

        private async Task<List<JobOffer>> FetchSourceSafelyAsync(string sourceName, Func<Task<List<JobOffer>>> fetchSource)
        {
            try
            {
                return await fetchSource();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd źródła {sourceName}: {ex.Message}");

                if (string.IsNullOrWhiteSpace(LastApiError))
                {
                    LastApiError = $"Nie udało się pobrać ofert ze źródła {sourceName}.";
                }

                return new List<JobOffer>();
            }
        }

        private async Task<List<JobOffer>> FetchAdzunaOffersAsync(string query, string location, bool remoteOnly)
        {
            var effectiveQuery = string.IsNullOrWhiteSpace(query) ? "pracownik" : query;
            var queryVariants = remoteOnly
                ? BuildRemoteQueries(effectiveQuery)
                : new List<string> { effectiveQuery };
            var variantTasks = queryVariants
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(searchQuery => FetchOffersForVariantAsync(searchQuery, location, remoteOnly))
                .ToList();
            var variantResults = await Task.WhenAll(variantTasks);

            return variantResults
                .SelectMany(result => result)
                .GroupBy(GetOfferKey)
                .Select(group => group.First())
                .ToList();
        }

        private async Task<List<JobOffer>> FetchOffersForVariantAsync(string query, string location, bool remoteOnly)
        {
            var offers = new List<JobOffer>();
            var pageNumber = 1;

            while (true)
            {
                var response = await FetchAdzunaPageAsync(query, location, remoteOnly, pageNumber);
                var mappedOffers = response.Results
                    .Select(MapAdzunaOffer)
                    .Where(offer => !remoteOnly || offer.IsRemote)
                    .GroupBy(GetOfferKey)
                    .Select(group => group.First())
                    .ToList();

                foreach (var offer in mappedOffers)
                {
                    offer.MatchScore = CalculateMatchingScore(offer);
                    offers.Add(offer);
                }

                if (response.Results.Count < response.PageSize)
                {
                    break;
                }

                pageNumber += 1;
            }

            return offers;
        }

        private async Task<List<JobOffer>> FetchRemotiveOffersAsync(string query, bool remoteOnly)
        {
            try
            {
                var remotiveUrl = string.IsNullOrWhiteSpace(query)
                    ? "https://remotive.com/api/remote-jobs"
                    : $"https://remotive.com/api/remote-jobs?search={Uri.EscapeDataString(query)}";
                using var response = await _httpClient.GetAsync(remotiveUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return new List<JobOffer>();
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var json = Encoding.UTF8.GetString(bytes);
                var data = JsonConvert.DeserializeObject<RemotiveRoot>(json);
                if (data?.Jobs == null || !data.Jobs.Any())
                {
                    return new List<JobOffer>();
                }

                return data.Jobs
                    .Select(MapRemotiveOffer)
                    .Where(offer => !remoteOnly || offer.IsRemote)
                    .GroupBy(GetOfferKey)
                    .Select(group => group.First())
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd Remotive: {ex.Message}");
                return new List<JobOffer>();
            }
        }

        private async Task<List<JobOffer>> FetchJoobleOffersAsync(string query, string location, bool remoteOnly)
        {
            if (string.IsNullOrWhiteSpace(joobleApiKey))
            {
                return new List<JobOffer>();
            }

            var queryVariants = remoteOnly
                ? BuildRemoteQueries(string.IsNullOrWhiteSpace(query) ? "praca" : query)
                : new List<string> { query };

            var variantTasks = queryVariants
                .Where(searchQuery => !string.IsNullOrWhiteSpace(searchQuery) || !remoteOnly)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(searchQuery => FetchJoobleOffersForVariantAsync(searchQuery, location, remoteOnly))
                .ToList();

            if (!variantTasks.Any())
            {
                variantTasks.Add(FetchJoobleOffersForVariantAsync(string.Empty, location, remoteOnly));
            }

            var variantResults = await Task.WhenAll(variantTasks);

            return variantResults
                .SelectMany(result => result)
                .GroupBy(GetOfferKey)
                .Select(group => group.First())
                .ToList();
        }

        private async Task<List<JobOffer>> FetchJoobleOffersForVariantAsync(string query, string location, bool remoteOnly)
        {
            const int jooblePageSize = 20;
            const int maxPages = 5;
            var offers = new List<JobOffer>();
            var pageNumber = 1;

            while (pageNumber <= maxPages)
            {
                var response = await FetchJooblePageAsync(query, location, pageNumber, jooblePageSize);
                if (response.Jobs == null || !response.Jobs.Any())
                {
                    break;
                }

                var mappedOffers = response.Jobs
                    .Select(MapJoobleOffer)
                    .Where(offer => !remoteOnly || offer.IsRemote)
                    .GroupBy(GetOfferKey)
                    .Select(group => group.First())
                    .ToList();

                foreach (var offer in mappedOffers)
                {
                    offer.MatchScore = CalculateMatchingScore(offer);
                    offers.Add(offer);
                }

                if (response.Jobs.Count < jooblePageSize || offers.Count >= response.TotalCount)
                {
                    break;
                }

                pageNumber += 1;
            }

            return offers;
        }

        private async Task<List<JobOffer>> FetchArbeitnowOffersAsync(string query, bool remoteOnly)
        {
            try
            {
                const string arbeitnowUrl = "https://www.arbeitnow.com/api/job-board-api";
                using var response = await _httpClient.GetAsync(arbeitnowUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return new List<JobOffer>();
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var json = Encoding.UTF8.GetString(bytes);
                var data = JsonConvert.DeserializeObject<ArbeitnowRoot>(json);
                if (data?.Data == null || !data.Data.Any())
                {
                    return new List<JobOffer>();
                }

                var normalizedQuery = query?.Trim() ?? string.Empty;

                return data.Data
                    .Select(MapArbeitnowOffer)
                    .Where(offer => !remoteOnly || offer.IsRemote)
                    .Where(offer =>
                        string.IsNullOrWhiteSpace(normalizedQuery) ||
                        offer.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        offer.Company.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        offer.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        offer.Tags.Any(tag => tag.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
                    .GroupBy(GetOfferKey)
                    .Select(group => group.First())
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd Arbeitnow: {ex.Message}");
                return new List<JobOffer>();
            }
        }

        private Task<List<JobOffer>> FetchEPracaOffersAsync(string query, string location)
        {
            if (string.IsNullOrWhiteSpace(ePracaPartnerName))
            {
                LastApiError = "Źródło ePraca wymaga nadanego przez MRPiPS identyfikatora Partner. Gdy go dostaniesz, podepnę pobieranie ofert.";
                return Task.FromResult(new List<JobOffer>());
            }

            LastApiError = "Integracja ePraca jest przygotowana, ale wymaga jeszcze podpięcia danych Partner i wywołania WebService.";
            return Task.FromResult(new List<JobOffer>());
        }

        private async Task<(List<AdzunaResult> Results, int PageSize)> FetchAdzunaPageAsync(string query, string location, bool remoteOnly, int pageNumber)
        {
            var pageSize = remoteOnly ? RemoteResultsPerQuery : DisplayedResultsPerPage;
            var adzunaUrl = BuildAdzunaUrl(query, location, remoteOnly, pageNumber, pageSize);
            using var response = await _httpClient.GetAsync(adzunaUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                await SetApiErrorFromResponseAsync(response);
                return (new List<AdzunaResult>(), pageSize);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var json = Encoding.UTF8.GetString(bytes);
            var data = JsonConvert.DeserializeObject<AdzunaRoot>(json);
            return (data?.Results ?? new List<AdzunaResult>(), pageSize);
        }

        private async Task<JoobleRoot> FetchJooblePageAsync(string query, string location, int pageNumber, int resultsPerPage)
        {
            var endpoint = $"https://pl.jooble.org/api/{joobleApiKey}";
            var payload = new Dictionary<string, object?>
            {
                ["keywords"] = query,
                ["location"] = location,
                ["salary"] = ConvertHourlyRateToMonthlySalary(MinSalary),
                ["page"] = pageNumber.ToString(),
                ["ResultOnPage"] = resultsPerPage.ToString(),
                ["companysearch"] = "false"
            };

            if (Distance > 0)
            {
                payload["radius"] = MapDistanceToJoobleRadius(Distance);
            }

            using var response = await _httpClient.PostAsync(
                endpoint,
                new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    LastApiError = "Jooble odrzuciło zapytanie. Sprawdź poprawność klucza API.";
                }
                else
                {
                    LastApiError = $"Błąd Jooble ({(int)response.StatusCode}).";
                }

                return new JoobleRoot();
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<JoobleRoot>(json) ?? new JoobleRoot();
        }

        private async Task SetApiErrorFromResponseAsync(HttpResponseMessage response)
        {
            try
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var json = Encoding.UTF8.GetString(bytes);
                var apiError = JsonConvert.DeserializeObject<AdzunaErrorResponse>(json);

                if (string.Equals(apiError?.ExceptionCode, "AUTH_FAIL", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(apiError.DisplayMessage))
                {
                    LastApiError = $"Adzuna odrzuciło zapytanie: {apiError.DisplayMessage}";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(apiError?.DisplayMessage))
                {
                    LastApiError = $"Błąd Adzuna: {apiError.DisplayMessage}";
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd odczytu błędu API: {ex.Message}");
            }

            LastApiError = $"Błąd połączenia z Adzuna ({(int)response.StatusCode}).";
        }

        private string BuildAdzunaUrl(string query, string location, bool remoteOnly, int page, int resultsPerPage)
        {
            var adzunaUrl = $"https://api.adzuna.com/v1/api/jobs/pl/search/{page}?app_id={appId}&app_key={appKey}&results_per_page={resultsPerPage}&what={Uri.EscapeDataString(query)}";

            if (!remoteOnly &&
                !string.IsNullOrWhiteSpace(location))
            {
                adzunaUrl += $"&where={Uri.EscapeDataString(location)}";
                if (Distance > 0)
                {
                    adzunaUrl += $"&distance={Distance}";
                }
            }

            return adzunaUrl;
        }

        private static string GetOfferKey(JobOffer offer)
        {
            return offer.Url ?? $"{offer.Title}|{offer.Company}|{offer.Location}";
        }

        private string BuildStatusMessage(int offersCount)
        {
            return offersCount > 0
                ? $"Znaleziono łącznie {offersCount} ofert spełniających kryteria."
                : "Nie znaleziono ofert spełniających wybrane kryteria.";
        }

        private static List<string> BuildRemoteQueries(string normalizedQuery)
        {
            var queries = BuildSearchQueries(normalizedQuery);
            queries.AddRange(new[]
            {
                $"{normalizedQuery} zdalna",
                $"{normalizedQuery} remote"
            });

            return queries
                .Where(query => !string.IsNullOrWhiteSpace(query))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> BuildSearchQueries(string? query, bool includeStemVariants = true)
        {
            var normalizedQuery = query?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return new List<string> { string.Empty };
            }

            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                normalizedQuery
            };

            var tokens = Tokenize(normalizedQuery);
            foreach (var token in tokens)
            {
                if (includeStemVariants)
                {
                    var stem = NormalizeTokenStem(token);
                    if (stem.Length >= 4)
                    {
                        variants.Add(stem);
                    }
                }

                if (SearchSynonyms.TryGetValue(token, out var synonyms))
                {
                    foreach (var synonym in synonyms)
                    {
                        variants.Add(synonym);
                    }
                }
            }

            return variants.ToList();
        }

        private static string NormalizeTokenStem(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var normalized = token.Trim().ToLowerInvariant();
            var endings = new[]
            {
                "owie", "anie", "enie", "yka", "ika", "arz", "arka", "owiec", "owiek",
                "owiec", "owie", "ach", "ami", "ego", "owej", "nych", "nym", "ami",
                "owa", "owe", "owy", "owi", "ami", "cie", "cia", "ciu", "ką", "ka",
                "ek", "em", "ie", "ia", "yk", "ki", "ów", "om", "a", "u", "y", "i"
            };

            foreach (var ending in endings)
            {
                if (normalized.Length > ending.Length + 3 && normalized.EndsWith(ending, StringComparison.Ordinal))
                {
                    normalized = normalized[..^ending.Length];
                    break;
                }
            }

            return normalized;
        }

        private JobOffer MapAdzunaOffer(AdzunaResult result)
        {
            var description = result.Description ?? string.Empty;
            var location = result.Location?.DisplayName ?? "Nie podano lokalizacji";
            var detectedLanguages = DetectLanguages(result.Title, description);
            var salary = FormatSalary(result.Salary_Min, result.Salary_Max);
            var contractType = NormalizeContractTypeLabel(result.Contract_Type, description);
            var contractTime = NormalizeContractLabel(result.Contract_Time);
            var publishedAt = FormatPublishedDate(result.Created);
            var tags = BuildTags(result, contractType, contractTime);
            var isRemote = DetectRemoteOffer(result.Title, description, location);

            return new JobOffer
            {
                Title = result.Title ?? "Bez tytułu",
                Company = result.Company?.DisplayName ?? "Nieznana firma",
                Location = location,
                Salary = salary,
                SalaryDetails = salary,
                Description = BuildShortDescription(description),
                Experience = DetectExperienceLevel(result.Title, description),
                Education = DetectEducationLevel(result.Title, description),
                Source = "Adzuna",
                Url = result.Redirect_Url,
                Languages = detectedLanguages,
                PostedAgo = publishedAt,
                PublishedAt = publishedAt,
                ContractType = contractType,
                ContractTime = contractTime,
                Category = result.Category?.Label ?? "Brak kategorii",
                IsRemote = isRemote,
                Tags = tags
            };
        }

        private JobOffer MapRemotiveOffer(RemotiveJob job)
        {
            var rawDescription = job.Description ?? string.Empty;
            var location = string.IsNullOrWhiteSpace(job.CandidateRequiredLocation)
                ? "Remote"
                : job.CandidateRequiredLocation!;
            var contractTime = NormalizeRemotiveJobType(job.JobType);
            var contractType = contractTime;
            var publishedAt = FormatPublishedDate(job.PublicationDate);
            var tags = new List<string>();

            if (!string.IsNullOrWhiteSpace(job.Category))
            {
                tags.Add(job.Category);
            }

            if (!string.IsNullOrWhiteSpace(contractTime) && contractTime != "Nie podano")
            {
                tags.Add(contractTime);
            }

            tags.Add("Zdalna");

            var description = CleanHtml(rawDescription);
            var offer = new JobOffer
            {
                Title = job.Title ?? "Bez tytułu",
                Company = job.CompanyName ?? "Nieznana firma",
                Location = location,
                Salary = string.IsNullOrWhiteSpace(job.Salary) ? "Do uzgodnienia" : job.Salary!,
                SalaryDetails = string.IsNullOrWhiteSpace(job.Salary) ? "Do uzgodnienia" : job.Salary!,
                Description = BuildShortDescription(description),
                Experience = DetectExperienceLevel(job.Title, description),
                Education = DetectEducationLevel(job.Title, description),
                Source = "Remotive",
                Url = job.Url,
                Languages = DetectLanguages(job.Title, description),
                PostedAgo = publishedAt,
                PublishedAt = publishedAt,
                ContractType = contractType,
                ContractTime = contractTime,
                Category = job.Category ?? "Remote",
                IsRemote = true,
                Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList()
            };

            offer.MatchScore = CalculateMatchingScore(offer);
            return offer;
        }

        private JobOffer MapArbeitnowOffer(ArbeitnowJob job)
        {
            var rawDescription = job.Description ?? string.Empty;
            var description = CleanHtml(rawDescription);
            var location = string.IsNullOrWhiteSpace(job.Location)
                ? (job.Remote ? "Remote" : "Nie podano lokalizacji")
                : job.Location!;
            var contractTime = NormalizeArbeitnowJobType(job.JobTypes);
            var contractType = contractTime;
            var publishedAt = FormatUnixPublishedDate(job.CreatedAtUnix);
            var tags = new List<string>();

            if (job.Tags != null && job.Tags.Any())
            {
                tags.AddRange(job.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Take(3));
            }

            if (!string.IsNullOrWhiteSpace(contractTime) && contractTime != "Nie podano")
            {
                tags.Add(contractTime);
            }

            if (job.Remote)
            {
                tags.Add("Zdalna");
            }

            var offer = new JobOffer
            {
                Title = job.Title ?? "Bez tytułu",
                Company = job.CompanyName ?? "Nieznana firma",
                Location = location,
                Salary = "Do uzgodnienia",
                SalaryDetails = "Do uzgodnienia",
                Description = BuildShortDescription(description),
                Experience = DetectExperienceLevel(job.Title, description),
                Education = DetectEducationLevel(job.Title, description),
                Source = "Arbeitnow",
                Url = job.Url,
                Languages = DetectLanguages(job.Title, description),
                PostedAgo = publishedAt,
                PublishedAt = publishedAt,
                ContractType = contractType,
                ContractTime = contractTime,
                Category = job.Tags?.FirstOrDefault(tag => !string.IsNullOrWhiteSpace(tag)) ?? "Brak kategorii",
                IsRemote = job.Remote || DetectRemoteOffer(job.Title, description, location),
                Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList()
            };

            offer.MatchScore = CalculateMatchingScore(offer);
            return offer;
        }

        private JobOffer MapJoobleOffer(JoobleJob job)
        {
            var description = CleanHtml(job.Snippet ?? string.Empty);
            var location = string.IsNullOrWhiteSpace(job.Location)
                ? "Nie podano lokalizacji"
                : job.Location!;
            var contractTime = NormalizeJoobleJobType(job.Type);
            var contractType = contractTime;
            var publishedAt = FormatPublishedDate(job.Updated);
            var tags = new List<string>();

            if (!string.IsNullOrWhiteSpace(contractTime) && contractTime != "Nie podano")
            {
                tags.Add(contractTime);
            }

            if (DetectRemoteOffer(job.Title, description, location))
            {
                tags.Add("Zdalna");
            }

            var detectedSource = string.IsNullOrWhiteSpace(job.Source) ? "Jooble" : job.Source!;

            return new JobOffer
            {
                Title = job.Title ?? "Bez tytułu",
                Company = job.Company ?? "Nieznana firma",
                Location = location,
                Salary = string.IsNullOrWhiteSpace(job.Salary) ? "Do uzgodnienia" : job.Salary!,
                SalaryDetails = string.IsNullOrWhiteSpace(job.Salary) ? "Do uzgodnienia" : job.Salary!,
                Description = BuildShortDescription(description),
                Experience = DetectExperienceLevel(job.Title, description),
                Education = DetectEducationLevel(job.Title, description),
                Source = "Jooble",
                Url = job.Link,
                Languages = DetectLanguages(job.Title, description),
                PostedAgo = publishedAt,
                PublishedAt = publishedAt,
                ContractType = contractType,
                ContractTime = contractTime,
                Category = detectedSource,
                IsRemote = DetectRemoteOffer(job.Title, description, location),
                Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList()
            };
        }

        private static string FormatSalary(double? salaryMin, double? salaryMax)
        {
            if (salaryMin.HasValue && salaryMax.HasValue)
            {
                return $"{(int)salaryMin.Value:N0} - {(int)salaryMax.Value:N0} PLN";
            }

            if (salaryMin.HasValue)
            {
                return $"od {(int)salaryMin.Value:N0} PLN";
            }

            if (salaryMax.HasValue)
            {
                return $"do {(int)salaryMax.Value:N0} PLN";
            }

            return "Do uzgodnienia";
        }

        private static string NormalizeRemotiveJobType(string? jobType)
        {
            if (string.IsNullOrWhiteSpace(jobType))
            {
                return "Nie podano";
            }

            return jobType switch
            {
                "full_time" => "Pełny etat",
                "part_time" => "Część etatu",
                "contract" => "Kontrakt",
                _ => jobType.Replace("_", " ")
            };
        }

        private static string NormalizeArbeitnowJobType(List<string>? jobTypes)
        {
            if (jobTypes == null || !jobTypes.Any())
            {
                return "Nie podano";
            }

            var normalizedTypes = string.Join(" ", jobTypes);

            if (ContainsAny(normalizedTypes, "full time", "full-time", "full_time"))
            {
                return "Pełny etat";
            }

            if (ContainsAny(normalizedTypes, "part time", "part-time", "part_time"))
            {
                return "Część etatu";
            }

            if (ContainsAny(normalizedTypes, "contract"))
            {
                return "Kontrakt";
            }

            if (ContainsAny(normalizedTypes, "intern"))
            {
                return "Część etatu";
            }

            return jobTypes.FirstOrDefault(type => !string.IsNullOrWhiteSpace(type)) ?? "Nie podano";
        }

        private static string NormalizeJoobleJobType(string? jobType)
        {
            if (string.IsNullOrWhiteSpace(jobType))
            {
                return "Nie podano";
            }

            if (ContainsAny(jobType, "pełny etat", "full", "full-time"))
            {
                return "Pełny etat";
            }

            if (ContainsAny(jobType, "część", "part", "part-time"))
            {
                return "Część etatu";
            }

            if (ContainsAny(jobType, "contract", "kontrakt"))
            {
                return "Kontrakt";
            }

            return jobType;
        }

        private static string NormalizeContractLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Nie podano";
            }

            return value switch
            {
                "full_time" => "Pełny etat",
                "part_time" => "Część etatu",
                "permanent" => "Stała",
                "contract" => "Kontrakt",
                _ => value.Replace("_", " ")
            };
        }

        private static string NormalizeContractTypeLabel(string? value, string description)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DetectContractTypeFromDescription(description);
            }

            return value switch
            {
                "permanent" => "Umowa o pracę",
                "contract" => DetectContractTypeFromDescription(description),
                _ => NormalizeContractLabel(value)
            };
        }

        private static string DetectContractTypeFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "Kontrakt";
            }

            if (description.Contains("b2b", StringComparison.OrdinalIgnoreCase))
            {
                return "B2B";
            }

            if (description.Contains("zlecen", StringComparison.OrdinalIgnoreCase))
            {
                return "Umowa zlecenie";
            }

            if (description.Contains("o prac", StringComparison.OrdinalIgnoreCase))
            {
                return "Umowa o pracę";
            }

            return "Kontrakt";
        }

        private static string FormatPublishedDate(string? created)
        {
            if (!DateTimeOffset.TryParse(created, out var date))
            {
                return "Nie podano daty";
            }

            var localDate = date.ToLocalTime();
            var days = (DateTimeOffset.Now.Date - localDate.Date).Days;

            if (days <= 0)
            {
                return "Dzisiaj";
            }

            if (days == 1)
            {
                return "Wczoraj";
            }

            return $"{days} dni temu";
        }

        private static string FormatUnixPublishedDate(long? unixTimestamp)
        {
            if (!unixTimestamp.HasValue || unixTimestamp.Value <= 0)
            {
                return "Nie podano daty";
            }

            return FormatPublishedDate(DateTimeOffset.FromUnixTimeSeconds(unixTimestamp.Value).ToString("O"));
        }

        private static string MapDistanceToJoobleRadius(int distance)
        {
            return distance switch
            {
                <= 0 => "0",
                <= 4 => "4",
                <= 8 => "8",
                <= 16 => "16",
                <= 26 => "26",
                <= 40 => "40",
                _ => "80"
            };
        }

        private static string BuildShortDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "Brak opisu oferty.";
            }

            var normalized = description.Replace("\r", " ").Replace("\n", " ").Trim();
            normalized = Regex.Replace(normalized, @"Ogłoszenie\s*nr\s*:?\s*\d+", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"Data\s+ukazania\s+się\s+ogłoszenia\s*:?\s*\d{4}-\d{2}-\d{2}", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"Data\s+ukazania\s+sie\s+ogłoszenia\s*:?\s*\d{4}-\d{2}-\d{2}", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\s{2,}", " ").Trim(' ', '-', '|', ';', ',');

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "Brak opisu oferty.";
            }

            if (normalized.Length <= 220)
            {
                return normalized;
            }

            return normalized[..220].Trim() + "...";
        }

        private static string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var withoutTags = Regex.Replace(html, "<.*?>", " ");
            return System.Net.WebUtility.HtmlDecode(withoutTags);
        }

        private static List<string> BuildTags(AdzunaResult result, string contractType, string contractTime)
        {
            var tags = new List<string>();

            if (!string.IsNullOrWhiteSpace(result.Category?.Label))
            {
                tags.Add(result.Category.Label);
            }

            if (!string.IsNullOrWhiteSpace(contractTime) && contractTime != "Nie podano")
            {
                tags.Add(contractTime);
            }

            if (!string.IsNullOrWhiteSpace(contractType) && contractType != "Nie podano")
            {
                tags.Add(contractType);
            }

            return tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        }

        private static string DetectExperienceLevel(string? title, string description)
        {
            var text = $"{title} {description}";

            if (ContainsAny(text, "manager", "menadż", "kierownik", "team lead", "head of", "dyrektor"))
            {
                return "Menadżer";
            }

            if (MatchesAnyPattern(text, @"\bsenior\b", @"\bsr\.?\b", @"starszy specjalista", @"\blead\b"))
            {
                return "Starszy specjalista";
            }

            if (ContainsAny(text, "staż", "praktyk", "trainee", "intern", "bez doświadczenia", "apprentice"))
            {
                return "Brak doświadczenia";
            }

            if (MatchesAnyPattern(text, @"\bjunior\b", @"\bjr\.?\b", @"młodszy", @"asystent"))
            {
                return "Młodszy specjalista";
            }

            if (MatchesAnyPattern(text, @"\bmid\b", @"\bmiddle\b") || ContainsAny(text, "doświadczen", "specialist", "specjalista"))
            {
                return "Specjalista";
            }

            return "Wszystkie";
        }

        private static string DetectEducationLevel(string? title, string description)
        {
            var text = $"{title} {description}";

            if (string.IsNullOrWhiteSpace(text))
            {
                return "Brak wymagań lub niewymagane";
            }

            if (MatchesAnyPattern(
                text,
                @"wymagan\w*[^.]{0,40}wykszta\w*[^.]{0,20}wyższ",
                @"wykszta\w* wyższ\w*",
                @"uczeln\w* wyższ\w*",
                @"studia wyższ\w*",
                @"higher education",
                @"bachelor('?s)? degree",
                @"master('?s)? degree",
                @"degree in"))
            {
                return "Wyższe";
            }

            if (MatchesAnyPattern(
                text,
                @"wymagan\w*[^.]{0,40}wykszta\w*[^.]{0,20}śred",
                @"wykszta\w* średni\w*",
                @"średnie techniczne",
                @"technikum",
                @"liceum",
                @"secondary education"))
            {
                return "Średnie";
            }

            if (MatchesAnyPattern(
                text,
                @"student\w*",
                @"ucze\w*",
                @"w trakcie studi",
                @"studying",
                @"undergraduate",
                @"mile widzian\w* student"))
            {
                return "Student / w trakcie";
            }

            return "Brak wymagań lub niewymagane";
        }

        private static bool DetectRemoteOffer(string? title, string description, string? location)
        {
            var text = $"{title} {description} {location}";

            return ContainsAny(
                text,
                "zdal",
                "remote",
                "home office",
                "work from home",
                "telepraca",
                "praca z domu",
                "100% zdal",
                "fully remote"
            );
        }

        private static bool ContainsAny(string text, params string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return patterns.Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private static string DetectOfferTextLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 80)
            {
                return string.Empty;
            }

            var normalized = text.ToLowerInvariant();
            var polishScore = CountLanguageIndicators(normalized, PolishTextIndicators);
            var englishScore = CountLanguageIndicators(normalized, EnglishTextIndicators);
            var germanScore = CountLanguageIndicators(normalized, GermanTextIndicators);

            if (Regex.IsMatch(normalized, "[äöüß]"))
            {
                germanScore += 3;
            }

            if (germanScore >= 5 && germanScore >= englishScore && germanScore > polishScore + 1)
            {
                return "Niemiecki";
            }

            if (englishScore >= 6 && englishScore > germanScore + 1 && englishScore > polishScore + 1)
            {
                return "Angielski";
            }

            return string.Empty;
        }

        private static int CountLanguageIndicators(string text, string[] indicators)
        {
            return indicators.Sum(indicator =>
                Regex.Matches(text, indicator, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count);
        }

        private static List<string> DetectLanguages(string? title, string description)
        {
            var text = $"{title} {description}";
            var languages = new List<string>();

            if (HasLanguageRequirement(text, "angiel", "english"))
            {
                languages.Add("Angielski");
            }

            if (HasLanguageRequirement(text, "niemie", "german", "deutsch"))
            {
                languages.Add("Niemiecki");
            }

            if (HasLanguageRequirement(text, "ukrai", "ukrainian"))
            {
                languages.Add("Ukraiński");
            }

            if (HasLanguageRequirement(text, "francus", "french", "français", "francais"))
            {
                languages.Add("Francuski");
            }

            return languages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool HasLanguageRequirement(string text, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (var alias in aliases)
            {
                var safeAlias = Regex.Escape(alias);
                if (MatchesAnyPattern(
                    text,
                    $@"znajomo\w*.{{0,20}}{safeAlias}",
                    $@"{safeAlias}.{{0,20}}(b1|b2|c1|c2)",
                    $@"wymagan\w*.{{0,30}}{safeAlias}",
                    $@"mile widzian\w*.{{0,30}}{safeAlias}",
                    $@"{safeAlias}.{{0,20}}(required|mandatory)",
                    $@"language.{{0,20}}{safeAlias}"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAnyPattern(string text, params string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return patterns.Any(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        }

        private static string GetEffectiveSalaryText(JobOffer offer)
        {
            if (!string.IsNullOrWhiteSpace(offer.SalaryDetails))
            {
                return offer.SalaryDetails;
            }

            return offer.Salary;
        }

        private static bool IsLowerEducationMatch(string offerEducation, string selectedEducation)
        {
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Brak wymagań lub niewymagane"] = 0,
                ["Student / w trakcie"] = 1,
                ["Średnie"] = 2,
                ["Wyższe"] = 3
            };

            if (!order.TryGetValue(selectedEducation, out var selectedLevel) ||
                !order.TryGetValue(offerEducation, out var offerLevel))
            {
                return false;
            }

            return offerLevel < selectedLevel;
        }

        private static bool IsLowerExperienceMatch(string offerExperience, string selectedExperience)
        {
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Wszystkie"] = 0,
                ["Brak doświadczenia"] = 1,
                ["Młodszy specjalista"] = 2,
                ["Specjalista"] = 3,
                ["Starszy specjalista"] = 4,
                ["Menadżer"] = 5
            };

            if (!order.TryGetValue(selectedExperience, out var selectedLevel) ||
                !order.TryGetValue(offerExperience, out var offerLevel))
            {
                return false;
            }

            return offerLevel < selectedLevel;
        }

    }
}
