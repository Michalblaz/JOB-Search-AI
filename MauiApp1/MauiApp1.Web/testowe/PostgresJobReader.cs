using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MauiApp1.testowe
{
    public class PostgresJobReader
    {
        private readonly string _connectionString;

        public PostgresJobReader(AppSettingsProvider settingsProvider)
        {
            _connectionString = settingsProvider.GetSettings().Database.ConnectionString;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

        public async Task<List<JobOffer>> LoadActiveOffersAsync(IEnumerable<string> selectedSources)
        {
            if (!IsConfigured)
            {
                return new List<JobOffer>();
            }

            var normalizedSources = NormalizeSourceCodes(selectedSources);

            const string sql = """
                select
                    jo.id,
                    jo.external_id,
                    jo.title,
                    coalesce(jo.company_name, 'Nieznana firma') as company_name,
                    jo.company_logo_url,
                    coalesce(jo.location_name, 'Nie podano lokalizacji') as location_name,
                    coalesce(jo.salary_raw, 'Do uzgodnienia') as salary_raw,
                    coalesce(jo.description_short, jo.description, 'Brak opisu oferty.') as description_value,
                    coalesce(jo.experience_level, 'Wszystkie') as experience_level,
                    coalesce(jo.education_level, 'Brak wymagań lub niewymagane') as education_level,
                    js.code,
                    js.display_name,
                    jo.external_url,
                    coalesce(jo.employment_type, 'Nie podano') as employment_type,
                    coalesce(jo.contract_type, 'Nie podano') as contract_type,
                    coalesce(jo.published_at, jo.created_at) as published_at,
                    jo.is_remote,
                    coalesce(array_remove(array_agg(distinct jol.language_name), null), '{}'::text[]) as languages,
                    coalesce(array_remove(array_agg(distinct jot.tag), null), '{}'::text[]) as tags,
                    coalesce(array_remove(array_agg(distinct cat.code), null), '{}'::text[]) as category_codes,
                    coalesce(array_remove(array_agg(distinct jr.code), null), '{}'::text[]) as role_codes,
                    coalesce(array_remove(array_agg(distinct jc.code), null), '{}'::text[]) as criterion_codes,
                    coalesce(array_remove(array_agg(distinct concat_ws('|', jc.kind, joc.is_required::text, jc.code)), null), '{}'::text[]) as criterion_signals,
                    jo.experience_min_years,
                    jo.experience_max_years,
                    jo.no_experience_allowed,
                    jo.experience_required,
                    jo.experience_confidence,
                    jo.experience_evidence,
                    jo.education_required,
                    jo.education_field,
                    jo.education_confidence,
                    jo.education_evidence
                from public.job_offers jo
                join public.job_sources js on js.id = jo.source_id
                left join public.job_offer_languages jol on jol.job_offer_id = jo.id
                left join public.job_offer_tags jot on jot.job_offer_id = jo.id
                left join public.job_offer_roles jor on jor.job_offer_id = jo.id
                left join public.job_roles jr on jr.id = jor.role_id
                left join public.job_categories cat on cat.id = jr.category_id
                left join public.job_offer_criteria joc on joc.job_offer_id = jo.id
                left join public.job_criteria jc on jc.id = joc.criterion_id
                where jo.is_active = true
                  and (@source_count = 0 or js.code = any(@sources))
                group by jo.id, js.code, js.display_name
                order by coalesce(jo.published_at, jo.created_at) desc
                limit 2500;
                """;

            var offers = new List<JobOffer>();
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("source_count", normalizedSources.Length);
            command.Parameters.Add("sources", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = normalizedSources;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var salary = reader.GetString(6);
                var contractTime = reader.GetString(13);
                var contractType = reader.GetString(14);
                var publishedAtValue = reader.IsDBNull(15) ? null : reader.GetValue(15)?.ToString();
                var publishedAt = FormatPublishedDate(publishedAtValue);
                var categoryCodes = reader.GetFieldValue<string[]>(19).ToList();
                var roleCodes = reader.GetFieldValue<string[]>(20).ToList();
                var criterionCodes = reader.GetFieldValue<string[]>(21).ToList();
                var criteria = ParseCriterionSignals(reader.GetFieldValue<string[]>(22));

                var offer = new JobOffer
                {
                    JobOfferId = reader.GetInt64(0),
                    ExternalId = reader.GetString(1),
                    Title = reader.GetString(2),
                    Company = reader.GetString(3),
                    CompanyLogoUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Location = reader.GetString(5),
                    Salary = salary,
                    SalaryDetails = salary,
                    Description = reader.GetString(7),
                    Experience = NormalizeExperienceLabel(reader.GetString(8)),
                    Education = NormalizeEducationLabel(reader.GetString(9)),
                    ExperienceMinYears = reader.IsDBNull(23) ? null : reader.GetDecimal(23),
                    ExperienceMaxYears = reader.IsDBNull(24) ? null : reader.GetDecimal(24),
                    NoExperienceAllowed = !reader.IsDBNull(25) && reader.GetBoolean(25),
                    ExperienceRequired = reader.IsDBNull(26) ? null : reader.GetBoolean(26),
                    ExperienceConfidence = reader.IsDBNull(27) ? 0.5m : reader.GetDecimal(27),
                    ExperienceEvidence = reader.IsDBNull(28) ? null : reader.GetString(28),
                    EducationRequired = reader.IsDBNull(29) ? null : reader.GetBoolean(29),
                    EducationField = reader.IsDBNull(30) ? null : reader.GetString(30),
                    EducationConfidence = reader.IsDBNull(31) ? 0.5m : reader.GetDecimal(31),
                    EducationEvidence = reader.IsDBNull(32) ? null : reader.GetString(32),
                    SourceCode = reader.GetString(10),
                    Source = reader.GetString(11),
                    Url = reader.IsDBNull(12) ? null : reader.GetString(12),
                    ContractTime = contractTime,
                    ContractType = contractType,
                    PublishedAt = publishedAt,
                    PostedAgo = publishedAt,
                    IsRemote = !reader.IsDBNull(16) && reader.GetBoolean(16),
                    Languages = reader.GetFieldValue<string[]>(17).ToList(),
                    Tags = reader.GetFieldValue<string[]>(18).ToList(),
                    CategoryCodes = categoryCodes,
                    RoleCodes = roleCodes,
                    CriterionCodes = criterionCodes,
                    Criteria = criteria,
                    Category = categoryCodes.FirstOrDefault() ?? reader.GetString(11)
                };

                offers.Add(offer);
            }

            return offers;
        }

        private static List<JobOfferCriterionMatch> ParseCriterionSignals(IEnumerable<string> values)
        {
            return values
                .Select(value => value.Split('|', 3))
                .Where(parts => parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[2]))
                .Select(parts => new JobOfferCriterionMatch
                {
                    Kind = parts[0],
                    IsRequired = bool.TryParse(parts[1], out var isRequired) && isRequired,
                    Code = parts[2]
                })
                .GroupBy(item => $"{item.Kind}|{item.Code}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.IsRequired).First())
                .ToList();
        }

        public async Task<List<JobRoleOption>> LoadRoleOptionsAsync(IEnumerable<string> selectedSources)
        {
            if (!IsConfigured)
            {
                return new List<JobRoleOption>();
            }

            var normalizedSources = NormalizeSourceCodes(selectedSources);

            const string sql = """
                select
                    cat.code as category_code,
                    cat.display_name as category_name,
                    jr.code as role_code,
                    jr.display_name as role_name,
                    count(distinct jo.id)::int as active_offer_count
                from public.job_roles jr
                join public.job_categories cat on cat.id = jr.category_id
                join public.job_offer_roles jor on jor.role_id = jr.id
                join public.job_offers jo on jo.id = jor.job_offer_id and jo.is_active = true
                join public.job_sources js on js.id = jo.source_id
                where (@source_count = 0 or js.code = any(@sources))
                group by cat.code, cat.display_name, jr.code, jr.display_name
                order by cat.display_name, active_offer_count desc, jr.display_name;
                """;

            var options = new List<JobRoleOption>();
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("source_count", normalizedSources.Length);
            command.Parameters.Add("sources", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = normalizedSources;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                options.Add(new JobRoleOption
                {
                    CategoryCode = reader.GetString(0),
                    CategoryName = reader.GetString(1),
                    RoleCode = reader.GetString(2),
                    RoleName = reader.GetString(3),
                    ActiveOfferCount = reader.GetInt32(4)
                });
            }

            return options;
        }

        public async Task<List<JobCriterionOption>> LoadCriteriaOptionsAsync(string? categoryCode, string? roleCode, IEnumerable<string> selectedSources)
        {
            if (!IsConfigured)
            {
                return new List<JobCriterionOption>();
            }

            var normalizedSources = NormalizeSourceCodes(selectedSources);

            const string sql = """
                select
                    jc.kind,
                    jc.code,
                    jc.display_name,
                    count(distinct jo.id)::int as active_offer_count
                from public.job_criteria jc
                join public.job_offer_criteria joc on joc.criterion_id = jc.id
                join public.job_offers jo on jo.id = joc.job_offer_id and jo.is_active = true
                join public.job_sources js on js.id = jo.source_id
                left join public.job_offer_roles jor on jor.job_offer_id = jo.id
                left join public.job_roles jr on jr.id = jor.role_id
                left join public.job_categories cat on cat.id = jr.category_id
                where jc.is_user_selectable = true
                  and (@source_count = 0 or js.code = any(@sources))
                  and (@category_code is null or cat.code = @category_code)
                  and (@role_code is null or jr.code = @role_code)
                group by jc.kind, jc.code, jc.display_name
                order by jc.kind, active_offer_count desc, jc.display_name;
                """;

            var options = new List<JobCriterionOption>();
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("source_count", normalizedSources.Length);
            command.Parameters.Add("sources", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = normalizedSources;
            command.Parameters.Add("category_code", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(categoryCode) ? DBNull.Value : categoryCode;
            command.Parameters.Add("role_code", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(roleCode) ? DBNull.Value : roleCode;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                options.Add(new JobCriterionOption
                {
                    Kind = reader.GetString(0),
                    Code = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    ActiveOfferCount = reader.GetInt32(3)
                });
            }

            return options;
        }

        private static string NormalizeEducationLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Brak wymagań lub niewymagane";
            }

            var normalized = value.Trim();

            return normalized switch
            {
                "WyĹĽsze" => "Wyższe",
                "Ĺšrednie" => "Średnie",
                "Brak wymagaĹ„ lub niewymagane" => "Brak wymagań lub niewymagane",
                "Brak wymagań lub niewymagane" => "Brak wymagań lub niewymagane",
                _ when normalized.Contains("student", StringComparison.OrdinalIgnoreCase) => "Student / w trakcie",
                _ => normalized
            };
        }

        private static string NormalizeExperienceLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Wszystkie";
            }

            var normalized = value.Trim();

            return normalized switch
            {
                "MĹ‚odszy specjalista" => "Młodszy specjalista",
                "MenadĹĽer" => "Menadżer",
                "Brak doĹ›wiadczenia" => "Brak doświadczenia",
                _ => normalized
            };
        }

        private static string MapSourceToCode(string sourceDisplayName)
        {
            return sourceDisplayName switch
            {
                "Adzuna" => "adzuna",
                "Jooble" => "jooble",
                "Remotive" => "remotive",
                "Arbeitnow" => "arbeitnow",
                "ePraca (CBOP)" => "epraca",
                _ => sourceDisplayName.ToLowerInvariant()
            };
        }

        private static string[] NormalizeSourceCodes(IEnumerable<string> selectedSources)
        {
            return selectedSources
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Select(MapSourceToCode)
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
    }
}
