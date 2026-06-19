-- Roadmap Job Search AI schema extensions.
-- Safe to run repeatedly on PostgreSQL job_platform.

alter table public.job_offer_languages
    add column if not exists level_min varchar(20) not null default 'unknown',
    add column if not exists is_required boolean,
    add column if not exists confidence numeric(5,4) not null default 0.5000,
    add column if not exists evidence text,
    add column if not exists source_field varchar(80),
    add column if not exists source_section varchar(80),
    add column if not exists extractor_version varchar(50) not null default 'rules_v3';

alter table public.job_offer_domains
    add column if not exists source_field varchar(80),
    add column if not exists source_section varchar(80),
    add column if not exists extractor_version varchar(50) not null default 'rules_v3';

alter table public.job_offer_formal_requirements
    add column if not exists source_field varchar(80),
    add column if not exists source_section varchar(80),
    add column if not exists extractor_version varchar(50) not null default 'rules_v3';

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
);

create index if not exists ix_criterion_aliases_alias
    on public.criterion_aliases(normalized_alias)
    where is_active = true;

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
    calculated_at timestamptz not null default now()
);

create index if not exists ix_job_offer_match_scores_total
    on public.job_offer_match_scores(total_score desc);

alter table public.user_profiles
    add column if not exists expected_salary_min numeric(12,2),
    add column if not exists salary_currency varchar(10) not null default 'PLN',
    add column if not exists salary_tax_type varchar(30) not null default 'unknown',
    add column if not exists work_mode_preference varchar(50) not null default 'any',
    add column if not exists max_office_days_per_week numeric(3,1),
    add column if not exists work_time_type_preference varchar(50) not null default 'any',
    add column if not exists max_distance_km integer;

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

create index if not exists ix_job_offers_active_city
    on public.job_offers(city)
    where is_active = true;

create index if not exists ix_job_offers_active_work_mode
    on public.job_offers(work_mode, remote_scope)
    where is_active = true;

create index if not exists ix_job_offers_active_salary
    on public.job_offers(salary_min, salary_max)
    where is_active = true;

create index if not exists ix_job_offers_active_experience
    on public.job_offers(experience_level, experience_min_years)
    where is_active = true;

create index if not exists ix_job_offers_active_education
    on public.job_offers(education_level, education_field)
    where is_active = true;

create index if not exists ix_job_offers_active_published
    on public.job_offers(published_at desc)
    where is_active = true;

create or replace view public.job_offer_search_view as
select
    jo.id,
    js.code as source_code,
    js.display_name as source_name,
    jo.external_id,
    jo.external_url,
    jo.title,
    jo.company_name,
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
    jo.employment_type,
    jo.contract_type,
    jo.work_mode,
    jo.remote_scope,
    jo.work_time_type,
    jo.experience_level,
    jo.experience_min_years,
    jo.education_level,
    jo.education_field,
    jo.description_quality,
    jo.data_quality_score,
    jo.extraction_score,
    jo.is_remote,
    jo.published_at,
    jo.last_seen_at
from public.job_offers jo
join public.job_sources js on js.id = jo.source_id
where jo.is_active = true;

create or replace view public.job_offer_filter_values_view as
select filter_kind, filter_code, filter_name, count(distinct job_offer_id)::bigint as active_offer_count
from (
    select 'work_mode' as filter_kind, coalesce(work_mode, 'unknown') as filter_code, coalesce(work_mode, 'unknown') as filter_name, id as job_offer_id
    from public.job_offers where is_active = true and coalesce(work_mode, 'unknown') <> 'unknown'
    union all
    select 'work_time', coalesce(work_time_type, 'unknown'), coalesce(work_time_type, 'unknown'), id
    from public.job_offers where is_active = true and coalesce(work_time_type, 'unknown') <> 'unknown'
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
    select jc.kind, jc.code, jc.display_name, joc.job_offer_id
    from public.job_offer_criteria joc
    join public.job_criteria jc on jc.id = joc.criterion_id
    join public.job_offers jo on jo.id = joc.job_offer_id and jo.is_active = true
    where jc.is_user_selectable = true
) filter_values
group by filter_kind, filter_code, filter_name;
