CREATE TABLE dbo.job_sources (
    id INT IDENTITY(1,1) PRIMARY KEY,
    code NVARCHAR(50) NOT NULL UNIQUE,
    display_name NVARCHAR(100) NOT NULL,
    is_enabled BIT NOT NULL CONSTRAINT DF_job_sources_is_enabled DEFAULT (1),
    last_success_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT DF_job_sources_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at DATETIME2 NOT NULL CONSTRAINT DF_job_sources_updated_at DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.job_import_runs (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    source_id INT NOT NULL,
    started_at DATETIME2 NOT NULL CONSTRAINT DF_job_import_runs_started_at DEFAULT (SYSUTCDATETIME()),
    finished_at DATETIME2 NULL,
    status NVARCHAR(20) NOT NULL,
    fetched_count INT NOT NULL CONSTRAINT DF_job_import_runs_fetched_count DEFAULT (0),
    inserted_count INT NOT NULL CONSTRAINT DF_job_import_runs_inserted_count DEFAULT (0),
    updated_count INT NOT NULL CONSTRAINT DF_job_import_runs_updated_count DEFAULT (0),
    deactivated_count INT NOT NULL CONSTRAINT DF_job_import_runs_deactivated_count DEFAULT (0),
    error_message NVARCHAR(MAX) NULL,
    CONSTRAINT FK_job_import_runs_source FOREIGN KEY (source_id) REFERENCES dbo.job_sources(id)
);
GO

CREATE TABLE dbo.job_offers (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    source_id INT NOT NULL,
    external_id NVARCHAR(200) NOT NULL,
    external_url NVARCHAR(1000) NULL,
    title NVARCHAR(300) NOT NULL,
    company_name NVARCHAR(300) NULL,
    location_name NVARCHAR(300) NULL,
    city NVARCHAR(150) NULL,
    region NVARCHAR(150) NULL,
    country_code NVARCHAR(10) NULL,
    description NVARCHAR(MAX) NULL,
    description_short NVARCHAR(1000) NULL,
    salary_min DECIMAL(18,2) NULL,
    salary_max DECIMAL(18,2) NULL,
    salary_currency NVARCHAR(10) NULL,
    salary_raw NVARCHAR(200) NULL,
    employment_type NVARCHAR(100) NULL,
    contract_type NVARCHAR(100) NULL,
    experience_level NVARCHAR(100) NULL,
    education_level NVARCHAR(100) NULL,
    is_remote BIT NOT NULL CONSTRAINT DF_job_offers_is_remote DEFAULT (0),
    is_active BIT NOT NULL CONSTRAINT DF_job_offers_is_active DEFAULT (1),
    published_at DATETIME2 NULL,
    first_seen_at DATETIME2 NOT NULL CONSTRAINT DF_job_offers_first_seen_at DEFAULT (SYSUTCDATETIME()),
    last_seen_at DATETIME2 NOT NULL CONSTRAINT DF_job_offers_last_seen_at DEFAULT (SYSUTCDATETIME()),
    last_import_run_id BIGINT NULL,
    content_hash NVARCHAR(128) NULL,
    raw_payload_json NVARCHAR(MAX) NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT DF_job_offers_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at DATETIME2 NOT NULL CONSTRAINT DF_job_offers_updated_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_job_offers_source FOREIGN KEY (source_id) REFERENCES dbo.job_sources(id),
    CONSTRAINT FK_job_offers_import_run FOREIGN KEY (last_import_run_id) REFERENCES dbo.job_import_runs(id),
    CONSTRAINT UQ_job_offers_source_external UNIQUE (source_id, external_id)
);
GO

CREATE TABLE dbo.job_offer_languages (
    job_offer_id BIGINT NOT NULL,
    language_code NVARCHAR(20) NOT NULL,
    language_name NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_job_offer_languages PRIMARY KEY (job_offer_id, language_code),
    CONSTRAINT FK_job_offer_languages_offer FOREIGN KEY (job_offer_id) REFERENCES dbo.job_offers(id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.job_offer_tags (
    job_offer_id BIGINT NOT NULL,
    tag NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_job_offer_tags PRIMARY KEY (job_offer_id, tag),
    CONSTRAINT FK_job_offer_tags_offer FOREIGN KEY (job_offer_id) REFERENCES dbo.job_offers(id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_job_offers_is_active_published_at
ON dbo.job_offers (is_active, published_at DESC);
GO

CREATE INDEX IX_job_offers_city_is_active
ON dbo.job_offers (city, is_active);
GO

CREATE INDEX IX_job_offers_is_remote_is_active
ON dbo.job_offers (is_remote, is_active);
GO

CREATE INDEX IX_job_offers_experience_is_active
ON dbo.job_offers (experience_level, is_active);
GO

CREATE INDEX IX_job_offers_education_is_active
ON dbo.job_offers (education_level, is_active);
GO

CREATE INDEX IX_job_offers_salary_min_salary_max
ON dbo.job_offers (salary_min, salary_max);
GO

INSERT INTO dbo.job_sources (code, display_name)
VALUES
    (N'adzuna', N'Adzuna'),
    (N'jooble', N'Jooble'),
    (N'epraca', N'ePraca (CBOP)'),
    (N'remotive', N'Remotive'),
    (N'arbeitnow', N'Arbeitnow');
GO
