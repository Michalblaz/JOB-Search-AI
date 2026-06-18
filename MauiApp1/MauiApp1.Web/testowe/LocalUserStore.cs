using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MauiApp1.testowe
{
    public class LocalUserStore : IUserStore
    {
        private readonly string _databasePath;

        public LocalUserStore(IAppDataPathProvider appDataPathProvider)
        {
            _databasePath = Path.Combine(appDataPathProvider.AppDataDirectory, "users-db.json");
        }

        public UserAccountRecord? FindByLogin(string login)
        {
            var database = LoadDatabase();
            return database.Accounts.FirstOrDefault(account =>
                account.Login.Equals(login, StringComparison.OrdinalIgnoreCase));
        }

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

    public class UserDatabase
    {
        public List<UserAccountRecord> Accounts { get; set; } = new();
    }

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
