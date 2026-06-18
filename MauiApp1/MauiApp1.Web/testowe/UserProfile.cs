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
        public bool DarkMode { get; set; } = false;
        public string DefaultLocation { get; set; } = "Rzeszów";
        public bool NotificationsEnabled { get; set; } = true;
        public string ExpectedSalary { get; set; } = "";
        public List<string> Skills { get; set; } = new(); 
        public string JobTitle { get; set; } = "";
        public string Experience { get; set; } = "Wszystkie";
        public string Education { get; set; } = "Brak wymagań lub niewymagane";
        public List<string> PreferredContractTypes { get; set; } = new();
        public List<string> KnownLanguages { get; set; } = new();
    }
}
