namespace MauiApp1.testowe
{
    public class UserProfile
    {
        public string AccountLogin { get; set; } = "";
        public string Username { get; set; } = "Gość";
        public string Email { get; set; } = "";

        // Historia przeglądania (przechowujemy listę ofert)
        public List<JobOffer> SearchHistory { get; set; } = new();

        // Zapisane ustawienia filtrów
        public UserSettings Settings { get; set; } = new();
    }

    public class UserSettings
    {
        private string _theme = "light";

        public bool DarkMode
        {
            get => Theme == "dark";
            set => Theme = value ? "dark" : "light";
        }

        public string Theme
        {
            get => NormalizeTheme(_theme);
            set => _theme = NormalizeTheme(value);
        }

        public string ThemeCssClass => Theme switch
        {
            "dark" => "dark-theme",
            "cyberpunk" => "cyberpunk-theme",
            _ => "light-theme"
        };

        public string DefaultLocation { get; set; } = "Rzeszów";
        public bool NotificationsEnabled { get; set; } = true;
        public string ExpectedSalary { get; set; } = "";
        public decimal? ExpectedSalaryMin { get; set; }
        public string SalaryCurrency { get; set; } = "PLN";
        public string SalaryTaxType { get; set; } = "unknown";
        public string WorkMode { get; set; } = "any";
        public decimal? MaxOfficeDaysPerWeek { get; set; }
        public string WorkTimeType { get; set; } = "any";
        public int? MaxDistanceKm { get; set; }
        public string PreferredCategoryCode { get; set; } = "";
        public string PreferredRoleCode { get; set; } = "";
        public List<string> Skills { get; set; } = new(); 
        public List<string> PersonalTraits { get; set; } = new();
        public List<string> WorkActivities { get; set; } = new();
        public string JobTitle { get; set; } = "";
        public string Experience { get; set; } = "Wszystkie";
        public string Education { get; set; } = "Brak wymagań lub niewymagane";
        public List<string> PreferredContractTypes { get; set; } = new();
        public List<string> KnownLanguages { get; set; } = new();
        public Dictionary<string, string> KnownLanguageLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Certifications { get; set; } = new();
        public List<string> DrivingLicenses { get; set; } = new();
        public List<string> PreferredBenefits { get; set; } = new();
        public List<string> ExcludedFlags { get; set; } = new();
        public int RequiredCriteriaMatchPercent { get; set; } = 50;

        public IEnumerable<string> GetProfileCriterionCodes()
        {
            return Skills
                .Concat(PersonalTraits)
                .Concat(WorkActivities)
                .Concat(Certifications)
                .Concat(DrivingLicenses)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static string NormalizeTheme(string? theme)
        {
            return theme?.Trim().ToLowerInvariant() switch
            {
                "dark" => "dark",
                "cyberpunk" => "cyberpunk",
                _ => "light"
            };
        }
    }
}
