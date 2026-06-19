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
                    coalesce(p.theme, case when coalesce(p.dark_mode, false) then 'dark' else 'light' end) as theme,
                    coalesce(p.expected_salary, '') as expected_salary,
                    p.expected_salary_min,
                    coalesce(p.salary_currency, 'PLN') as salary_currency,
                    coalesce(p.salary_tax_type, 'unknown') as salary_tax_type,
                    coalesce(p.work_mode_preference, 'any') as work_mode_preference,
                    p.max_office_days_per_week,
                    coalesce(p.work_time_type_preference, 'any') as work_time_type_preference,
                    p.max_distance_km,
                    coalesce(p.preferred_category_code, '') as preferred_category_code,
                    coalesce(p.preferred_role_code, '') as preferred_role_code,
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
                        Theme = reader.GetString(9),
                        ExpectedSalary = reader.GetString(10),
                        ExpectedSalaryMin = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                        SalaryCurrency = reader.GetString(12),
                        SalaryTaxType = reader.GetString(13),
                        WorkMode = reader.GetString(14),
                        MaxOfficeDaysPerWeek = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                        WorkTimeType = reader.GetString(16),
                        MaxDistanceKm = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                        PreferredCategoryCode = reader.GetString(18),
                        PreferredRoleCode = reader.GetString(19),
                        JobTitle = reader.GetString(20),
                        Experience = reader.GetString(21),
                        Education = reader.GetString(22)
                    }
                },
                FavoriteOffers = new List<JobOffer>()
            };

            reader.Close();

            account.Profile.Settings.Skills = LoadStringList(connection, account.Login, "public.user_profile_skills", "skill_name");
            account.Profile.Settings.KnownLanguages = LoadStringList(connection, account.Login, "public.user_profile_languages", "language_name");
            account.Profile.Settings.PreferredContractTypes = LoadStringList(connection, account.Login, "public.user_profile_contract_types", "contract_type");
            account.Profile.Settings.Skills = MergeDistinct(account.Profile.Settings.Skills, LoadStringList(connection, account.Login, "public.user_skills", "skill_code"));
            account.Profile.Settings.KnownLanguageLevels = LoadLanguageLevels(connection, account.Login);
            account.Profile.Settings.KnownLanguages = MergeDistinct(account.Profile.Settings.KnownLanguages, account.Profile.Settings.KnownLanguageLevels.Keys);
            account.Profile.Settings.Certifications = LoadStringList(connection, account.Login, "public.user_certifications", "certification_code");
            account.Profile.Settings.DrivingLicenses = account.Profile.Settings.Certifications
                .Where(code => code.Contains("driving", StringComparison.OrdinalIgnoreCase) || code.Contains("prawo", StringComparison.OrdinalIgnoreCase))
                .ToList();
            account.Profile.Settings.ExcludedFlags = LoadStringList(connection, account.Login, "public.user_excluded_flags", "flag_code");
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
                    user_id, username, email, default_location, notifications_enabled, dark_mode, theme,
                    expected_salary, expected_salary_min, salary_currency, salary_tax_type, work_mode_preference,
                    max_office_days_per_week, work_time_type_preference, max_distance_km,
                    preferred_category_code, preferred_role_code, job_title, experience, education, created_at, updated_at
                )
                values (
                    @user_id, @username, @email, @default_location, @notifications_enabled, @dark_mode, @theme,
                    @expected_salary, @expected_salary_min, @salary_currency, @salary_tax_type, @work_mode_preference,
                    @max_office_days_per_week, @work_time_type_preference, @max_distance_km,
                    @preferred_category_code, @preferred_role_code, @job_title, @experience, @education, now(), now()
                )
                on conflict (user_id)
                do update set
                    username = excluded.username,
                    email = excluded.email,
                    default_location = excluded.default_location,
                    notifications_enabled = excluded.notifications_enabled,
                    dark_mode = excluded.dark_mode,
                    theme = excluded.theme,
                    expected_salary = excluded.expected_salary,
                    expected_salary_min = excluded.expected_salary_min,
                    salary_currency = excluded.salary_currency,
                    salary_tax_type = excluded.salary_tax_type,
                    work_mode_preference = excluded.work_mode_preference,
                    max_office_days_per_week = excluded.max_office_days_per_week,
                    work_time_type_preference = excluded.work_time_type_preference,
                    max_distance_km = excluded.max_distance_km,
                    preferred_category_code = excluded.preferred_category_code,
                    preferred_role_code = excluded.preferred_role_code,
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
                profileCommand.Parameters.AddWithValue("theme", UserSettings.NormalizeTheme(account.Profile?.Settings?.Theme));
                profileCommand.Parameters.AddWithValue("expected_salary", account.Profile?.Settings?.ExpectedSalary ?? string.Empty);
                profileCommand.Parameters.AddWithValue("expected_salary_min", (object?)account.Profile?.Settings?.ExpectedSalaryMin ?? DBNull.Value);
                profileCommand.Parameters.AddWithValue("salary_currency", account.Profile?.Settings?.SalaryCurrency ?? "PLN");
                profileCommand.Parameters.AddWithValue("salary_tax_type", account.Profile?.Settings?.SalaryTaxType ?? "unknown");
                profileCommand.Parameters.AddWithValue("work_mode_preference", account.Profile?.Settings?.WorkMode ?? "any");
                profileCommand.Parameters.AddWithValue("max_office_days_per_week", (object?)account.Profile?.Settings?.MaxOfficeDaysPerWeek ?? DBNull.Value);
                profileCommand.Parameters.AddWithValue("work_time_type_preference", account.Profile?.Settings?.WorkTimeType ?? "any");
                profileCommand.Parameters.AddWithValue("max_distance_km", (object?)account.Profile?.Settings?.MaxDistanceKm ?? DBNull.Value);
                profileCommand.Parameters.AddWithValue("preferred_category_code", account.Profile?.Settings?.PreferredCategoryCode ?? string.Empty);
                profileCommand.Parameters.AddWithValue("preferred_role_code", account.Profile?.Settings?.PreferredRoleCode ?? string.Empty);
                profileCommand.Parameters.AddWithValue("job_title", account.Profile?.Settings?.JobTitle ?? string.Empty);
                profileCommand.Parameters.AddWithValue("experience", account.Profile?.Settings?.Experience ?? "Wszystkie");
                profileCommand.Parameters.AddWithValue("education", account.Profile?.Settings?.Education ?? "Brak wymagań lub niewymagane");
                profileCommand.ExecuteNonQuery();
            }

            var settings = account.Profile?.Settings;
            ReplaceStringList(connection, transaction, userId, "public.user_profile_skills", "skill_name", account.Profile?.Settings?.Skills);
            ReplaceStringList(connection, transaction, userId, "public.user_profile_languages", "language_name", account.Profile?.Settings?.KnownLanguages);
            ReplaceStringList(connection, transaction, userId, "public.user_profile_contract_types", "contract_type", account.Profile?.Settings?.PreferredContractTypes);
            ReplaceUserSkills(connection, transaction, userId, settings);
            ReplaceUserLanguages(connection, transaction, userId, settings);
            ReplaceStringList(connection, transaction, userId, "public.user_certifications", "certification_code", (settings?.Certifications ?? new List<string>()).Concat(settings?.DrivingLicenses ?? new List<string>()));
            ReplaceStringList(connection, transaction, userId, "public.user_excluded_flags", "flag_code", settings?.ExcludedFlags);
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
                    theme varchar(20) not null default 'light',
                    expected_salary varchar(100) null,
                    job_title varchar(200) null,
                    experience varchar(100) not null default 'Wszystkie',
                    education varchar(100) not null default 'Brak wymagań lub niewymagane',
                    created_at timestamptz not null default now(),
                    updated_at timestamptz not null default now()
                );

                alter table public.user_profiles
                    add column if not exists expected_salary_min numeric(12,2),
                    add column if not exists theme varchar(20) not null default 'light',
                    add column if not exists salary_currency varchar(10) not null default 'PLN',
                    add column if not exists salary_tax_type varchar(30) not null default 'unknown',
                    add column if not exists work_mode_preference varchar(50) not null default 'any',
                    add column if not exists max_office_days_per_week numeric(3,1),
                    add column if not exists work_time_type_preference varchar(50) not null default 'any',
                    add column if not exists max_distance_km integer,
                    add column if not exists preferred_category_code varchar(100) not null default '',
                    add column if not exists preferred_role_code varchar(100) not null default '';

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

                create table if not exists public.user_skills (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    skill_code varchar(150) not null,
                    skill_kind varchar(60),
                    proficiency_level varchar(40),
                    created_at timestamptz not null default now(),
                    primary key (user_id, skill_code)
                );

                create table if not exists public.user_languages (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    language_code varchar(20) not null,
                    level_min varchar(20) not null default 'unknown',
                    created_at timestamptz not null default now(),
                    primary key (user_id, language_code)
                );

                create table if not exists public.user_certifications (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    certification_code varchar(150) not null,
                    created_at timestamptz not null default now(),
                    primary key (user_id, certification_code)
                );

                create table if not exists public.user_excluded_flags (
                    user_id bigint not null references public.app_users(id) on delete cascade,
                    flag_code varchar(150) not null,
                    created_at timestamptz not null default now(),
                    primary key (user_id, flag_code)
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

        private static List<string> MergeDistinct(IEnumerable<string>? first, IEnumerable<string>? second)
        {
            return (first ?? Enumerable.Empty<string>())
                .Concat(second ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, string> LoadLanguageLevels(NpgsqlConnection connection, string login)
        {
            const string sql = """
                select language_code, coalesce(level_min, 'unknown') as level_min
                from public.user_languages ul
                join public.app_users u on u.id = ul.user_id
                where lower(u.login) = lower(@login)
                order by language_code;
                """;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("login", login);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                values[reader.GetString(0)] = reader.GetString(1);
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

        private static void ReplaceUserSkills(NpgsqlConnection connection, NpgsqlTransaction transaction, long userId, UserSettings? settings)
        {
            using (var deleteCommand = new NpgsqlCommand("delete from public.user_skills where user_id = @user_id", connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("user_id", userId);
                deleteCommand.ExecuteNonQuery();
            }

            var values = new List<(string Code, string Kind)>();
            values.AddRange((settings?.Skills ?? new List<string>()).Select(code => (code, "skill")));
            values.AddRange((settings?.PersonalTraits ?? new List<string>()).Select(code => (code, "trait")));
            values.AddRange((settings?.WorkActivities ?? new List<string>()).Select(code => (code, "work_activity")));
            values.AddRange((settings?.DrivingLicenses ?? new List<string>()).Select(code => (code, "certification")));

            foreach (var value in values
                         .Where(value => !string.IsNullOrWhiteSpace(value.Code))
                         .GroupBy(value => value.Code, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First()))
            {
                using var insertCommand = new NpgsqlCommand(
                    """
                    insert into public.user_skills (user_id, skill_code, skill_kind, proficiency_level)
                    values (@user_id, @code, @kind, 'known')
                    on conflict (user_id, skill_code)
                    do update set skill_kind = excluded.skill_kind, proficiency_level = excluded.proficiency_level
                    """,
                    connection,
                    transaction);
                insertCommand.Parameters.AddWithValue("user_id", userId);
                insertCommand.Parameters.AddWithValue("code", value.Code);
                insertCommand.Parameters.AddWithValue("kind", value.Kind);
                insertCommand.ExecuteNonQuery();
            }
        }

        private static void ReplaceUserLanguages(NpgsqlConnection connection, NpgsqlTransaction transaction, long userId, UserSettings? settings)
        {
            using (var deleteCommand = new NpgsqlCommand("delete from public.user_languages where user_id = @user_id", connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("user_id", userId);
                deleteCommand.ExecuteNonQuery();
            }

            var languages = (settings?.KnownLanguages ?? new List<string>())
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var language in languages)
            {
                var level = settings?.KnownLanguageLevels.TryGetValue(language, out var savedLevel) == true
                    ? savedLevel
                    : "unknown";

                using var insertCommand = new NpgsqlCommand(
                    """
                    insert into public.user_languages (user_id, language_code, level_min)
                    values (@user_id, @language_code, @level_min)
                    on conflict (user_id, language_code)
                    do update set level_min = excluded.level_min
                    """,
                    connection,
                    transaction);
                insertCommand.Parameters.AddWithValue("user_id", userId);
                insertCommand.Parameters.AddWithValue("language_code", language);
                insertCommand.Parameters.AddWithValue("level_min", string.IsNullOrWhiteSpace(level) ? "unknown" : level);
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
