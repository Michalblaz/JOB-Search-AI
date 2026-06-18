create table if not exists public.job_sources (
    id integer generated always as identity primary key,
    code varchar(50) not null unique,
    display_name varchar(100) not null,
    is_enabled boolean not null default true,
    last_success_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.job_import_runs (
    id bigint generated always as identity primary key,
    source_id integer not null references public.job_sources(id),
    started_at timestamptz not null default now(),
    finished_at timestamptz null,
    status varchar(20) not null,
    fetched_count integer not null default 0,
    inserted_count integer not null default 0,
    updated_count integer not null default 0,
    deactivated_count integer not null default 0,
    error_message text null
);

create table if not exists public.job_offers (
    id bigint generated always as identity primary key,
    source_id integer not null references public.job_sources(id),
    external_id varchar(200) not null,
    external_url varchar(1000) null,
    title varchar(300) not null,
    company_name varchar(300) null,
    company_logo_url varchar(1000) null,
    location_name varchar(300) null,
    city varchar(150) null,
    region varchar(150) null,
    country_code varchar(10) null,
    description text null,
    description_quality varchar(30) not null default 'full',
    description_short varchar(1000) null,
    salary_min numeric(18,2) null,
    salary_max numeric(18,2) null,
    salary_currency varchar(10) null,
    salary_raw varchar(200) null,
    salary_is_predicted boolean null,
    salary_period varchar(30) null,
    salary_tax_type varchar(30) null,
    salary_rate_type varchar(30) null,
    salary_confidence numeric(5,4) not null default 0.5000,
    salary_evidence text null,
    employment_type varchar(100) null,
    contract_type varchar(100) null,
    employment_type_raw varchar(200) null,
    contract_type_raw varchar(200) null,
    work_mode varchar(50) null,
    remote_scope varchar(80) null,
    remote_country_restriction varchar(80) null,
    office_days_per_week_min numeric(3,1) null,
    office_days_per_week_max numeric(3,1) null,
    work_mode_confidence numeric(5,4) not null default 0.5000,
    work_mode_evidence text null,
    work_time_type varchar(50) null,
    fte_min numeric(3,2) null,
    fte_max numeric(3,2) null,
    hours_per_week_min numeric(5,2) null,
    hours_per_week_max numeric(5,2) null,
    work_time_confidence numeric(5,4) not null default 0.5000,
    work_time_evidence text null,
    experience_level varchar(100) null,
    experience_min_years numeric(4,1) null,
    experience_max_years numeric(4,1) null,
    no_experience_allowed boolean not null default false,
    experience_required boolean null,
    experience_confidence numeric(5,4) not null default 0.5000,
    experience_evidence text null,
    education_level varchar(100) null,
    education_required boolean null,
    education_field varchar(120) null,
    education_confidence numeric(5,4) not null default 0.5000,
    education_evidence text null,
    is_remote boolean not null default false,
    latitude double precision null,
    longitude double precision null,
    is_active boolean not null default true,
    published_at timestamptz null,
    first_seen_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    last_import_run_id bigint null references public.job_import_runs(id),
    content_hash varchar(128) null,
    external_reference varchar(500) null,
    raw_payload_json jsonb null,
    data_quality_score numeric(5,2) not null default 0,
    extraction_score numeric(5,2) not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint uq_job_offers_source_external unique (source_id, external_id)
);

alter table public.job_offers
    add column if not exists company_logo_url varchar(1000) null,
    add column if not exists salary_is_predicted boolean null,
    add column if not exists latitude double precision null,
    add column if not exists longitude double precision null,
    add column if not exists external_reference varchar(500) null,
    add column if not exists description_quality varchar(30) not null default 'full',
    add column if not exists experience_min_years numeric(4,1) null,
    add column if not exists experience_max_years numeric(4,1) null,
    add column if not exists no_experience_allowed boolean not null default false,
    add column if not exists experience_required boolean null,
    add column if not exists experience_confidence numeric(5,4) not null default 0.5000,
    add column if not exists experience_evidence text null,
    add column if not exists education_required boolean null,
    add column if not exists education_field varchar(120) null,
    add column if not exists education_confidence numeric(5,4) not null default 0.5000,
    add column if not exists education_evidence text null,
    add column if not exists employment_type_raw varchar(200) null,
    add column if not exists contract_type_raw varchar(200) null,
    add column if not exists work_mode varchar(50) null,
    add column if not exists remote_scope varchar(80) null,
    add column if not exists remote_country_restriction varchar(80) null,
    add column if not exists office_days_per_week_min numeric(3,1) null,
    add column if not exists office_days_per_week_max numeric(3,1) null,
    add column if not exists work_mode_confidence numeric(5,4) not null default 0.5000,
    add column if not exists work_mode_evidence text null,
    add column if not exists work_time_type varchar(50) null,
    add column if not exists fte_min numeric(3,2) null,
    add column if not exists fte_max numeric(3,2) null,
    add column if not exists hours_per_week_min numeric(5,2) null,
    add column if not exists hours_per_week_max numeric(5,2) null,
    add column if not exists work_time_confidence numeric(5,4) not null default 0.5000,
    add column if not exists work_time_evidence text null,
    add column if not exists salary_period varchar(30) null,
    add column if not exists salary_tax_type varchar(30) null,
    add column if not exists salary_rate_type varchar(30) null,
    add column if not exists salary_confidence numeric(5,4) not null default 0.5000,
    add column if not exists salary_evidence text null,
    add column if not exists data_quality_score numeric(5,2) not null default 0,
    add column if not exists extraction_score numeric(5,2) not null default 0,
    add column if not exists raw_payload_json jsonb null;

create table if not exists public.job_offer_languages (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    language_code varchar(20) not null,
    language_name varchar(100) not null,
    primary key (job_offer_id, language_code)
);

create table if not exists public.job_offer_tags (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    tag varchar(100) not null,
    primary key (job_offer_id, tag)
);

create table if not exists public.job_categories (
    id integer generated always as identity primary key,
    parent_id integer null references public.job_categories(id),
    code varchar(100) not null unique,
    display_name varchar(150) not null,
    description text null,
    is_user_selectable boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.job_roles (
    id integer generated always as identity primary key,
    category_id integer null references public.job_categories(id),
    code varchar(120) not null unique,
    display_name varchar(180) not null,
    description text null,
    is_user_selectable boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.job_offer_roles (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    role_id integer not null references public.job_roles(id) on delete cascade,
    confidence numeric(5,4) not null default 0.5000,
    evidence varchar(300) null,
    primary key (job_offer_id, role_id)
);

create table if not exists public.job_criteria (
    id integer generated always as identity primary key,
    kind varchar(40) not null,
    code varchar(120) not null,
    display_name varchar(180) not null,
    description text null,
    is_user_selectable boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint uq_job_criteria_kind_code unique (kind, code)
);

create table if not exists public.job_offer_criteria (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    criterion_id integer not null references public.job_criteria(id) on delete cascade,
    is_required boolean not null default false,
    confidence numeric(5,4) not null default 0.5000,
    evidence varchar(500) null,
    primary key (job_offer_id, criterion_id)
);

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
);

create table if not exists public.job_offer_schedule_flags (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    schedule_flag varchar(60) not null,
    confidence numeric(5,4) not null default 0.5000,
    evidence text,
    source_field varchar(80),
    source_section varchar(80),
    created_at timestamptz not null default now(),
    primary key (job_offer_id, schedule_flag)
);

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
);

create table if not exists public.job_offer_domains (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    domain_code varchar(80) not null,
    domain_name varchar(160) not null,
    confidence numeric(5,4) not null default 0.5000,
    evidence text,
    created_at timestamptz not null default now(),
    primary key (job_offer_id, domain_code)
);

create table if not exists public.job_offer_formal_requirements (
    job_offer_id bigint not null references public.job_offers(id) on delete cascade,
    requirement_code varchar(100) not null,
    is_required boolean not null default true,
    confidence numeric(5,4) not null default 0.5000,
    evidence text,
    created_at timestamptz not null default now(),
    primary key (job_offer_id, requirement_code)
);

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

create table if not exists public.user_profile_criteria (
    user_id bigint not null references public.app_users(id) on delete cascade,
    criterion_id integer not null references public.job_criteria(id) on delete cascade,
    importance smallint not null default 1,
    created_at timestamptz not null default now(),
    primary key (user_id, criterion_id)
);

create table if not exists public.user_favorite_offer_snapshots (
    id bigint generated always as identity primary key,
    user_id bigint not null references public.app_users(id) on delete cascade,
    job_offer_id bigint null references public.job_offers(id) on delete set null,
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

alter table public.user_favorite_offer_snapshots
    add column if not exists id bigint generated always as identity,
    add column if not exists job_offer_id bigint null references public.job_offers(id) on delete set null,
    add column if not exists source_code varchar(50) null,
    add column if not exists external_id varchar(200) null,
    add column if not exists company_logo_url varchar(1000) null;

create table if not exists public.user_search_history_snapshots (
    id bigint generated always as identity primary key,
    user_id bigint not null references public.app_users(id) on delete cascade,
    job_offer_id bigint null references public.job_offers(id) on delete set null,
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

alter table public.user_search_history_snapshots
    add column if not exists job_offer_id bigint null references public.job_offers(id) on delete set null,
    add column if not exists source_code varchar(50) null,
    add column if not exists external_id varchar(200) null,
    add column if not exists company_logo_url varchar(1000) null;

create index if not exists ix_job_offers_is_active_published_at
    on public.job_offers (is_active, published_at desc);

create index if not exists ix_job_offers_source_is_active
    on public.job_offers (source_id, is_active);

create index if not exists ix_job_offers_city_is_active
    on public.job_offers (city, is_active);

create index if not exists ix_job_offers_is_remote_is_active
    on public.job_offers (is_remote, is_active);

create index if not exists ix_job_offers_experience_is_active
    on public.job_offers (experience_level, is_active);

create index if not exists ix_job_offers_experience_min_years
    on public.job_offers (experience_min_years);

create index if not exists ix_job_offers_experience_max_years
    on public.job_offers (experience_max_years);

create index if not exists ix_job_offers_no_experience_allowed
    on public.job_offers (no_experience_allowed);

create index if not exists ix_job_offers_experience_required
    on public.job_offers (experience_required);

create index if not exists ix_job_offers_education_is_active
    on public.job_offers (education_level, is_active);

create index if not exists ix_job_offers_education_required
    on public.job_offers (education_required);

create index if not exists ix_job_offers_education_field
    on public.job_offers (education_field);

create index if not exists ix_job_offers_salary_min_salary_max
    on public.job_offers (salary_min, salary_max);

create index if not exists ix_job_offers_work_mode
    on public.job_offers (work_mode);

create index if not exists ix_job_offers_remote_scope
    on public.job_offers (remote_scope);

create index if not exists ix_job_offers_work_time_type
    on public.job_offers (work_time_type);

create index if not exists ix_job_offers_salary_period
    on public.job_offers (salary_period);

create index if not exists ix_job_offers_salary_tax_type
    on public.job_offers (salary_tax_type);

create index if not exists ix_job_offers_data_quality_score
    on public.job_offers (data_quality_score);

create index if not exists ix_job_offers_extraction_score
    on public.job_offers (extraction_score);

create index if not exists ix_job_offer_contract_types_type
    on public.job_offer_contract_types (contract_type);

create index if not exists ix_job_offer_schedule_flags_flag
    on public.job_offer_schedule_flags (schedule_flag);

create index if not exists ix_job_offer_benefits_code
    on public.job_offer_benefits (benefit_code);

create index if not exists ix_job_offer_domains_code
    on public.job_offer_domains (domain_code);

create index if not exists ix_job_offer_formal_requirements_code
    on public.job_offer_formal_requirements (requirement_code);

create index if not exists ix_job_offer_languages_language_name
    on public.job_offer_languages (language_name);

create index if not exists ix_job_offer_tags_tag
    on public.job_offer_tags (tag);

create index if not exists ix_job_roles_category_id
    on public.job_roles (category_id);

create index if not exists ix_job_offer_roles_role_id
    on public.job_offer_roles (role_id);

create index if not exists ix_job_criteria_kind_display_name
    on public.job_criteria (kind, display_name);

create index if not exists ix_job_offer_criteria_criterion_id
    on public.job_offer_criteria (criterion_id);

create index if not exists ix_app_users_login
    on public.app_users (login);

create index if not exists ix_user_favorite_offer_snapshots_user_created_at
    on public.user_favorite_offer_snapshots (user_id, created_at desc);

create index if not exists ix_user_search_history_snapshots_user_viewed_at
    on public.user_search_history_snapshots (user_id, viewed_at desc);

create index if not exists ix_user_profile_criteria_user_id
    on public.user_profile_criteria (user_id);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists trg_job_sources_set_updated_at on public.job_sources;
create trigger trg_job_sources_set_updated_at
before update on public.job_sources
for each row
execute function public.set_updated_at();

drop trigger if exists trg_job_offers_set_updated_at on public.job_offers;
create trigger trg_job_offers_set_updated_at
before update on public.job_offers
for each row
execute function public.set_updated_at();

drop trigger if exists trg_job_categories_set_updated_at on public.job_categories;
create trigger trg_job_categories_set_updated_at
before update on public.job_categories
for each row
execute function public.set_updated_at();

drop trigger if exists trg_job_roles_set_updated_at on public.job_roles;
create trigger trg_job_roles_set_updated_at
before update on public.job_roles
for each row
execute function public.set_updated_at();

drop trigger if exists trg_job_criteria_set_updated_at on public.job_criteria;
create trigger trg_job_criteria_set_updated_at
before update on public.job_criteria
for each row
execute function public.set_updated_at();

drop trigger if exists trg_app_users_set_updated_at on public.app_users;
create trigger trg_app_users_set_updated_at
before update on public.app_users
for each row
execute function public.set_updated_at();

drop trigger if exists trg_user_profiles_set_updated_at on public.user_profiles;
create trigger trg_user_profiles_set_updated_at
before update on public.user_profiles
for each row
execute function public.set_updated_at();

insert into public.job_sources (code, display_name)
values
    ('adzuna', 'Adzuna'),
    ('jooble', 'Jooble'),
    ('epraca', 'ePraca (CBOP)'),
    ('remotive', 'Remotive'),
    ('arbeitnow', 'Arbeitnow')
on conflict (code) do update
set
    display_name = excluded.display_name,
    updated_at = now();

insert into public.job_categories (code, display_name)
values
    ('it', 'IT i technologia'),
    ('sales', 'Sprzedaż i handel'),
    ('marketing', 'Marketing i social media'),
    ('customer_service', 'Obsługa klienta'),
    ('logistics', 'Logistyka i magazyn'),
    ('transport', 'Transport i dostawy'),
    ('care', 'Opieka i zdrowie'),
    ('office', 'Administracja i biuro'),
    ('finance', 'Finanse i księgowość'),
    ('construction', 'Budownictwo'),
    ('production', 'Produkcja'),
    ('gastronomy', 'Gastronomia'),
    ('technical', 'Techniczne i serwis'),
    ('other', 'Inne')
on conflict (code) do update
set
    display_name = excluded.display_name,
    updated_at = now();

create or replace view public.active_job_offers as
select *
from public.job_offers
where is_active = true;

create or replace view public.job_offer_search_view as
select
    id,
    title,
    company_name,
    city,
    region,
    country_code,
    salary_min,
    salary_max,
    salary_currency,
    salary_period,
    salary_tax_type,
    salary_rate_type,
    work_mode,
    remote_scope,
    work_time_type,
    experience_level,
    experience_min_years,
    education_level,
    education_field,
    data_quality_score,
    extraction_score,
    published_at,
    last_seen_at
from public.job_offers
where is_active = true;

create or replace view public.active_job_filter_options as
select
    jc.kind,
    jc.code,
    jc.display_name,
    count(distinct joc.job_offer_id) as active_offer_count
from public.job_criteria jc
join public.job_offer_criteria joc on joc.criterion_id = jc.id
join public.job_offers jo on jo.id = joc.job_offer_id and jo.is_active = true
where jc.is_user_selectable = true
group by jc.kind, jc.code, jc.display_name;

create or replace view public.active_job_role_options as
select
    coalesce(jc.code, 'other') as category_code,
    coalesce(jc.display_name, 'Inne') as category_name,
    jr.code as role_code,
    jr.display_name as role_name,
    count(distinct jor.job_offer_id) as active_offer_count
from public.job_roles jr
left join public.job_categories jc on jc.id = jr.category_id
join public.job_offer_roles jor on jor.role_id = jr.id
join public.job_offers jo on jo.id = jor.job_offer_id and jo.is_active = true
where jr.is_user_selectable = true
group by jc.code, jc.display_name, jr.code, jr.display_name;
