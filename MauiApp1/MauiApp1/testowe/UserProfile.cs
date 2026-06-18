namespace MauiApp1.testowe
{
    /// <summary>
    /// Przechowuje dane profilu użytkownika razem z historią przeglądanych ofert.
    /// </summary>
    /// <remarks>
    /// Profil jest zapisywany przez implementację <see cref="IUserStore"/> i ładowany do <see cref="JobSearchService.CurrentUser"/>
    /// po zalogowaniu. Celowo łączy dane prezentacyjne z preferencjami wyszukiwania, ponieważ aplikacja używa ich w filtrach,
    /// rankingu oraz ekranie profilu.
    /// </remarks>
    /// <seealso cref="UserSettings"/>
    public class UserProfile
    {
        /// <summary>
        /// Login konta, z którym powiązany jest profil.
        /// </summary>
        /// <value>Pusty tekst oznacza profil tymczasowy użytkownika niezalogowanego.</value>
        public string AccountLogin { get; set; } = "";
        public string Username { get; set; } = "Gość";
        public string Email { get; set; } = "";

        // Historia przeglądania (przechowujemy listę ofert)
        public List<JobOffer> SearchHistory { get; set; } = new();

        // Zapisane ustawienia filtrów
        public UserSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// Zestaw preferencji używanych do domyślnego filtrowania i oceniania ofert pracy.
    /// </summary>
    /// <remarks>
    /// Te ustawienia są traktowane jako profil długoterminowy. Bieżące filtry w <see cref="JobSearchService"/> mogą chwilowo
    /// różnić się od tych wartości, a metoda <see cref="JobSearchService.ApplyProfileFilters"/> synchronizuje je na żądanie użytkownika.
    /// </remarks>
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
