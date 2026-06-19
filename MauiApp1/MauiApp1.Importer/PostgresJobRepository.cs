using Npgsql;
using NpgsqlTypes;

namespace MauiApp1.Importer;

/// <summary>
/// Zapisuje znormalizowane oferty pracy do bazy PostgreSQL i rejestruje przebieg importu.
/// </summary>
/// <remarks>
/// Repozytorium odpowiada za operacje transakcyjne wokół tabel <c>job_offers</c>, <c>job_offer_languages</c>,
/// <c>job_offer_tags</c> oraz <c>job_import_runs</c>. Wykorzystuje klucz konfliktu źródło + zewnętrzne ID, aby aktualizować
/// istniejące oferty bez tworzenia duplikatów.
/// </remarks>
/// <seealso cref="JobImportCoordinator"/>
/// <seealso cref="NormalizedJobOffer"/>
public sealed class PostgresJobRepository
{
    private static readonly string[] TechnicalCriterionKinds =
    {
        "technology",
        "programming_language",
        "framework",
        "database",
        "cloud",
        "devops_tool",
        "testing_tool",
        "data_tool",
        "ai_tool",
        "integration_tool",
        "business_tool",
        "industrial_tool",
        "methodology"
    };

    private readonly string _connectionString;
    private readonly bool _deactivateMissingOffers;
    private readonly Dictionary<string, int> _sourceIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tworzy repozytorium zapisujące import do PostgreSQL.
    /// </summary>
    /// <param name="connectionString">Connection string do bazy z tabelami ofert pracy.</param>
    /// <param name="deactivateMissingOffers">Czy dezaktywować aktywne oferty, których nie było w bieżącym imporcie.</param>
    public PostgresJobRepository(string connectionString, bool deactivateMissingOffers)
    {
        _connectionString = connectionString;
        _deactivateMissingOffers = deactivateMissingOffers;
    }

