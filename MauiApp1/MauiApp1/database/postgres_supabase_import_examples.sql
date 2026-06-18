-- 1. Start importu dla źródła
insert into public.job_import_runs (source_id, status)
values (
    (select id from public.job_sources where code = 'jooble'),
    'running'
)
returning id;

-- 2. Upsert jednej oferty
insert into public.job_offers (
    source_id,
    external_id,
    external_url,
    title,
    company_name,
    location_name,
    city,
    region,
    country_code,
    description,
    description_short,
    salary_min,
    salary_max,
    salary_currency,
    salary_raw,
    employment_type,
    contract_type,
    experience_level,
    education_level,
    is_remote,
    is_active,
    published_at,
    last_seen_at,
    last_import_run_id,
    content_hash,
    raw_payload_json
)
values (
    :source_id,
    :external_id,
    :external_url,
    :title,
    :company_name,
    :location_name,
    :city,
    :region,
    :country_code,
    :description,
    :description_short,
    :salary_min,
    :salary_max,
    :salary_currency,
    :salary_raw,
    :employment_type,
    :contract_type,
    :experience_level,
    :education_level,
    :is_remote,
    true,
    :published_at,
    now(),
    :last_import_run_id,
    :content_hash,
    cast(:raw_payload_json as jsonb)
)
on conflict (source_id, external_id)
do update set
    external_url = excluded.external_url,
    title = excluded.title,
    company_name = excluded.company_name,
    location_name = excluded.location_name,
    city = excluded.city,
    region = excluded.region,
    country_code = excluded.country_code,
    description = excluded.description,
    description_short = excluded.description_short,
    salary_min = excluded.salary_min,
    salary_max = excluded.salary_max,
    salary_currency = excluded.salary_currency,
    salary_raw = excluded.salary_raw,
    employment_type = excluded.employment_type,
    contract_type = excluded.contract_type,
    experience_level = excluded.experience_level,
    education_level = excluded.education_level,
    is_remote = excluded.is_remote,
    is_active = true,
    published_at = excluded.published_at,
    last_seen_at = now(),
    last_import_run_id = excluded.last_import_run_id,
    content_hash = excluded.content_hash,
    raw_payload_json = excluded.raw_payload_json,
    updated_at = now();

-- 3. Przykład odświeżenia języków dla jednej oferty
delete from public.job_offer_languages
where job_offer_id = :job_offer_id;

insert into public.job_offer_languages (job_offer_id, language_code, language_name)
values
    (:job_offer_id, 'pl', 'Polski'),
    (:job_offer_id, 'en', 'Angielski')
on conflict do nothing;

-- 4. Przykład odświeżenia tagów dla jednej oferty
delete from public.job_offer_tags
where job_offer_id = :job_offer_id;

insert into public.job_offer_tags (job_offer_id, tag)
values
    (:job_offer_id, 'Pełny etat'),
    (:job_offer_id, 'Zdalna')
on conflict do nothing;

-- 5. Dezaktywacja ofert niewidzianych w bieżącym imporcie
update public.job_offers
set
    is_active = false,
    updated_at = now()
where source_id = :source_id
  and (last_import_run_id is null or last_import_run_id <> :current_import_run_id)
  and is_active = true;

-- 6. Zakończenie importu
update public.job_import_runs
set
    finished_at = now(),
    status = 'success',
    fetched_count = :fetched_count,
    inserted_count = :inserted_count,
    updated_count = :updated_count,
    deactivated_count = :deactivated_count
where id = :import_run_id;
