using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MauiApp1.testowe
{
    /// <summary>
    /// Lokalna implementacja <see cref="IUserStore"/> zapisująca konta w pliku JSON w katalogu danych aplikacji.
    /// </summary>
    /// <remarks>
    /// Ten magazyn jest użyteczny w trybie desktopowym i testowym, gdy aplikacja nie ma skonfigurowanego połączenia z PostgreSQL.
    /// W razie uszkodzenia pliku metoda odczytu zwraca pustą bazę, aby użytkownik nadal mógł uruchomić aplikację.
    /// </remarks>
    /// <seealso cref="PostgresUserStore"/>
    public class LocalUserStore : IUserStore
    {
        private readonly string _databasePath;

        /// <summary>
        /// Tworzy magazyn użytkowników oparty o plik <c>users-db.json</c>.
        /// </summary>
        /// <param name="appDataPathProvider">Dostawca katalogu danych aplikacji właściwy dla platformy.</param>
        public LocalUserStore(IAppDataPathProvider appDataPathProvider)
        {
            _databasePath = Path.Combine(appDataPathProvider.AppDataDirectory, "users-db.json");
        }

        /// <inheritdoc/>
        public UserAccountRecord? FindByLogin(string login)
        {
            var database = LoadDatabase();
            return database.Accounts.FirstOrDefault(account =>
                account.Login.Equals(login, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public void SaveAccount(UserAccountRecord account)
        {
            var database = LoadDatabase();
            var existing = database.Accounts.FindIndex(item =>
                item.Login.Equals(account.Login, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                database.Accounts[existing] = account;
            }
            else
            {
                database.Accounts.Add(account);
            }

            SaveDatabase(database);
        }

        /// <inheritdoc/>
        public bool AnyAccounts()
        {
            return LoadDatabase().Accounts.Any();
        }

        private UserDatabase LoadDatabase()
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    return new UserDatabase();
                }

                var json = File.ReadAllText(_databasePath);
                return JsonConvert.DeserializeObject<UserDatabase>(json) ?? new UserDatabase();
            }
            catch
            {
                return new UserDatabase();
            }
        }

        private void SaveDatabase(UserDatabase database)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            var json = JsonConvert.SerializeObject(database, Formatting.Indented);
            File.WriteAllText(_databasePath, json);
        }
    }

    /// <summary>
    /// Struktura pliku JSON używanego przez <see cref="LocalUserStore"/>.
    /// </summary>
    public class UserDatabase
    {
        public List<UserAccountRecord> Accounts { get; set; } = new();
    }

    /// <summary>
    /// Pełny zapis konta użytkownika wraz z hasłem, profilem i migawkami ofert.
    /// </summary>
    /// <remarks>
    /// Rekord jest przenoszony między implementacjami <see cref="IUserStore"/> i stanowi granicę serializacji danych konta.
    /// Hasło przechowywane jest jako skrót przygotowany przez <see cref="JobSearchService"/>.
    /// </remarks>
    public class UserAccountRecord
    {
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserProfile Profile { get; set; } = new();
        public List<JobOffer> FavoriteOffers { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
