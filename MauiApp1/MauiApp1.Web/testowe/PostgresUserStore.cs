using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MauiApp1.testowe
{
    public class PostgresUserStore : IUserStore
    {
        private readonly string _connectionString;
        private bool _schemaEnsured;

        public PostgresUserStore(AppSettingsProvider settingsProvider)
        {
            _connectionString = settingsProvider.GetSettings().Database.ConnectionString;
        }

        public UserAccountRecord? FindByLogin(string login)
        {
            EnsureSchema();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            const string userSql = """
                select
                    u.login,
                    u.password_hash,
                    u.created_at,
                    u.updated_at,
                    coalesce(p.username, 'Gość') as username,
                    p.email,
                    coalesce(p.default_location, 'Rzeszów') as default_location,
                    coalesce(p.notifications_enabled, true) as notifications_enabled,
                    coalesce(p.dark_mode, false) as dark_mode,
                    coalesce(p.expected_salary, '') as expected_salary,
                    coalesce(p.job_title, '') as job_title,
                    coalesce(p.experience, 'Wszystkie') as experience,
                    coalesce(p.education, 'Brak wymagań lub niewymagane') as education
                from public.app_users u
                left join public.user_profiles p on p.user_id = u.id
                where lower(u.login) = lower(@login)
                limit 1;
                """;

            using var command = new NpgsqlCommand(userSql, connection);
            command.Parameters.AddWithValue("login", login);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var account = new UserAccountRecord
            {
                Login = reader.GetString(0),
                PasswordHash = reader.GetString(1),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(2),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(3),
                Profile = new UserProfile
                {
                    AccountLogin = reader.GetString(0),
                    Username = reader.GetString(4),
                    Email = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Settings = new UserSettings
                    {
                        DefaultLocation = reader.GetString(6),
                        NotificationsEnabled = reader.GetBoolean(7),
                        DarkMode = reader.GetBoolean(8),
                        ExpectedSalary = reader.GetString(9),
                        JobTitle = reader.GetString(10),
                        Experience = reader.GetString(11),
                        Education = reader.GetString(12)
                    }
                },
                FavoriteOffers = new List<JobOffer>()
            };

            reader.Close();

            account.Profile.Settings.Skills = LoadStringList(connection, account.Login, "public.user_profile_skills", "skill_name");
            account.Profile.Settings.KnownLanguages = LoadStringList(connection, account.Login, "public.user_profile_languages", "language_name");
            account.Profile.Settings.PreferredContractTypes = LoadStringList(connection, account.Login, "public.user_profile_contract_types", "contract_type");
            account.FavoriteOffers = LoadFavoriteOffers(connection, account.Login);
            account.Profile.SearchHistory = LoadSearchHistory(connection, account.Login);

            return account;
        }

        public void SaveAccount(UserAccountRecord account)
        {
            EnsureSchema();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            const string upsertUserSql = """
                insert into public.app_users (login, password_hash, created_at, updated_at)
                values (@login, @password_hash, @created_at, @updated_at)
                on conflict (login)
                do update set
                    password_hash = excluded.password_hash,
                    updated_at = excluded.updated_at
                returning id;
                """;

            long userId;
            using (var userCommand = new NpgsqlCommand(upsertUserSql, connection, transaction))
            {
                userCommand.Parameters.AddWithValue("login", account.Login);
                userCommand.Parameters.AddWithValue("password_hash", account.PasswordHash);
                userCommand.Parameters.AddWithValue("created_at", account.CreatedAt == default ? DateTimeOffset.UtcNow : account.CreatedAt);
                userCommand.Parameters.AddWithValue("updated_at", account.UpdatedAt == default ? DateTimeOffset.UtcNow : account.UpdatedAt);
                userId = Convert.ToInt64(userCommand.ExecuteScalar());
            }

            const string upsertProfileSql = """
                insert into public.user_profiles (
                    user_id, username, email, default_location, notifications_enabled, dark_mode,
                    expected_salary, job_title, experience, education, created_at, updated_at
                )
                values (
                    @user_id, @username, @email, @default_location, @notifications_enabled, @dark_mode,
                    @expected_salary, @job_title, @experience, @education, now(), now()
                )
                on conflict (user_id)
                do update set
                    username = excluded.username,
                    email = excluded.email,
                    default_location = excluded.default_location,
                    notifications_enabled = excluded.notifications_enabled,
                    dark_mode = excluded.dark_mode,
                    expected_salary = excluded.expected_salary,
                    job_title = excluded.job_title,
                    experience = excluded.experience,
                    education = excluded.education,
                    updated_at = now();
                """;

            using (var profileCommand = new NpgsqlCommand(upsertProfileSql, connection, transaction))
            {
                profileCommand.Parameters.AddWithValue("user_id", userId);
                profileCommand.Parameters.AddWithValue("username", account.Profile?.Username ?? account.Login);
                profileCommand.Parameters.AddWithValue("email", (object?)account.Profile?.Email ?? DBNull.Value);
                profileCommand.Parameters.AddWithValue("default_location", account.Profile?.Settings?.DefaultLocation ?? "Rzeszów");
                profileCommand.Parameters.AddWithValue("notifications_enabled", account.Profile?.Settings?.NotificationsEnabled ?? true);
                profileCommand.Parameters.AddWithValue("dark_mode", account.Profile?.Settings?.DarkMode ?? false);
                profileCommand.Parameters.AddWithValue("expected_salary", account.Profile?.Settings?.ExpectedSalary ?? string.Empty);
                profileCommand.Parameters.AddWithValue("job_title", account.Profile?.Settings?.JobTitle ?? string.Empty);
                profileCommand.Parameters.AddWithValue("experience", account.Profile?.Settings?.Experience ?? "Wszystkie");
                profileCommand.Parameters.AddWithValue("education", account.Profile?.Settings?.Education ?? "Brak wymagań lub niewymagane");
                profileCommand.ExecuteNonQuery();
            }

            ReplaceStringList(connection, transaction, userId, "public.user_profile_skills", "skill_name", account.Profile?.Settings?.Skills);
            ReplaceStringList(connection, transaction, userId, "public.user_profile_languages", "language_name", account.Profile?.Settings?.KnownLanguages);
            ReplaceStringList(connection, transaction, userId, "public.user_profile_contract_types", "contract_type", account.Profile?.Settings?.PreferredContractTypes);
            ReplaceFavoriteOffers(connection, transaction, userId, account.FavoriteOffers);
            ReplaceSearchHistory(connection, transaction, userId, account.Profile?.SearchHistory);

            transaction.Commit();
        }

        public bool AnyAccounts()
        {
            EnsureSchema();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand("select exists(select 1 from public.app_users)", connection);
            return Convert.ToBoolean(command.ExecuteScalar());
        }

        private void EnsureSchema()
        {
            if (_schemaEnsured)
            {
                return;
            }

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            const string sql = """
                create table if not exists public.app_users (
                    id bigint generated always as identity primary key,
                    login varchar(100) not null unique,
                    password_hash varchar(512) not null,
                    created_at timestamptz not null default now(),
                    updated_at timestamptz not null default now(),
                    last_login_at timestamptz null,
                    is_active boolean not null default true
                );

                create table if not exists public.user_profiles (
                    user_id bigint primary key references public.app_users(id) on delete cascade,
                    username varchar(150) not null default 'Gość',
                    email varchar(320) null,
                    default_location varchar(150) not null default 'Rzeszów',
                    notifications_enabled boolean not null default true,
                    dark_mode boolean not null default false,
                    expected_salary varchar(100) null,
                    job_title varchar(200) null,
                    experience varchar(100) not null default 'Wszystkie',
                    education varchar(100) not null default 'Brak wymagań lub niewymagane',
                    created_at timestamptz not null default now(),
                    updated_at timestamptz not null default now()
                );

                create table if not exists public.user_profile_skills (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    skill_name varchar(150) not null,
                    primary key (user_id, skill_name)
                );

                create table if not exists public.user_profile_languages (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    language_name varchar(100) not null,
                    primary key (user_id, language_name)
                );

                create table if not exists public.user_profile_contract_types (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    contract_type varchar(100) not null,
                    primary key (user_id, contract_type)
                );

                create table if not exists public.user_favorite_offer_snapshots (
                    id bigint generated always as identity primary key,
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    job_offer_id bigint null,
                    source_code varchar(50) null,
                    external_id varchar(200) null,
                    title varchar(300) not null,
                    company_name varchar(300) not null default '',
                    company_logo_url varchar(1000) null,
                    location_name varchar(300) null,
                    salary_raw varchar(200) null,
                    source_name varchar(100) null,
                    external_url varchar(1000) null,
                    created_at timestamptz not null default now()
                );

                create table if not exists public.user_search_history_snapshots (
                    id bigint generated always as identity primary key,
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    job_offer_id bigint null,
                    source_code varchar(50) null,
                    external_id varchar(200) null,
                    title varchar(300) not null,
                    company_name varchar(300) not null default '',
                    company_logo_url varchar(1000) null,
                    location_name varchar(300) null,
                    salary_raw varchar(200) null,
                    source_name varchar(100) null,
                    external_url varchar(1000) null,
                    viewed_at timestamptz not null default now()
                );

                alter table public.user_favorite_offer_snapshots
                    add column if not exists id bigint generated always as identity,
                    add column if not exists job_offer_id bigint null,
                    add column if not exists source_code varchar(50) null,
                    add column if not exists external_id varchar(200) null,
                    add column if not exists company_logo_url varchar(1000) null;

                alter table public.user_search_history_snapshots
                    add column if not exists job_offer_id bigint null,
                    add column if not exists source_code varchar(50) null,
                    add column if not exists external_id varchar(200) null,
                    add column if not exists company_logo_url varchar(1000) null;
                """;

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
            _schemaEnsured = true;
        }

        private static List<string> LoadStringList(NpgsqlConnection connection, string login, string tableName, string valueColumn)
        {
            var values = new List<string>();
            var sql = $"""
                select t.{valueColumn}
                from {tableName} t
                join public.app_users u on u.id = t.user_id
                where lower(u.login) = lower(@login)
                order by t.{valueColumn};
                """;

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("login", login);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private static List<JobOffer> LoadFavoriteOffers(NpgsqlConnection connection, string login)
        {
            const string sql = """
                select job_offer_id, source_code, external_id, title, company_name, company_logo_url, location_name, salary_raw, source_name, external_url
                from public.user_favorite_offer_snapshots fs
                join public.app_users u on u.id = fs.user_id
                where lower(u.login) = lower(@login)
                order by fs.created_at desc;
                """;

            var offers = new List<JobOffer>();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("login", login);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                offers.Add(new JobOffer
                {
                    JobOfferId = reader.IsDBNull(0) ? null : reader.GetInt64(0),
                    SourceCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ExternalId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Title = reader.GetString(3),
                    Company = reader.IsDBNull(4) ? "Nieznana firma" : reader.GetString(4),
                    CompanyLogoUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Location = reader.IsDBNull(6) ? "Nie podano lokalizacji" : reader.GetString(6),
                    Salary = reader.IsDBNull(7) ? "Do uzgodnienia" : reader.GetString(7),
                    SalaryDetails = reader.IsDBNull(7) ? "Do uzgodnienia" : reader.GetString(7),
                    Source = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Url = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return offers;
        }

        private static List<JobOffer> LoadSearchHistory(NpgsqlConnection connection, string login)
        {
            const string sql = """
                select job_offer_id, source_code, external_id, title, company_name, company_logo_url, location_name, salary_raw, source_name, external_url
                from public.user_search_history_snapshots hs
                join public.app_users u on u.id = hs.user_id
                where lower(u.login) = lower(@login)
                order by hs.viewed_at desc
                limit 20;
                """;

            var offers = new List<JobOffer>();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("login", login);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                offers.Add(new JobOffer
                {
                    JobOfferId = reader.IsDBNull(0) ? null : reader.GetInt64(0),
                    SourceCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ExternalId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Title = reader.GetString(3),
                    Company = reader.GetString(4),
                    CompanyLogoUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Location = reader.IsDBNull(6) ? "Nie podano lokalizacji" : reader.GetString(6),
                    Salary = reader.IsDBNull(7) ? "Do uzgodnienia" : reader.GetString(7),
                    SalaryDetails = reader.IsDBNull(7) ? "Do uzgodnienia" : reader.GetString(7),
                    Source = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Url = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return offers;
        }

        private static void ReplaceStringList(NpgsqlConnection connection, NpgsqlTransaction transaction, long userId, string tableName, string valueColumn, IEnumerable<string>? values)
        {
            using (var deleteCommand = new NpgsqlCommand($"delete from {tableName} where user_id = @user_id", connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("user_id", userId);
                deleteCommand.ExecuteNonQuery();
            }

            foreach (var value in (values ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                using var insertCommand = new NpgsqlCommand(
                    $"insert into {tableName} (user_id, {valueColumn}) values (@user_id, @value) on conflict do nothing",
                    connection,
                    transaction);
                insertCommand.Parameters.AddWithValue("user_id", userId);
                insertCommand.Parameters.AddWithValue("value", value);
                insertCommand.ExecuteNonQuery();
            }
        }

        private static void ReplaceFavoriteOffers(NpgsqlConnection connection, NpgsqlTransaction transaction, long userId, IEnumerable<JobOffer>? offers)
        {
            using (var deleteCommand = new NpgsqlCommand("delete from public.user_favorite_offer_snapshots where user_id = @user_id", connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("user_id", userId);
                deleteCommand.ExecuteNonQuery();
            }

            foreach (var offer in (offers ?? Enumerable.Empty<JobOffer>())
                         .Where(offer => !string.IsNullOrWhiteSpace(offer.Title))
                         .GroupBy(BuildFavoriteSnapshotKey, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First()))
            {
                using var insertCommand = new NpgsqlCommand(
                    """
                    insert into public.user_favorite_offer_snapshots (
                        user_id, job_offer_id, source_code, external_id, title, company_name, company_logo_url, location_name, salary_raw, source_name, external_url
                    )
                    values (@user_id, @job_offer_id, @source_code, @external_id, @title, @company_name, @company_logo_url, @location_name, @salary_raw, @source_name, @external_url)
                    on conflict do nothing
                    """,
                    connection,
                    transaction);
                insertCommand.Parameters.AddWithValue("user_id", userId);
                insertCommand.Parameters.AddWithValue("job_offer_id", (object?)offer.JobOfferId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("source_code", (object?)offer.SourceCode ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("external_id", (object?)offer.ExternalId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("title", offer.Title);
                insertCommand.Parameters.AddWithValue("company_name", offer.Company ?? string.Empty);
                insertCommand.Parameters.AddWithValue("company_logo_url", (object?)offer.CompanyLogoUrl ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("location_name", (object?)offer.Location ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("salary_raw", (object?)offer.Salary ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("source_name", (object?)offer.Source ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("external_url", (object?)offer.Url ?? DBNull.Value);
                insertCommand.ExecuteNonQuery();
            }
        }

        private static void ReplaceSearchHistory(NpgsqlConnection connection, NpgsqlTransaction transaction, long userId, IEnumerable<JobOffer>? offers)
        {
            using (var deleteCommand = new NpgsqlCommand("delete from public.user_search_history_snapshots where user_id = @user_id", connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("user_id", userId);
                deleteCommand.ExecuteNonQuery();
            }

            foreach (var offer in (offers ?? Enumerable.Empty<JobOffer>())
                         .Where(offer => !string.IsNullOrWhiteSpace(offer.Title))
                         .Take(20))
            {
                using var insertCommand = new NpgsqlCommand(
                    """
                    insert into public.user_search_history_snapshots (
                        user_id, job_offer_id, source_code, external_id, title, company_name, company_logo_url, location_name, salary_raw, source_name, external_url
                    )
                    values (@user_id, @job_offer_id, @source_code, @external_id, @title, @company_name, @company_logo_url, @location_name, @salary_raw, @source_name, @external_url)
                    """,
                    connection,
                    transaction);
                insertCommand.Parameters.AddWithValue("user_id", userId);
                insertCommand.Parameters.AddWithValue("job_offer_id", (object?)offer.JobOfferId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("source_code", (object?)offer.SourceCode ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("external_id", (object?)offer.ExternalId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("title", offer.Title);
                insertCommand.Parameters.AddWithValue("company_name", offer.Company ?? string.Empty);
                insertCommand.Parameters.AddWithValue("company_logo_url", (object?)offer.CompanyLogoUrl ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("location_name", (object?)offer.Location ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("salary_raw", (object?)offer.Salary ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("source_name", (object?)offer.Source ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("external_url", (object?)offer.Url ?? DBNull.Value);
                insertCommand.ExecuteNonQuery();
            }
        }

        private static string BuildFavoriteSnapshotKey(JobOffer offer)
        {
            if (offer.JobOfferId.HasValue)
            {
                return $"id:{offer.JobOfferId.Value}";
            }

            return $"snapshot:{offer.Title}|{offer.Company}|{offer.Url}";
        }
    }
}