    /// <summary>
    /// Ładuje identyfikatory źródeł z tabeli <c>job_sources</c> do pamięci repozytorium.
    /// </summary>
    /// <returns>Zadanie zakończone po odczycie mapowania kod źródła - ID.</returns>
    /// <exception cref="Npgsql.NpgsqlException">Gdy baza danych jest niedostępna albo schemat nie zawiera wymaganej tabeli.</exception>
    public async Task EnsureSourceIdsAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureClassificationSchemaAsync(connection);
        await using var command = new NpgsqlCommand("select id, code from public.job_sources", connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            _sourceIds[reader.GetString(1)] = reader.GetInt32(0);
        }
    }

    public async Task<int> ReclassifyExistingOffersAsync(ReclassificationOptions? options = null)
    {
        options ??= new ReclassificationOptions();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureClassificationSchemaAsync(connection);

        var offers = new List<ExistingOfferForClassification>();
        var sql = new List<string>
        {
            """
            select
                jo.id,
                jo.title,
                jo.description,
                jo.location_name,
                jo.salary_min,
                jo.salary_max,
                jo.salary_raw,
                jo.employment_type,
                jo.contract_type,
                jo.is_remote,
                coalesce(array_remove(array_agg(distinct jot.tag), null), '{}'::text[]) as tags
            from public.job_offers jo
            left join public.job_offer_tags jot on jot.job_offer_id = jo.id
            left join public.job_offer_roles jor on jor.job_offer_id = jo.id
            left join public.job_roles jr on jr.id = jor.role_id
            left join public.job_categories cat on cat.id = jr.category_id
            where jo.is_active = true
            """
        };

        if (!string.IsNullOrWhiteSpace(options.CategoryCode))
        {
            sql.Add("and cat.code = @category_code");
        }

        if (options.OnlyWithoutCriteria)
        {
            sql.Add("""
                and not exists (
                    select 1 from public.job_offer_criteria joc where joc.job_offer_id = jo.id
                )
                """);
        }

        sql.Add("""
            group by jo.id, jo.title, jo.description, jo.location_name, jo.salary_min, jo.salary_max, jo.salary_raw, jo.employment_type, jo.contract_type, jo.is_remote
            order by jo.id
            """);

        if (options.Limit.HasValue)
        {
            sql.Add("limit @limit");
        }

        await using (var command = new NpgsqlCommand(string.Join(Environment.NewLine, sql), connection))
        {
            if (!string.IsNullOrWhiteSpace(options.CategoryCode))
            {
                command.Parameters.AddWithValue("category_code", options.CategoryCode);
            }

            if (options.Limit.HasValue)
            {
                command.Parameters.AddWithValue("limit", options.Limit.Value);
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                offers.Add(new ExistingOfferForClassification(
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    !reader.IsDBNull(9) && reader.GetBoolean(9),
                    reader.GetFieldValue<string[]>(10).ToList()));
            }
        }

        foreach (var offer in offers)
        {
            var classification = JobClassificationRules.Classify(offer.Title, offer.Description, offer.Tags);
            var experience = ImporterHelpers.DetectExperienceInfo(offer.Title, offer.Description);
            var education = ImporterHelpers.DetectEducationInfo(offer.Title, offer.Description);
            var normalized = new NormalizedJobOffer
            {
                Title = offer.Title ?? string.Empty,
                Description = offer.Description,
                LocationName = offer.LocationName,
                SalaryMin = offer.SalaryMin,
                SalaryMax = offer.SalaryMax,
                SalaryRaw = offer.SalaryRaw,
                EmploymentType = offer.EmploymentType,
                ContractType = offer.ContractType,
                IsRemote = offer.IsRemote,
                Tags = offer.Tags,
                Experience = experience,
                Education = education,
                ExperienceLevel = experience.Level,
                EducationLevel = education.Level,
                Languages = ImporterHelpers.DetectLanguagesWithLevels(offer.Title, offer.Description),
                Classification = classification
            };
            ImporterHelpers.PopulateDerivedOfferData(normalized);
            if (options.DryRun)
            {
                Console.WriteLine($"[DRY-RUN] offer={offer.Id}, experience={experience.Level}, education={education.Level}, contracts={normalized.ContractTypes.Count}, work_mode={normalized.WorkMode.WorkMode}, work_time={normalized.WorkTime.WorkTimeType}, benefits={normalized.Benefits.Count}, criteria={classification.Criteria.Count}, hits={classification.CriterionHits.Count}");
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync();
            await UpdateDerivedOfferFieldsAsync(connection, transaction, offer.Id, normalized);
            await ReplaceClassificationAsync(connection, transaction, offer.Id, classification);
            await ReplaceLanguagesAsync(connection, transaction, offer.Id, normalized.Languages);
            await ReplaceContractTypesAsync(connection, transaction, offer.Id, normalized.ContractTypes);
            await ReplaceScheduleFlagsAsync(connection, transaction, offer.Id, normalized.ScheduleFlags);
            await ReplaceBenefitsAsync(connection, transaction, offer.Id, normalized.Benefits);
            await ReplaceDomainsAsync(connection, transaction, offer.Id, normalized.Domains);
            await ReplaceFormalRequirementsAsync(connection, transaction, offer.Id, normalized.FormalRequirements);
            await transaction.CommitAsync();
        }

        return offers.Count;
    }

    public async Task<List<string>> GetClassificationStatsAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureClassificationSchemaAsync(connection);

        var lines = new List<string>();
        await using (var command = new NpgsqlCommand("""
            select count(*)::bigint from public.job_offers where is_active = true
            """, connection))
        {
            lines.Add($"offers.total={Convert.ToInt64(await command.ExecuteScalarAsync())}");
        }

        await using (var command = new NpgsqlCommand("""
            select count(*)::bigint
            from public.job_offers jo
            where jo.is_active = true
              and not exists (
                  select 1 from public.job_offer_criteria joc where joc.job_offer_id = jo.id
              )
            """, connection))
        {
            lines.Add($"offers.without_criteria={Convert.ToInt64(await command.ExecuteScalarAsync())}");
        }

        await using (var command = new NpgsqlCommand("""
            select
                count(*) filter (where salary_min is not null or salary_max is not null or nullif(salary_raw, '') is not null)::bigint,
                count(*) filter (where salary_min is null and salary_max is null and nullif(salary_raw, '') is null)::bigint,
                count(*) filter (where exists (select 1 from public.job_offer_contract_types x where x.job_offer_id = jo.id))::bigint,
                count(*) filter (where not exists (select 1 from public.job_offer_contract_types x where x.job_offer_id = jo.id))::bigint,
                count(*) filter (where coalesce(work_mode, 'unknown') <> 'unknown')::bigint,
                count(*) filter (where coalesce(work_mode, 'unknown') = 'unknown')::bigint,
                count(*) filter (where coalesce(work_time_type, 'unknown') <> 'unknown')::bigint,
                count(*) filter (where coalesce(work_time_type, 'unknown') = 'unknown')::bigint,
                count(*) filter (where coalesce(experience_level, 'Wszystkie') <> 'Wszystkie' or experience_min_years is not null)::bigint,
                count(*) filter (where coalesce(experience_level, 'Wszystkie') = 'Wszystkie' and experience_min_years is null)::bigint,
                count(*) filter (where coalesce(education_level, 'Brak wymagań lub niewymagane') <> 'Brak wymagań lub niewymagane' or education_field is not null)::bigint,
                count(*) filter (where coalesce(education_level, 'Brak wymagań lub niewymagane') = 'Brak wymagań lub niewymagane' and education_field is null)::bigint,
                count(*) filter (where exists (select 1 from public.job_offer_benefits x where x.job_offer_id = jo.id))::bigint,
                count(*) filter (where exists (
                    select 1
                    from public.job_offer_criteria joc
                    join public.job_criteria jc on jc.id = joc.criterion_id
                    where joc.job_offer_id = jo.id and joc.is_required and jc.kind = any(@technical_kinds)
                ))::bigint,
                count(*) filter (where not exists (
                    select 1
                    from public.job_offer_criteria joc
                    join public.job_criteria jc on jc.id = joc.criterion_id
                    where joc.job_offer_id = jo.id and joc.is_required and jc.kind = any(@technical_kinds)
                ))::bigint,
                count(*) filter (where data_quality_score < 40 or extraction_score < 25)::bigint
            from public.job_offers jo
            where jo.is_active = true
            """, connection))
        {
            command.Parameters.AddWithValue("technical_kinds", TechnicalCriterionKinds);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                lines.Add($"offers.with_salary={reader.GetInt64(0)}");
                lines.Add($"offers.without_salary={reader.GetInt64(1)}");
                lines.Add($"offers.with_contract={reader.GetInt64(2)}");
                lines.Add($"offers.without_contract={reader.GetInt64(3)}");
                lines.Add($"offers.with_work_mode={reader.GetInt64(4)}");
                lines.Add($"offers.without_work_mode={reader.GetInt64(5)}");
                lines.Add($"offers.with_work_time={reader.GetInt64(6)}");
                lines.Add($"offers.without_work_time={reader.GetInt64(7)}");
                lines.Add($"offers.with_experience={reader.GetInt64(8)}");
                lines.Add($"offers.without_experience={reader.GetInt64(9)}");
                lines.Add($"offers.with_education={reader.GetInt64(10)}");
                lines.Add($"offers.without_education={reader.GetInt64(11)}");
                lines.Add($"offers.with_benefits={reader.GetInt64(12)}");
                lines.Add($"offers.with_required_skills={reader.GetInt64(13)}");
                lines.Add($"offers.without_required_skills={reader.GetInt64(14)}");
                lines.Add($"offers.low_quality={reader.GetInt64(15)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select requirement_level, count(*)::bigint
            from public.job_offer_criteria
            group by requirement_level
            order by requirement_level
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                lines.Add($"criteria.{reader.GetString(0)}={reader.GetInt64(1)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select
                count(*) filter (where no_experience_allowed)::bigint,
                count(*) filter (where experience_min_years is not null)::bigint,
                count(*) filter (where coalesce(experience_level, 'Wszystkie') = 'Wszystkie' and experience_min_years is null)::bigint,
                count(*) filter (where experience_level in ('Junior', 'Młodszy specjalista'))::bigint,
                count(*) filter (where experience_level in ('Mid / Regular', 'Specjalista'))::bigint,
                count(*) filter (where experience_level in ('Senior', 'Starszy specjalista'))::bigint,
                count(*) filter (where experience_level = 'Lead / Principal')::bigint
            from public.job_offers
            where is_active = true
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                lines.Add($"experience.no_experience_allowed={reader.GetInt64(0)}");
                lines.Add($"experience.with_min_years={reader.GetInt64(1)}");
                lines.Add($"experience.without_detected={reader.GetInt64(2)}");
                lines.Add($"experience.junior={reader.GetInt64(3)}");
                lines.Add($"experience.mid={reader.GetInt64(4)}");
                lines.Add($"experience.senior={reader.GetInt64(5)}");
                lines.Add($"experience.lead={reader.GetInt64(6)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select
                count(*) filter (where education_required = true)::bigint,
                count(*) filter (where education_required = false)::bigint,
                count(*) filter (where education_field is not null)::bigint,
                count(*) filter (where coalesce(education_level, 'Brak wymagań lub niewymagane') = 'Brak wymagań lub niewymagane' and education_field is null)::bigint,
                count(*) filter (where education_level = 'Wyższe')::bigint,
                count(*) filter (where education_level = 'Średnie')::bigint,
                count(*) filter (where education_level = 'Techniczne / średnie techniczne')::bigint,
                count(*) filter (where education_level = 'Student / w trakcie')::bigint
            from public.job_offers
            where is_active = true
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                lines.Add($"education.required={reader.GetInt64(0)}");
                lines.Add($"education.not_required={reader.GetInt64(1)}");
                lines.Add($"education.with_field={reader.GetInt64(2)}");
                lines.Add($"education.without_detected={reader.GetInt64(3)}");
                lines.Add($"education.higher={reader.GetInt64(4)}");
                lines.Add($"education.secondary={reader.GetInt64(5)}");
                lines.Add($"education.technical={reader.GetInt64(6)}");
                lines.Add($"education.student={reader.GetInt64(7)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select
                count(distinct jo.id)::bigint as it_offers,
                count(distinct jo.id) filter (
                    where joc.is_required and jc.kind = any(@technical_kinds)
                )::bigint as it_with_required_technology
            from public.job_offers jo
            join public.job_offer_roles jor on jor.job_offer_id = jo.id
            join public.job_roles jr on jr.id = jor.role_id
            join public.job_categories cat on cat.id = jr.category_id
            left join public.job_offer_criteria joc on joc.job_offer_id = jo.id
            left join public.job_criteria jc on jc.id = joc.criterion_id
            where jo.is_active = true
              and cat.code = 'it'
            """, connection))
        {
            command.Parameters.AddWithValue("technical_kinds", TechnicalCriterionKinds);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var itOffers = reader.GetInt64(0);
                var itWithRequiredTechnology = reader.GetInt64(1);
                lines.Add($"it.offers={itOffers}");
                lines.Add($"it.with_required_technology={itWithRequiredTechnology}");
                lines.Add($"it.without_required_technology={itOffers - itWithRequiredTechnology}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select jc.kind, jc.display_name, count(*)::bigint
            from public.job_offer_criteria joc
            join public.job_criteria jc on jc.id = joc.criterion_id
            group by jc.kind, jc.display_name
            order by count(*) desc, jc.display_name
            limit 30
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                lines.Add($"top.criteria.{reader.GetString(0)}.{reader.GetString(1)}={reader.GetInt64(2)}");
            }
        }

        return lines;
    }

    public async Task<List<string>> GetClassificationGapReportAsync(int limit = 80)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureClassificationSchemaAsync(connection);

        var lines = new List<string>();
        await using (var command = new NpgsqlCommand("""
            select
                js.code,
                coalesce(jo.description_quality, 'unknown') as description_quality,
                count(*)::bigint as total,
                count(*) filter (
                    where not exists (select 1 from public.job_offer_criteria joc where joc.job_offer_id = jo.id)
                )::bigint as without_criteria,
                count(*) filter (
                    where exists (
                        select 1
                        from public.job_offer_roles jor
                        join public.job_roles jr on jr.id = jor.role_id
                        join public.job_categories cat on cat.id = jr.category_id
                        where jor.job_offer_id = jo.id and cat.code = 'it'
                    )
                    and not exists (
                        select 1
                        from public.job_offer_criteria joc
                        join public.job_criteria jc on jc.id = joc.criterion_id
                        where joc.job_offer_id = jo.id and joc.is_required and jc.kind = any(@technical_kinds)
                    )
                )::bigint as it_without_required_technology
            from public.job_offers jo
            join public.job_sources js on js.id = jo.source_id
            where jo.is_active = true
            group by js.code, jo.description_quality
            order by without_criteria desc, total desc
            """, connection))
        {
            command.Parameters.AddWithValue("technical_kinds", TechnicalCriterionKinds);
            await using var reader = await command.ExecuteReaderAsync();
            lines.Add("coverage.by_source_quality:");
            while (await reader.ReadAsync())
            {
                lines.Add($"- source={reader.GetString(0)}, quality={reader.GetString(1)}, total={reader.GetInt64(2)}, without_criteria={reader.GetInt64(3)}, it_without_required_technology={reader.GetInt64(4)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select
                coalesce(cat.code, 'unknown') as category_code,
                coalesce(cat.display_name, 'Unknown') as category_name,
                count(distinct jo.id)::bigint as total,
                count(distinct jo.id) filter (
                    where not exists (select 1 from public.job_offer_criteria joc where joc.job_offer_id = jo.id)
                )::bigint as without_criteria
            from public.job_offers jo
            left join public.job_offer_roles jor on jor.job_offer_id = jo.id
            left join public.job_roles jr on jr.id = jor.role_id
            left join public.job_categories cat on cat.id = jr.category_id
            where jo.is_active = true
            group by cat.code, cat.display_name
            order by without_criteria desc, total desc
            limit 30
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            lines.Add("coverage.by_category:");
            while (await reader.ReadAsync())
            {
                lines.Add($"- category={reader.GetString(0)} ({reader.GetString(1)}), total={reader.GetInt64(2)}, without_criteria={reader.GetInt64(3)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select
                jo.id,
                js.code,
                jo.title,
                coalesce(jo.description_quality, 'unknown'),
                left(regexp_replace(coalesce(jo.description, ''), '\s+', ' ', 'g'), 520) as description_sample,
                coalesce(array_to_string(array_agg(distinct jot.tag) filter (where jot.tag is not null), ', '), '') as tags
            from public.job_offers jo
            join public.job_sources js on js.id = jo.source_id
            left join public.job_offer_tags jot on jot.job_offer_id = jo.id
            where jo.is_active = true
              and not exists (select 1 from public.job_offer_criteria joc where joc.job_offer_id = jo.id)
            group by jo.id, js.code
            order by jo.description_quality, length(coalesce(jo.description, '')) desc
            limit @limit
            """, connection))
        {
            command.Parameters.AddWithValue("limit", limit);
            await using var reader = await command.ExecuteReaderAsync();
            lines.Add("samples.without_criteria:");
            while (await reader.ReadAsync())
            {
                lines.Add($"- id={reader.GetInt64(0)}, source={reader.GetString(1)}, quality={reader.GetString(3)}, title={reader.GetString(2)}");
                lines.Add($"  tags={reader.GetString(5)}");
                lines.Add($"  text={reader.GetString(4)}");
            }
        }

        await using (var command = new NpgsqlCommand("""
            select
                jo.id,
                js.code,
                jo.title,
                coalesce(jr.display_name, ''),
                coalesce(jo.description_quality, 'unknown'),
                left(regexp_replace(coalesce(jo.description, ''), '\s+', ' ', 'g'), 620) as description_sample,
                coalesce(array_to_string(array_agg(distinct jot.tag) filter (where jot.tag is not null), ', '), '') as tags
            from public.job_offers jo
            join public.job_sources js on js.id = jo.source_id
            join public.job_offer_roles jor on jor.job_offer_id = jo.id
            join public.job_roles jr on jr.id = jor.role_id
            join public.job_categories cat on cat.id = jr.category_id
            left join public.job_offer_tags jot on jot.job_offer_id = jo.id
            where jo.is_active = true
              and cat.code = 'it'
              and not exists (
                  select 1
                  from public.job_offer_criteria joc
                  join public.job_criteria jc on jc.id = joc.criterion_id
                  where joc.job_offer_id = jo.id and joc.is_required and jc.kind = any(@technical_kinds)
              )
            group by jo.id, js.code, jr.display_name
            order by jo.description_quality, length(coalesce(jo.description, '')) desc
            limit @limit
            """, connection))
        {
            command.Parameters.AddWithValue("technical_kinds", TechnicalCriterionKinds);
            command.Parameters.AddWithValue("limit", limit);
            await using var reader = await command.ExecuteReaderAsync();
            lines.Add("samples.it_without_required_technology:");
            while (await reader.ReadAsync())
            {
                lines.Add($"- id={reader.GetInt64(0)}, source={reader.GetString(1)}, role={reader.GetString(3)}, quality={reader.GetString(4)}, title={reader.GetString(2)}");
                lines.Add($"  tags={reader.GetString(6)}");
                lines.Add($"  text={reader.GetString(5)}");
            }
        }

        return lines;
    }

    /// <summary>
    /// Rozpoczyna zapis przebiegu importu dla pojedynczego źródła.
    /// </summary>
    /// <param name="sourceCode">Kod źródła zgodny z tabelą <c>job_sources</c>.</param>
    /// <returns>Kontekst zawierający ID źródła i ID uruchomienia importu.</returns>
    public async Task<SourceImportContext> StartImportAsync(string sourceCode)
    {
        var sourceId = _sourceIds[sourceCode];
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "insert into public.job_import_runs (source_id, status) values (@source_id, 'running') returning id",
            connection);
        command.Parameters.AddWithValue("source_id", sourceId);
        var id = Convert.ToInt64(await command.ExecuteScalarAsync());
        return new SourceImportContext { SourceId = sourceId, ImportRunId = id };
    }

    /// <summary>
    /// Dodaje lub aktualizuje oferty oraz ich języki i tagi.
    /// </summary>
    /// <param name="context">Kontekst uruchomienia importu zwrócony przez <see cref="StartImportAsync"/>.</param>
    /// <param name="sourceCode">Kod źródła używany w statystykach importu.</param>
    /// <param name="offers">Lista znormalizowanych ofert do zapisania.</param>
    /// <returns>Statystyki liczby pobranych, dodanych, zaktualizowanych i dezaktywowanych ofert.</returns>
    /// <remarks>
    /// Po zapisie każdej oferty repozytorium odtwarza listę języków i tagów, ponieważ te dane są wielowartościowe i pochodzą
    /// z heurystyk lub pól źródłowych różniących się między API.
    /// </remarks>
    public async Task<SourceImportStats> UpsertOffersAsync(SourceImportContext context, string sourceCode, List<NormalizedJobOffer> offers)
    {
        var stats = new SourceImportStats
        {
            SourceCode = sourceCode,
            FetchedCount = offers.Count
        };

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var offer in offers)
        {
            ImporterHelpers.PopulateDerivedOfferData(offer);
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var result = await UpsertOfferAsync(connection, transaction, context, offer);
                await ReplaceLanguagesAsync(connection, transaction, result.JobOfferId, offer.Languages);
                await ReplaceTagsAsync(connection, transaction, result.JobOfferId, offer.Tags);
                await ReplaceClassificationAsync(connection, transaction, result.JobOfferId, offer.Classification);
                await ReplaceContractTypesAsync(connection, transaction, result.JobOfferId, offer.ContractTypes);
                await ReplaceScheduleFlagsAsync(connection, transaction, result.JobOfferId, offer.ScheduleFlags);
                await ReplaceBenefitsAsync(connection, transaction, result.JobOfferId, offer.Benefits);
                await ReplaceDomainsAsync(connection, transaction, result.JobOfferId, offer.Domains);
                await ReplaceFormalRequirementsAsync(connection, transaction, result.JobOfferId, offer.FormalRequirements);

                await transaction.CommitAsync();

                if (result.IsInsert)
                {
                    stats.InsertedCount += 1;
                }
                else
                {
                    stats.UpdatedCount += 1;
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        if (_deactivateMissingOffers)
        {
            stats.DeactivatedCount = await DeactivateMissingOffersAsync(connection, context);
        }

        await UpdateSourceSuccessAsync(connection, context.SourceId);
        return stats;
    }

    public async Task FinishImportAsync(long importRunId, string status, SourceImportStats stats)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(@"
            update public.job_import_runs
            set finished_at = now(),
                status = @status,
                fetched_count = @fetched_count,
                inserted_count = @inserted_count,
                updated_count = @updated_count,
                deactivated_count = @deactivated_count
            where id = @id", connection);
        command.Parameters.AddWithValue("id", importRunId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("fetched_count", stats.FetchedCount);
        command.Parameters.AddWithValue("inserted_count", stats.InsertedCount);
        command.Parameters.AddWithValue("updated_count", stats.UpdatedCount);
        command.Parameters.AddWithValue("deactivated_count", stats.DeactivatedCount);
        await command.ExecuteNonQueryAsync();
    }

    public async Task FailImportAsync(long importRunId, string errorMessage)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "update public.job_import_runs set finished_at = now(), status = 'failed', error_message = @message where id = @id",
            connection);
        command.Parameters.AddWithValue("id", importRunId);
        command.Parameters.AddWithValue("message", errorMessage);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(long JobOfferId, bool IsInsert)> UpsertOfferAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, SourceImportContext context, NormalizedJobOffer offer)
    {
        const string sql = """
            insert into public.job_offers (
                source_id, external_id, external_url, title, company_name, company_logo_url, location_name, city, region, country_code,
                description, description_quality, description_short, salary_min, salary_max, salary_currency, salary_raw, salary_is_predicted,
                salary_period, salary_tax_type, salary_rate_type, salary_confidence, salary_evidence,
                employment_type, contract_type, employment_type_raw, contract_type_raw,
                work_mode, remote_scope, remote_country_restriction, office_days_per_week_min, office_days_per_week_max, work_mode_confidence, work_mode_evidence,
                work_time_type, fte_min, fte_max, hours_per_week_min, hours_per_week_max, work_time_confidence, work_time_evidence,
                data_quality_score, extraction_score,
                experience_level, education_level,
                experience_min_years, experience_max_years, no_experience_allowed, experience_required, experience_confidence, experience_evidence,
                education_required, education_field, education_confidence, education_evidence,
                is_remote, latitude, longitude, is_active,
                published_at, last_seen_at, last_import_run_id, content_hash, external_reference, raw_payload_json
            )
            values (
                @source_id, @external_id, @external_url, @title, @company_name, @company_logo_url, @location_name, @city, @region, @country_code,
                @description, @description_quality, @description_short, @salary_min, @salary_max, @salary_currency, @salary_raw, @salary_is_predicted,
                @salary_period, @salary_tax_type, @salary_rate_type, @salary_confidence, @salary_evidence,
                @employment_type, @contract_type, @employment_type_raw, @contract_type_raw,
                @work_mode, @remote_scope, @remote_country_restriction, @office_days_per_week_min, @office_days_per_week_max, @work_mode_confidence, @work_mode_evidence,
                @work_time_type, @fte_min, @fte_max, @hours_per_week_min, @hours_per_week_max, @work_time_confidence, @work_time_evidence,
                @data_quality_score, @extraction_score,
                @experience_level, @education_level,
                @experience_min_years, @experience_max_years, @no_experience_allowed, @experience_required, @experience_confidence, @experience_evidence,
                @education_required, @education_field, @education_confidence, @education_evidence,
                @is_remote, @latitude, @longitude, true,
                @published_at, now(), @last_import_run_id, @content_hash, @external_reference, @raw_payload_json
            )
            on conflict (source_id, external_id)
            do update set
                external_url = excluded.external_url,
                title = excluded.title,
                company_name = excluded.company_name,
                company_logo_url = excluded.company_logo_url,
                location_name = excluded.location_name,
                city = excluded.city,
                region = excluded.region,
                country_code = excluded.country_code,
                description = excluded.description,
                description_quality = excluded.description_quality,
                description_short = excluded.description_short,
                salary_min = excluded.salary_min,
                salary_max = excluded.salary_max,
                salary_currency = excluded.salary_currency,
                salary_raw = excluded.salary_raw,
                salary_is_predicted = excluded.salary_is_predicted,
                salary_period = excluded.salary_period,
                salary_tax_type = excluded.salary_tax_type,
                salary_rate_type = excluded.salary_rate_type,
                salary_confidence = excluded.salary_confidence,
                salary_evidence = excluded.salary_evidence,
                employment_type = excluded.employment_type,
                contract_type = excluded.contract_type,
                employment_type_raw = excluded.employment_type_raw,
                contract_type_raw = excluded.contract_type_raw,
                work_mode = excluded.work_mode,
                remote_scope = excluded.remote_scope,
                remote_country_restriction = excluded.remote_country_restriction,
                office_days_per_week_min = excluded.office_days_per_week_min,
                office_days_per_week_max = excluded.office_days_per_week_max,
                work_mode_confidence = excluded.work_mode_confidence,
                work_mode_evidence = excluded.work_mode_evidence,
                work_time_type = excluded.work_time_type,
                fte_min = excluded.fte_min,
                fte_max = excluded.fte_max,
                hours_per_week_min = excluded.hours_per_week_min,
                hours_per_week_max = excluded.hours_per_week_max,
                work_time_confidence = excluded.work_time_confidence,
                work_time_evidence = excluded.work_time_evidence,
                data_quality_score = excluded.data_quality_score,
                extraction_score = excluded.extraction_score,
                experience_level = excluded.experience_level,
                education_level = excluded.education_level,
                experience_min_years = excluded.experience_min_years,
                experience_max_years = excluded.experience_max_years,
                no_experience_allowed = excluded.no_experience_allowed,
                experience_required = excluded.experience_required,
                experience_confidence = excluded.experience_confidence,
                experience_evidence = excluded.experience_evidence,
                education_required = excluded.education_required,
                education_field = excluded.education_field,
                education_confidence = excluded.education_confidence,
                education_evidence = excluded.education_evidence,
                is_remote = excluded.is_remote,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                is_active = true,
                published_at = excluded.published_at,
                last_seen_at = now(),
                last_import_run_id = excluded.last_import_run_id,
                content_hash = excluded.content_hash,
                external_reference = excluded.external_reference,
                raw_payload_json = excluded.raw_payload_json,
                updated_at = now()
            returning id, (xmax = 0) as inserted;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("source_id", context.SourceId);
        command.Parameters.AddWithValue("external_id", offer.ExternalId);
        command.Parameters.AddWithValue("external_url", (object?)offer.ExternalUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("title", offer.Title);
        command.Parameters.AddWithValue("company_name", (object?)offer.CompanyName ?? DBNull.Value);
        command.Parameters.AddWithValue("company_logo_url", (object?)offer.CompanyLogoUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("location_name", (object?)offer.LocationName ?? DBNull.Value);
        command.Parameters.AddWithValue("city", (object?)offer.City ?? DBNull.Value);
        command.Parameters.AddWithValue("region", (object?)offer.Region ?? DBNull.Value);
        command.Parameters.AddWithValue("country_code", (object?)offer.CountryCode ?? DBNull.Value);
        command.Parameters.AddWithValue("description", (object?)offer.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("description_quality", offer.DescriptionQuality);
        command.Parameters.AddWithValue("description_short", (object?)offer.DescriptionShort ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_min", (object?)offer.SalaryMin ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_max", (object?)offer.SalaryMax ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_currency", (object?)offer.SalaryCurrency ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_raw", (object?)offer.SalaryRaw ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_is_predicted", (object?)offer.SalaryIsPredicted ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_period", offer.SalaryInfo.SalaryPeriod);
        command.Parameters.AddWithValue("salary_tax_type", offer.SalaryInfo.SalaryTaxType);
        command.Parameters.AddWithValue("salary_rate_type", offer.SalaryInfo.SalaryRateType);
        command.Parameters.AddWithValue("salary_confidence", offer.SalaryInfo.Confidence);
        command.Parameters.AddWithValue("salary_evidence", (object?)offer.SalaryInfo.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("employment_type", (object?)offer.EmploymentType ?? DBNull.Value);
        command.Parameters.AddWithValue("contract_type", (object?)offer.ContractType ?? DBNull.Value);
        command.Parameters.AddWithValue("employment_type_raw", (object?)offer.EmploymentTypeRaw ?? DBNull.Value);
        command.Parameters.AddWithValue("contract_type_raw", (object?)offer.ContractTypeRaw ?? DBNull.Value);
        command.Parameters.AddWithValue("work_mode", offer.WorkMode.WorkMode);
        command.Parameters.AddWithValue("remote_scope", offer.WorkMode.RemoteScope);
        command.Parameters.AddWithValue("remote_country_restriction", (object?)offer.WorkMode.RemoteCountryRestriction ?? DBNull.Value);
        command.Parameters.AddWithValue("office_days_per_week_min", (object?)offer.WorkMode.OfficeDaysPerWeekMin ?? DBNull.Value);
        command.Parameters.AddWithValue("office_days_per_week_max", (object?)offer.WorkMode.OfficeDaysPerWeekMax ?? DBNull.Value);
        command.Parameters.AddWithValue("work_mode_confidence", offer.WorkMode.Confidence);
        command.Parameters.AddWithValue("work_mode_evidence", (object?)offer.WorkMode.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("work_time_type", offer.WorkTime.WorkTimeType);
        command.Parameters.AddWithValue("fte_min", (object?)offer.WorkTime.FteMin ?? DBNull.Value);
        command.Parameters.AddWithValue("fte_max", (object?)offer.WorkTime.FteMax ?? DBNull.Value);
        command.Parameters.AddWithValue("hours_per_week_min", (object?)offer.WorkTime.HoursPerWeekMin ?? DBNull.Value);
        command.Parameters.AddWithValue("hours_per_week_max", (object?)offer.WorkTime.HoursPerWeekMax ?? DBNull.Value);
        command.Parameters.AddWithValue("work_time_confidence", offer.WorkTime.Confidence);
        command.Parameters.AddWithValue("work_time_evidence", (object?)offer.WorkTime.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("data_quality_score", offer.DataQualityScore);
        command.Parameters.AddWithValue("extraction_score", offer.ExtractionScore);
        command.Parameters.AddWithValue("experience_level", (object?)offer.ExperienceLevel ?? DBNull.Value);
        command.Parameters.AddWithValue("education_level", (object?)offer.EducationLevel ?? DBNull.Value);
        command.Parameters.AddWithValue("experience_min_years", (object?)offer.Experience.MinYears ?? DBNull.Value);
        command.Parameters.AddWithValue("experience_max_years", (object?)offer.Experience.MaxYears ?? DBNull.Value);
        command.Parameters.AddWithValue("no_experience_allowed", offer.Experience.NoExperienceAllowed);
        command.Parameters.AddWithValue("experience_required", (object?)offer.Experience.IsRequired ?? DBNull.Value);
        command.Parameters.AddWithValue("experience_confidence", offer.Experience.Confidence);
        command.Parameters.AddWithValue("experience_evidence", (object?)offer.Experience.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("education_required", (object?)offer.Education.IsRequired ?? DBNull.Value);
        command.Parameters.AddWithValue("education_field", (object?)offer.Education.Field ?? DBNull.Value);
        command.Parameters.AddWithValue("education_confidence", offer.Education.Confidence);
        command.Parameters.AddWithValue("education_evidence", (object?)offer.Education.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("is_remote", offer.IsRemote);
        command.Parameters.AddWithValue("latitude", (object?)offer.Latitude ?? DBNull.Value);
        command.Parameters.AddWithValue("longitude", (object?)offer.Longitude ?? DBNull.Value);
        command.Parameters.AddWithValue("published_at", offer.PublishedAt is null ? DBNull.Value : offer.PublishedAt.Value.ToUniversalTime());
        command.Parameters.AddWithValue("last_import_run_id", context.ImportRunId);
        command.Parameters.AddWithValue("content_hash", offer.ContentHash);
        command.Parameters.AddWithValue("external_reference", (object?)offer.ExternalReference ?? DBNull.Value);
        command.Parameters.Add("raw_payload_json", NpgsqlDbType.Jsonb).Value = string.IsNullOrWhiteSpace(offer.RawPayloadJson) ? DBNull.Value : offer.RawPayloadJson;

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetBoolean(1));
    }

    private static async Task UpdateDerivedOfferFieldsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, NormalizedJobOffer offer)
    {
        await using var command = new NpgsqlCommand("""
            update public.job_offers
            set employment_type_raw = @employment_type_raw,
                contract_type_raw = @contract_type_raw,
                work_mode = @work_mode,
                remote_scope = @remote_scope,
                remote_country_restriction = @remote_country_restriction,
                office_days_per_week_min = @office_days_per_week_min,
                office_days_per_week_max = @office_days_per_week_max,
                work_mode_confidence = @work_mode_confidence,
                work_mode_evidence = @work_mode_evidence,
                work_time_type = @work_time_type,
                fte_min = @fte_min,
                fte_max = @fte_max,
                hours_per_week_min = @hours_per_week_min,
                hours_per_week_max = @hours_per_week_max,
                work_time_confidence = @work_time_confidence,
                work_time_evidence = @work_time_evidence,
                salary_period = @salary_period,
                salary_tax_type = @salary_tax_type,
                salary_rate_type = @salary_rate_type,
                salary_confidence = @salary_confidence,
                salary_evidence = @salary_evidence,
                data_quality_score = @data_quality_score,
                extraction_score = @extraction_score,
                experience_level = @experience_level,
                experience_min_years = @experience_min_years,
                experience_max_years = @experience_max_years,
                no_experience_allowed = @no_experience_allowed,
                experience_required = @experience_required,
                experience_confidence = @experience_confidence,
                experience_evidence = @experience_evidence,
                education_level = @education_level,
                education_required = @education_required,
                education_field = @education_field,
                education_confidence = @education_confidence,
                education_evidence = @education_evidence,
                updated_at = now()
            where id = @job_offer_id
            """, connection, transaction);
        command.Parameters.AddWithValue("job_offer_id", jobOfferId);
        command.Parameters.AddWithValue("employment_type_raw", (object?)offer.EmploymentTypeRaw ?? DBNull.Value);
        command.Parameters.AddWithValue("contract_type_raw", (object?)offer.ContractTypeRaw ?? DBNull.Value);
        command.Parameters.AddWithValue("work_mode", offer.WorkMode.WorkMode);
        command.Parameters.AddWithValue("remote_scope", offer.WorkMode.RemoteScope);
        command.Parameters.AddWithValue("remote_country_restriction", (object?)offer.WorkMode.RemoteCountryRestriction ?? DBNull.Value);
        command.Parameters.AddWithValue("office_days_per_week_min", (object?)offer.WorkMode.OfficeDaysPerWeekMin ?? DBNull.Value);
        command.Parameters.AddWithValue("office_days_per_week_max", (object?)offer.WorkMode.OfficeDaysPerWeekMax ?? DBNull.Value);
        command.Parameters.AddWithValue("work_mode_confidence", offer.WorkMode.Confidence);
        command.Parameters.AddWithValue("work_mode_evidence", (object?)offer.WorkMode.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("work_time_type", offer.WorkTime.WorkTimeType);
        command.Parameters.AddWithValue("fte_min", (object?)offer.WorkTime.FteMin ?? DBNull.Value);
        command.Parameters.AddWithValue("fte_max", (object?)offer.WorkTime.FteMax ?? DBNull.Value);
        command.Parameters.AddWithValue("hours_per_week_min", (object?)offer.WorkTime.HoursPerWeekMin ?? DBNull.Value);
        command.Parameters.AddWithValue("hours_per_week_max", (object?)offer.WorkTime.HoursPerWeekMax ?? DBNull.Value);
        command.Parameters.AddWithValue("work_time_confidence", offer.WorkTime.Confidence);
        command.Parameters.AddWithValue("work_time_evidence", (object?)offer.WorkTime.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("salary_period", offer.SalaryInfo.SalaryPeriod);
        command.Parameters.AddWithValue("salary_tax_type", offer.SalaryInfo.SalaryTaxType);
        command.Parameters.AddWithValue("salary_rate_type", offer.SalaryInfo.SalaryRateType);
        command.Parameters.AddWithValue("salary_confidence", offer.SalaryInfo.Confidence);
        command.Parameters.AddWithValue("salary_evidence", (object?)offer.SalaryInfo.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("data_quality_score", offer.DataQualityScore);
        command.Parameters.AddWithValue("extraction_score", offer.ExtractionScore);
        command.Parameters.AddWithValue("experience_level", offer.Experience.Level);
        command.Parameters.AddWithValue("experience_min_years", (object?)offer.Experience.MinYears ?? DBNull.Value);
        command.Parameters.AddWithValue("experience_max_years", (object?)offer.Experience.MaxYears ?? DBNull.Value);
        command.Parameters.AddWithValue("no_experience_allowed", offer.Experience.NoExperienceAllowed);
        command.Parameters.AddWithValue("experience_required", (object?)offer.Experience.IsRequired ?? DBNull.Value);
        command.Parameters.AddWithValue("experience_confidence", offer.Experience.Confidence);
        command.Parameters.AddWithValue("experience_evidence", (object?)offer.Experience.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("education_level", offer.Education.Level);
        command.Parameters.AddWithValue("education_required", (object?)offer.Education.IsRequired ?? DBNull.Value);
        command.Parameters.AddWithValue("education_field", (object?)offer.Education.Field ?? DBNull.Value);
        command.Parameters.AddWithValue("education_confidence", offer.Education.Confidence);
        command.Parameters.AddWithValue("education_evidence", (object?)offer.Education.Evidence ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ReplaceLanguagesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<OfferLanguage> languages)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_languages where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var language in languages)
        {
            await using var insertCommand = new NpgsqlCommand(
                """
                insert into public.job_offer_languages (
                    job_offer_id, language_code, language_name, level_min, is_required,
                    confidence, evidence, source_field, source_section, extractor_version
                )
                values (
                    @job_offer_id, @code, @name, @level_min, @is_required,
                    @confidence, @evidence, @source_field, @source_section, @extractor_version
                )
                on conflict (job_offer_id, language_code) do update set
                    language_name = excluded.language_name,
                    level_min = excluded.level_min,
                    is_required = excluded.is_required,
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section,
                    extractor_version = excluded.extractor_version
                """,
                connection,
                transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("code", language.Code);
            insertCommand.Parameters.AddWithValue("name", language.Name);
            insertCommand.Parameters.AddWithValue("level_min", language.LevelMin);
            insertCommand.Parameters.AddWithValue("is_required", (object?)language.IsRequired ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("confidence", language.Confidence);
            insertCommand.Parameters.AddWithValue("evidence", (object?)language.Evidence ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_field", (object?)language.SourceField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_section", (object?)language.SourceSection ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("extractor_version", language.ExtractorVersion);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceTagsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<string> tags)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_tags where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await using var insertCommand = new NpgsqlCommand(
                "insert into public.job_offer_tags (job_offer_id, tag) values (@job_offer_id, @tag) on conflict do nothing",
                connection,
                transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("tag", tag);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceContractTypesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<OfferContractType> contractTypes)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_contract_types where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var contract in contractTypes.Where(x => !string.IsNullOrWhiteSpace(x.ContractType)))
        {
            await using var insertCommand = new NpgsqlCommand("""
                insert into public.job_offer_contract_types (job_offer_id, contract_type, is_primary, confidence, evidence, source_field, source_section)
                values (@job_offer_id, @contract_type, @is_primary, @confidence, @evidence, @source_field, @source_section)
                on conflict (job_offer_id, contract_type) do update set
                    is_primary = excluded.is_primary,
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section
                """, connection, transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("contract_type", contract.ContractType);
            insertCommand.Parameters.AddWithValue("is_primary", contract.IsPrimary);
            insertCommand.Parameters.AddWithValue("confidence", contract.Confidence);
            insertCommand.Parameters.AddWithValue("evidence", (object?)contract.Evidence ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_field", (object?)contract.SourceField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_section", (object?)contract.SourceSection ?? DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceScheduleFlagsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<OfferScheduleFlag> flags)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_schedule_flags where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var flag in flags.Where(x => !string.IsNullOrWhiteSpace(x.ScheduleFlag)))
        {
            await using var insertCommand = new NpgsqlCommand("""
                insert into public.job_offer_schedule_flags (job_offer_id, schedule_flag, confidence, evidence, source_field, source_section)
                values (@job_offer_id, @schedule_flag, @confidence, @evidence, @source_field, @source_section)
                on conflict (job_offer_id, schedule_flag) do update set
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section
                """, connection, transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("schedule_flag", flag.ScheduleFlag);
            insertCommand.Parameters.AddWithValue("confidence", flag.Confidence);
            insertCommand.Parameters.AddWithValue("evidence", (object?)flag.Evidence ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_field", (object?)flag.SourceField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_section", (object?)flag.SourceSection ?? DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceBenefitsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<OfferBenefit> benefits)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_benefits where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var benefit in benefits.Where(x => !string.IsNullOrWhiteSpace(x.BenefitCode)))
        {
            await using var insertCommand = new NpgsqlCommand("""
                insert into public.job_offer_benefits (job_offer_id, benefit_code, benefit_name, confidence, evidence, source_field, source_section)
                values (@job_offer_id, @benefit_code, @benefit_name, @confidence, @evidence, @source_field, @source_section)
                on conflict (job_offer_id, benefit_code) do update set
                    benefit_name = excluded.benefit_name,
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section
                """, connection, transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("benefit_code", benefit.BenefitCode);
            insertCommand.Parameters.AddWithValue("benefit_name", benefit.BenefitName);
            insertCommand.Parameters.AddWithValue("confidence", benefit.Confidence);
            insertCommand.Parameters.AddWithValue("evidence", (object?)benefit.Evidence ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_field", (object?)benefit.SourceField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_section", (object?)benefit.SourceSection ?? DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceDomainsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<OfferDomain> domains)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_domains where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var domain in domains.Where(x => !string.IsNullOrWhiteSpace(x.DomainCode)))
        {
            await using var insertCommand = new NpgsqlCommand("""
                insert into public.job_offer_domains (job_offer_id, domain_code, domain_name, confidence, evidence, source_field, source_section, extractor_version)
                values (@job_offer_id, @domain_code, @domain_name, @confidence, @evidence, @source_field, @source_section, @extractor_version)
                on conflict (job_offer_id, domain_code) do update set
                    domain_name = excluded.domain_name,
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section,
                    extractor_version = excluded.extractor_version
                """, connection, transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("domain_code", domain.DomainCode);
            insertCommand.Parameters.AddWithValue("domain_name", domain.DomainName);
            insertCommand.Parameters.AddWithValue("confidence", domain.Confidence);
            insertCommand.Parameters.AddWithValue("evidence", (object?)domain.Evidence ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_field", (object?)domain.SourceField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_section", (object?)domain.SourceSection ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("extractor_version", domain.ExtractorVersion);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceFormalRequirementsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, List<OfferFormalRequirement> requirements)
    {
        await using (var deleteCommand = new NpgsqlCommand("delete from public.job_offer_formal_requirements where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var requirement in requirements.Where(x => !string.IsNullOrWhiteSpace(x.RequirementCode)))
        {
            await using var insertCommand = new NpgsqlCommand("""
                insert into public.job_offer_formal_requirements (job_offer_id, requirement_code, is_required, confidence, evidence, source_field, source_section, extractor_version)
                values (@job_offer_id, @requirement_code, @is_required, @confidence, @evidence, @source_field, @source_section, @extractor_version)
                on conflict (job_offer_id, requirement_code) do update set
                    is_required = excluded.is_required,
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section,
                    extractor_version = excluded.extractor_version
                """, connection, transaction);
            insertCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCommand.Parameters.AddWithValue("requirement_code", requirement.RequirementCode);
            insertCommand.Parameters.AddWithValue("is_required", requirement.IsRequired);
            insertCommand.Parameters.AddWithValue("confidence", requirement.Confidence);
            insertCommand.Parameters.AddWithValue("evidence", (object?)requirement.Evidence ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_field", (object?)requirement.SourceField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("source_section", (object?)requirement.SourceSection ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("extractor_version", requirement.ExtractorVersion);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ReplaceClassificationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long jobOfferId, OfferClassification classification)
    {
        await using (var deleteRolesCommand = new NpgsqlCommand("delete from public.job_offer_roles where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteRolesCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteRolesCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteCriteriaCommand = new NpgsqlCommand("delete from public.job_offer_criteria where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteCriteriaCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteCriteriaCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteHitsCommand = new NpgsqlCommand("delete from public.job_offer_criterion_hits where job_offer_id = @job_offer_id", connection, transaction))
        {
            deleteHitsCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            await deleteHitsCommand.ExecuteNonQueryAsync();
        }

        var categoryId = await UpsertCategoryAsync(connection, transaction, classification.CategoryCode, classification.CategoryName);
        if (!string.IsNullOrWhiteSpace(classification.RoleCode) && !string.IsNullOrWhiteSpace(classification.RoleName))
        {
            var roleId = await UpsertRoleAsync(connection, transaction, categoryId, classification.RoleCode, classification.RoleName);
            await using var insertRoleCommand = new NpgsqlCommand("""
                insert into public.job_offer_roles (job_offer_id, role_id, confidence, evidence)
                values (@job_offer_id, @role_id, @confidence, @evidence)
                on conflict (job_offer_id, role_id)
                do update set
                    confidence = excluded.confidence,
                    evidence = excluded.evidence
                """, connection, transaction);
            insertRoleCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertRoleCommand.Parameters.AddWithValue("role_id", roleId);
            insertRoleCommand.Parameters.AddWithValue("confidence", classification.RoleConfidence);
            insertRoleCommand.Parameters.AddWithValue("evidence", (object?)classification.RoleEvidence ?? DBNull.Value);
            await insertRoleCommand.ExecuteNonQueryAsync();
        }

        foreach (var criterion in classification.Criteria.Where(x => !string.IsNullOrWhiteSpace(x.Kind) && !string.IsNullOrWhiteSpace(x.Code)))
        {
            var criterionId = await UpsertCriterionAsync(connection, transaction, criterion);
            await using var insertCriterionCommand = new NpgsqlCommand("""
                insert into public.job_offer_criteria (
                    job_offer_id, criterion_id, is_required, requirement_level, confidence, evidence,
                    source_field, source_section, evidence_start, evidence_end, matched_alias, extractor_version
                )
                values (
                    @job_offer_id, @criterion_id, @is_required, @requirement_level, @confidence, @evidence,
                    @source_field, @source_section, @evidence_start, @evidence_end, @matched_alias, @extractor_version
                )
                on conflict (job_offer_id, criterion_id)
                do update set
                    is_required = excluded.is_required,
                    requirement_level = excluded.requirement_level,
                    confidence = excluded.confidence,
                    evidence = excluded.evidence,
                    source_field = excluded.source_field,
                    source_section = excluded.source_section,
                    evidence_start = excluded.evidence_start,
                    evidence_end = excluded.evidence_end,
                    matched_alias = excluded.matched_alias,
                    extractor_version = excluded.extractor_version
                """, connection, transaction);
            insertCriterionCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertCriterionCommand.Parameters.AddWithValue("criterion_id", criterionId);
            insertCriterionCommand.Parameters.AddWithValue("is_required", criterion.IsRequired);
            insertCriterionCommand.Parameters.AddWithValue("requirement_level", criterion.RequirementLevel);
            insertCriterionCommand.Parameters.AddWithValue("confidence", criterion.Confidence);
            insertCriterionCommand.Parameters.AddWithValue("evidence", (object?)criterion.Evidence ?? DBNull.Value);
            insertCriterionCommand.Parameters.AddWithValue("source_field", (object?)criterion.SourceField ?? DBNull.Value);
            insertCriterionCommand.Parameters.AddWithValue("source_section", (object?)criterion.SourceSection ?? DBNull.Value);
            insertCriterionCommand.Parameters.AddWithValue("evidence_start", (object?)criterion.EvidenceStart ?? DBNull.Value);
            insertCriterionCommand.Parameters.AddWithValue("evidence_end", (object?)criterion.EvidenceEnd ?? DBNull.Value);
            insertCriterionCommand.Parameters.AddWithValue("matched_alias", (object?)criterion.MatchedAlias ?? DBNull.Value);
            insertCriterionCommand.Parameters.AddWithValue("extractor_version", criterion.ExtractorVersion);
            await insertCriterionCommand.ExecuteNonQueryAsync();
        }

        foreach (var hit in classification.CriterionHits.Where(x => !string.IsNullOrWhiteSpace(x.Kind) && !string.IsNullOrWhiteSpace(x.Code)))
        {
            var criterionId = await UpsertCriterionAsync(connection, transaction, hit);
            await using var insertHitCommand = new NpgsqlCommand("""
                insert into public.job_offer_criterion_hits (
                    job_offer_id, criterion_id, source_field, source_section, matched_alias,
                    requirement_level, confidence, evidence, evidence_start, evidence_end, extractor_version
                )
                values (
                    @job_offer_id, @criterion_id, @source_field, @source_section, @matched_alias,
                    @requirement_level, @confidence, @evidence, @evidence_start, @evidence_end, @extractor_version
                )
                """, connection, transaction);
            insertHitCommand.Parameters.AddWithValue("job_offer_id", jobOfferId);
            insertHitCommand.Parameters.AddWithValue("criterion_id", criterionId);
            insertHitCommand.Parameters.AddWithValue("source_field", (object?)hit.SourceField ?? "unknown");
            insertHitCommand.Parameters.AddWithValue("source_section", (object?)hit.SourceSection ?? DBNull.Value);
            insertHitCommand.Parameters.AddWithValue("matched_alias", (object?)hit.MatchedAlias ?? hit.Code);
            insertHitCommand.Parameters.AddWithValue("requirement_level", hit.RequirementLevel);
            insertHitCommand.Parameters.AddWithValue("confidence", hit.Confidence);
            insertHitCommand.Parameters.AddWithValue("evidence", (object?)hit.Evidence ?? DBNull.Value);
            insertHitCommand.Parameters.AddWithValue("evidence_start", (object?)hit.EvidenceStart ?? DBNull.Value);
            insertHitCommand.Parameters.AddWithValue("evidence_end", (object?)hit.EvidenceEnd ?? DBNull.Value);
            insertHitCommand.Parameters.AddWithValue("extractor_version", hit.ExtractorVersion);
            await insertHitCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureClassificationSchemaAsync(NpgsqlConnection connection)
    {
        await using (var command = new NpgsqlCommand("""
            alter table public.job_offers
                add column if not exists description_quality varchar(30) not null default 'full',
                add column if not exists experience_min_years numeric(4,1),
                add column if not exists experience_max_years numeric(4,1),
                add column if not exists no_experience_allowed boolean not null default false,
                add column if not exists experience_required boolean,
                add column if not exists experience_confidence numeric(5,4) not null default 0.5000,
                add column if not exists experience_evidence text,
                add column if not exists education_required boolean,
                add column if not exists education_field varchar(120),
                add column if not exists education_confidence numeric(5,4) not null default 0.5000,
                add column if not exists education_evidence text,
                add column if not exists employment_type_raw varchar(200),
                add column if not exists contract_type_raw varchar(200),
                add column if not exists work_mode varchar(50),
                add column if not exists remote_scope varchar(80),
                add column if not exists remote_country_restriction varchar(80),
                add column if not exists office_days_per_week_min numeric(3,1),
                add column if not exists office_days_per_week_max numeric(3,1),
                add column if not exists work_mode_confidence numeric(5,4) not null default 0.5000,
                add column if not exists work_mode_evidence text,
                add column if not exists work_time_type varchar(50),
                add column if not exists fte_min numeric(3,2),
                add column if not exists fte_max numeric(3,2),
                add column if not exists hours_per_week_min numeric(5,2),
                add column if not exists hours_per_week_max numeric(5,2),
                add column if not exists work_time_confidence numeric(5,4) not null default 0.5000,
                add column if not exists work_time_evidence text,
                add column if not exists salary_period varchar(30),
                add column if not exists salary_tax_type varchar(30),
                add column if not exists salary_rate_type varchar(30),
                add column if not exists salary_confidence numeric(5,4) not null default 0.5000,
                add column if not exists salary_evidence text,
                add column if not exists data_quality_score numeric(5,2) not null default 0,
                add column if not exists extraction_score numeric(5,2) not null default 0
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            "create index if not exists ix_job_offers_experience_min_years on public.job_offers (experience_min_years)",
            "create index if not exists ix_job_offers_experience_max_years on public.job_offers (experience_max_years)",
            "create index if not exists ix_job_offers_no_experience_allowed on public.job_offers (no_experience_allowed)",
            "create index if not exists ix_job_offers_experience_required on public.job_offers (experience_required)",
            "create index if not exists ix_job_offers_education_required on public.job_offers (education_required)",
            "create index if not exists ix_job_offers_education_field on public.job_offers (education_field)",
            "create index if not exists ix_job_offers_work_mode on public.job_offers (work_mode)",
            "create index if not exists ix_job_offers_remote_scope on public.job_offers (remote_scope)",
            "create index if not exists ix_job_offers_work_time_type on public.job_offers (work_time_type)",
            "create index if not exists ix_job_offers_salary_period on public.job_offers (salary_period)",
            "create index if not exists ix_job_offers_salary_tax_type on public.job_offers (salary_tax_type)",
            "create index if not exists ix_job_offers_data_quality_score on public.job_offers (data_quality_score)",
            "create index if not exists ix_job_offers_extraction_score on public.job_offers (extraction_score)"
        })
        {
            await using var indexCommand = new NpgsqlCommand(sql, connection);
            await indexCommand.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            """
            alter table public.job_offer_languages
                add column if not exists level_min varchar(20) not null default 'unknown',
                add column if not exists is_required boolean,
                add column if not exists confidence numeric(5,4) not null default 0.5000,
                add column if not exists evidence text,
                add column if not exists source_field varchar(80),
                add column if not exists source_section varchar(80),
                add column if not exists extractor_version varchar(50) not null default 'rules_v3'
            """
        })
        {
            await using var alterCommand = new NpgsqlCommand(sql, connection);
            await alterCommand.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            """
            create table if not exists public.job_offer_contract_types (
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                contract_type varchar(50) not null,
                is_primary boolean not null default false,
                confidence numeric(5,4) not null default 0.5000,
                evidence text,
                source_field varchar(80),
                source_section varchar(80),
                created_at timestamptz not null default now(),
                primary key (job_offer_id, contract_type)
            )
            """,
            """
            create table if not exists public.job_offer_schedule_flags (
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                schedule_flag varchar(60) not null,
                confidence numeric(5,4) not null default 0.5000,
                evidence text,
                source_field varchar(80),
                source_section varchar(80),
                created_at timestamptz not null default now(),
                primary key (job_offer_id, schedule_flag)
            )
            """,
            """
            create table if not exists public.job_offer_benefits (
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                benefit_code varchar(80) not null,
                benefit_name varchar(160) not null,
                confidence numeric(5,4) not null default 0.5000,
                evidence text,
                source_field varchar(80),
                source_section varchar(80),
                created_at timestamptz not null default now(),
                primary key (job_offer_id, benefit_code)
            )
            """,
            """
            create table if not exists public.job_offer_domains (
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                domain_code varchar(80) not null,
                domain_name varchar(160) not null,
                confidence numeric(5,4) not null default 0.5000,
                evidence text,
                source_field varchar(80),
                source_section varchar(80),
                extractor_version varchar(50) not null default 'rules_v3',
                created_at timestamptz not null default now(),
                primary key (job_offer_id, domain_code)
            )
            """,
            """
            create table if not exists public.job_offer_formal_requirements (
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                requirement_code varchar(100) not null,
                is_required boolean not null default true,
                confidence numeric(5,4) not null default 0.5000,
                evidence text,
                source_field varchar(80),
                source_section varchar(80),
                extractor_version varchar(50) not null default 'rules_v3',
                created_at timestamptz not null default now(),
                primary key (job_offer_id, requirement_code)
            )
            """
        })
        {
            await using var tableCommand = new NpgsqlCommand(sql, connection);
            await tableCommand.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            """
            alter table public.job_offer_domains
                add column if not exists source_field varchar(80),
                add column if not exists source_section varchar(80),
                add column if not exists extractor_version varchar(50) not null default 'rules_v3'
            """,
            """
            alter table public.job_offer_formal_requirements
                add column if not exists source_field varchar(80),
                add column if not exists source_section varchar(80),
                add column if not exists extractor_version varchar(50) not null default 'rules_v3'
            """
        })
        {
            await using var alterCommand = new NpgsqlCommand(sql, connection);
            await alterCommand.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            "create index if not exists ix_job_offer_contract_types_type on public.job_offer_contract_types(contract_type)",
            "create index if not exists ix_job_offer_schedule_flags_flag on public.job_offer_schedule_flags(schedule_flag)",
            "create index if not exists ix_job_offer_benefits_code on public.job_offer_benefits(benefit_code)",
            "create index if not exists ix_job_offer_domains_code on public.job_offer_domains(domain_code)",
            "create index if not exists ix_job_offer_formal_requirements_code on public.job_offer_formal_requirements(requirement_code)"
        })
        {
            await using var indexCommand = new NpgsqlCommand(sql, connection);
            await indexCommand.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            alter table public.job_offer_criteria
                add column if not exists requirement_level varchar(20) not null default 'unknown',
                add column if not exists source_field varchar(80),
                add column if not exists source_section varchar(80),
                add column if not exists evidence_start integer,
                add column if not exists evidence_end integer,
                add column if not exists matched_alias varchar(180),
                add column if not exists extractor_version varchar(50) not null default 'rules_v1'
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            update public.job_offer_criteria
            set requirement_level = case when is_required then 'required' else 'unknown' end
            where requirement_level = 'unknown'
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create table if not exists public.job_offer_criterion_hits (
                id bigint generated always as identity primary key,
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                criterion_id integer not null references public.job_criteria(id) on delete cascade,
                source_field varchar(80) not null,
                source_section varchar(80),
                matched_alias varchar(180) not null,
                requirement_level varchar(20) not null,
                confidence numeric(5,4) not null default 0.5000,
                evidence text,
                evidence_start integer,
                evidence_end integer,
                extractor_version varchar(50) not null default 'rules_v1',
                created_at timestamptz not null default now()
            )
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create index if not exists ix_job_offer_criterion_hits_offer
                on public.job_offer_criterion_hits(job_offer_id)
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create index if not exists ix_job_offer_criterion_hits_criterion
                on public.job_offer_criterion_hits(criterion_id)
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create table if not exists public.job_offer_match_scores (
                id bigint generated always as identity primary key,
                job_offer_id bigint not null references public.job_offers(id) on delete cascade,
                user_id bigint null,
                total_score numeric(6,2) not null default 0,
                salary_score numeric(6,2) not null default 0,
                location_score numeric(6,2) not null default 0,
                skills_score numeric(6,2) not null default 0,
                experience_score numeric(6,2) not null default 0,
                education_score numeric(6,2) not null default 0,
                language_score numeric(6,2) not null default 0,
                contract_score numeric(6,2) not null default 0,
                work_mode_score numeric(6,2) not null default 0,
                work_time_score numeric(6,2) not null default 0,
                benefits_score numeric(6,2) not null default 0,
                schedule_score numeric(6,2) not null default 0,
                missing_required_skills_count integer not null default 0,
                matched_required_skills_count integer not null default 0,
                matched_optional_skills_count integer not null default 0,
                explanation text,
                calculated_at timestamptz not null default now(),
                constraint uq_job_offer_match_scores_offer_user unique (job_offer_id, user_id)
            )
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create index if not exists ix_job_offer_match_scores_total
                on public.job_offer_match_scores(total_score desc)
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await EnsureCriterionAliasesAsync(connection);
        await LoadCriterionAliasesAsync(connection);
        await EnsureSearchViewsAsync(connection);
    }

    private static async Task EnsureCriterionAliasesAsync(NpgsqlConnection connection)
    {
        await using (var command = new NpgsqlCommand("""
            create table if not exists public.criterion_aliases (
                id bigint generated always as identity primary key,
                criterion_id integer not null references public.job_criteria(id) on delete cascade,
                alias varchar(180) not null,
                normalized_alias varchar(180) not null,
                is_short_ambiguous boolean not null default false,
                requires_tech_context boolean not null default false,
                requires_whole_token boolean not null default true,
                priority integer not null default 100,
                is_active boolean not null default true,
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now(),
                constraint uq_criterion_aliases_criterion_alias unique (criterion_id, normalized_alias)
            )
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        foreach (var seed in JobClassificationRules.GetBootstrapCriterionAliases())
        {
            await using var command = new NpgsqlCommand("""
                with criterion as (
                    insert into public.job_criteria (kind, code, display_name)
                    values (@kind, @code, @display_name)
                    on conflict (kind, code)
                    do update set display_name = excluded.display_name, updated_at = now()
                    returning id
                )
                insert into public.criterion_aliases (
                    criterion_id, alias, normalized_alias, is_short_ambiguous,
                    requires_tech_context, requires_whole_token, priority, is_active
                )
                select
                    criterion.id, @alias, lower(@alias), @is_short_ambiguous,
                    @requires_tech_context, @requires_whole_token, @priority, @is_active
                from criterion
                on conflict (criterion_id, normalized_alias)
                do update set
                    alias = excluded.alias,
                    is_short_ambiguous = excluded.is_short_ambiguous,
                    requires_tech_context = excluded.requires_tech_context,
                    requires_whole_token = excluded.requires_whole_token,
                    priority = least(public.criterion_aliases.priority, excluded.priority),
                    is_active = public.criterion_aliases.is_active,
                    updated_at = now()
                """, connection);
            command.Parameters.AddWithValue("kind", seed.Kind);
            command.Parameters.AddWithValue("code", seed.Code);
            command.Parameters.AddWithValue("display_name", seed.DisplayName);
            command.Parameters.AddWithValue("alias", seed.Alias);
            command.Parameters.AddWithValue("is_short_ambiguous", seed.IsShortAmbiguous);
            command.Parameters.AddWithValue("requires_tech_context", seed.RequiresTechContext);
            command.Parameters.AddWithValue("requires_whole_token", seed.RequiresWholeToken);
            command.Parameters.AddWithValue("priority", seed.Priority);
            command.Parameters.AddWithValue("is_active", seed.IsActive);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            "create index if not exists ix_criterion_aliases_alias on public.criterion_aliases(normalized_alias) where is_active = true",
            "create index if not exists ix_criterion_aliases_criterion on public.criterion_aliases(criterion_id) where is_active = true"
        })
        {
            await using var indexCommand = new NpgsqlCommand(sql, connection);
            await indexCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task LoadCriterionAliasesAsync(NpgsqlConnection connection)
    {
        var aliases = new List<CriterionAliasSeed>();
        await using var command = new NpgsqlCommand("""
            select
                jc.kind,
                jc.code,
                jc.display_name,
                ca.alias,
                ca.is_short_ambiguous,
                ca.requires_tech_context,
                ca.requires_whole_token,
                ca.priority,
                ca.is_active
            from public.criterion_aliases ca
            join public.job_criteria jc on jc.id = ca.criterion_id
            where ca.is_active = true
            order by jc.kind, jc.code, ca.priority, ca.alias
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            aliases.Add(new CriterionAliasSeed
            {
                Kind = reader.GetString(0),
                Code = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Alias = reader.GetString(3),
                IsShortAmbiguous = reader.GetBoolean(4),
                RequiresTechContext = reader.GetBoolean(5),
                RequiresWholeToken = reader.GetBoolean(6),
                Priority = reader.GetInt32(7),
                IsActive = reader.GetBoolean(8)
            });
        }

        JobClassificationRules.ConfigureCriterionAliases(aliases);
    }

    private static async Task EnsureSearchViewsAsync(NpgsqlConnection connection)
    {
        foreach (var sql in new[]
        {
            "create index if not exists ix_job_offers_active_city on public.job_offers(city) where is_active = true",
            "create index if not exists ix_job_offers_active_work_mode on public.job_offers(work_mode, remote_scope) where is_active = true",
            "create index if not exists ix_job_offers_active_salary on public.job_offers(salary_min, salary_max) where is_active = true",
            "create index if not exists ix_job_offers_active_experience on public.job_offers(experience_level, experience_min_years) where is_active = true",
            "create index if not exists ix_job_offers_active_education on public.job_offers(education_level, education_field) where is_active = true",
            "create index if not exists ix_job_offers_active_published on public.job_offers(published_at desc) where is_active = true"
        })
        {
            await using var indexCommand = new NpgsqlCommand(sql, connection);
            await indexCommand.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create or replace view public.job_offer_search_view as
            select
                jo.id,
                jo.source_id,
                js.code as source_code,
                js.display_name as source_name,
                jo.external_id,
                jo.external_url,
                jo.title,
                jo.company_name,
                jo.company_logo_url,
                jo.location_name,
                jo.city,
                jo.region,
                jo.country_code,
                jo.salary_min,
                jo.salary_max,
                jo.salary_currency,
                jo.salary_raw,
                jo.salary_period,
                jo.salary_tax_type,
                jo.salary_rate_type,
                jo.salary_confidence,
                jo.employment_type,
                jo.contract_type,
                jo.employment_type_raw,
                jo.contract_type_raw,
                jo.work_mode,
                jo.remote_scope,
                jo.remote_country_restriction,
                jo.office_days_per_week_min,
                jo.office_days_per_week_max,
                jo.work_mode_confidence,
                jo.work_time_type,
                jo.fte_min,
                jo.fte_max,
                jo.hours_per_week_min,
                jo.hours_per_week_max,
                jo.work_time_confidence,
                jo.experience_level,
                jo.experience_min_years,
                jo.experience_max_years,
                jo.no_experience_allowed,
                jo.experience_required,
                jo.experience_confidence,
                jo.education_level,
                jo.education_required,
                jo.education_field,
                jo.education_confidence,
                jo.description_quality,
                jo.data_quality_score,
                jo.extraction_score,
                jo.is_remote,
                jo.published_at,
                jo.created_at,
                jo.last_seen_at
            from public.job_offers jo
            join public.job_sources js on js.id = jo.source_id
            where jo.is_active = true
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand("""
            create or replace view public.job_offer_filter_values_view as
            select filter_kind, filter_code, filter_name, count(distinct job_offer_id)::bigint as active_offer_count
            from (
                select 'work_mode' as filter_kind, coalesce(work_mode, 'unknown') as filter_code, coalesce(work_mode, 'unknown') as filter_name, id as job_offer_id
                from public.job_offers where is_active = true and coalesce(work_mode, 'unknown') <> 'unknown'
                union all
                select 'work_time', coalesce(work_time_type, 'unknown'), coalesce(work_time_type, 'unknown'), id
                from public.job_offers where is_active = true and coalesce(work_time_type, 'unknown') <> 'unknown'
                union all
                select 'experience', coalesce(experience_level, 'Wszystkie'), coalesce(experience_level, 'Wszystkie'), id
                from public.job_offers where is_active = true and coalesce(experience_level, 'Wszystkie') <> 'Wszystkie'
                union all
                select 'education', coalesce(education_level, 'Brak wymagan lub niewymagane'), coalesce(education_level, 'Brak wymagan lub niewymagane'), id
                from public.job_offers where is_active = true and coalesce(education_level, 'Brak wymagan lub niewymagane') <> 'Brak wymagan lub niewymagane'
                union all
                select 'contract', contract_type, contract_type, job_offer_id
                from public.job_offer_contract_types
                union all
                select 'schedule', schedule_flag, schedule_flag, job_offer_id
                from public.job_offer_schedule_flags
                union all
                select 'benefit', benefit_code, benefit_name, job_offer_id
                from public.job_offer_benefits
                union all
                select 'language_required', language_code || ':' || level_min, language_name || ' ' || level_min, job_offer_id
                from public.job_offer_languages where is_required = true
                union all
                select 'language_optional', language_code || ':' || level_min, language_name || ' ' || level_min, job_offer_id
                from public.job_offer_languages where is_required = false
                union all
                select jc.kind, jc.code, jc.display_name, joc.job_offer_id
                from public.job_offer_criteria joc
                join public.job_criteria jc on jc.id = joc.criterion_id
                join public.job_offers jo on jo.id = joc.job_offer_id and jo.is_active = true
                where jc.is_user_selectable = true
            ) filter_values
            group by filter_kind, filter_code, filter_name
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int?> UpsertCategoryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? code, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        await using var command = new NpgsqlCommand("""
            insert into public.job_categories (code, display_name)
            values (@code, @display_name)
            on conflict (code)
            do update set display_name = excluded.display_name
            returning id
            """, connection, transaction);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("display_name", displayName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> UpsertRoleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int? categoryId, string code, string displayName)
    {
        await using var command = new NpgsqlCommand("""
            insert into public.job_roles (category_id, code, display_name)
            values (@category_id, @code, @display_name)
            on conflict (code)
            do update set
                category_id = coalesce(excluded.category_id, public.job_roles.category_id),
                display_name = excluded.display_name
            returning id
            """, connection, transaction);
        command.Parameters.AddWithValue("category_id", (object?)categoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("display_name", displayName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> UpsertCriterionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, OfferCriterion criterion)
    {
        await using var command = new NpgsqlCommand("""
            insert into public.job_criteria (kind, code, display_name)
            values (@kind, @code, @display_name)
            on conflict (kind, code)
            do update set display_name = excluded.display_name
            returning id
            """, connection, transaction);
        command.Parameters.AddWithValue("kind", criterion.Kind);
        command.Parameters.AddWithValue("code", criterion.Code);
        command.Parameters.AddWithValue("display_name", criterion.DisplayName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> DeactivateMissingOffersAsync(NpgsqlConnection connection, SourceImportContext context)
    {
        await using var command = new NpgsqlCommand(@"
            update public.job_offers
            set is_active = false,
                updated_at = now()
            where source_id = @source_id
              and is_active = true
              and coalesce(last_import_run_id, 0) <> @import_run_id",
            connection);
        command.Parameters.AddWithValue("source_id", context.SourceId);
        command.Parameters.AddWithValue("import_run_id", context.ImportRunId);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateSourceSuccessAsync(NpgsqlConnection connection, int sourceId)
    {
        await using var command = new NpgsqlCommand(
            "update public.job_sources set last_success_at = now(), updated_at = now() where id = @source_id",
            connection);
        command.Parameters.AddWithValue("source_id", sourceId);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ExistingOfferForClassification(
        long Id,
        string? Title,
        string? Description,
        string? LocationName,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryRaw,
        string? EmploymentType,
        string? ContractType,
        bool IsRemote,
        List<string> Tags);
}
