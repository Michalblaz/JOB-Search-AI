using System.Collections.Generic;

namespace MauiApp1.testowe
{
    /// <summary>
    /// Reprezentuje ujednoliconą ofertę pracy wyświetlaną w aplikacji niezależnie od źródła danych.
    /// </summary>
    /// <remarks>
    /// Ten model jest wspólnym formatem dla ofert pobieranych z API, bazy PostgreSQL oraz lokalnych migawek ulubionych.
    /// Dzięki temu UI może korzystać z jednej struktury zamiast znać szczegóły formatów Adzuna, Jooble, Remotive lub ePraca.
    /// </remarks>
    /// <seealso cref="JobSearchService"/>
    /// <seealso cref="PostgresJobReader"/>
    public class JobOffer
    {
        public long? JobOfferId { get; set; }
        public string? ExternalId { get; set; }
        /// <summary>
        /// Nazwa stanowiska prezentowana użytkownikowi.
        /// </summary>
        /// <value>Tekst tytułu oferty po normalizacji; gdy źródło nie zwraca tytułu, usługi mapujące ustawiają wartość zastępczą.</value>
        public string Title { get; set; } = "";
        public string Company { get; set; } = "";
        public string? CompanyLogoUrl { get; set; }
        public string Location { get; set; } = "";
        public string Salary { get; set; } = "";
        /// <summary>
        /// Szczegółowy opis wynagrodzenia używany przez filtry i dopasowanie.
        /// </summary>
        /// <value>Może zawierać widełki, stawkę godzinową albo komunikat typu "Do uzgodnienia".</value>
        public string SalaryDetails { get; set; } = "";
        public string Experience { get; set; } = "";
        public decimal? ExperienceMinYears { get; set; }
        public decimal? ExperienceMaxYears { get; set; }
        public bool NoExperienceAllowed { get; set; }
        public bool? ExperienceRequired { get; set; }
        public decimal ExperienceConfidence { get; set; } = 0.5m;
        public string? ExperienceEvidence { get; set; }
        public string Education { get; set; } = "";
        public bool? EducationRequired { get; set; }
        public string? EducationField { get; set; }
        public decimal EducationConfidence { get; set; } = 0.5m;
        public string? EducationEvidence { get; set; }
        /// <summary>
        /// Nazwa źródła, z którego pochodzi oferta.
        /// </summary>
        /// <value>Wartość jest używana m.in. do filtrowania źródeł i rozpoznania obsługi filtra odległości.</value>
        public string Source { get; set; } = "";
        public string? SourceCode { get; set; }
        public string PostedAgo { get; set; } = "";
        public string ContractType { get; set; } = "";
        public string ContractTime { get; set; } = "";
        public string Category { get; set; } = "";
        public string PublishedAt { get; set; } = "";
        /// <summary>
        /// Określa, czy oferta może być wykonywana zdalnie.
        /// </summary>
        /// <value>Ustawiane na podstawie danych źródłowych lub wykrywane heurystycznie z opisu i lokalizacji.</value>
        public bool IsRemote { get; set; }
        public string? Url { get; set; }
        /// <summary>
        /// Wynik dopasowania oferty do aktywnych preferencji użytkownika.
        /// </summary>
        /// <value>Liczba od 0 do 100; może pochodzić z lokalnego algorytmu albo z usługi AI.</value>
        public int MatchScore { get; set; }
        public string MatchReason { get; set; } = "";
        public bool HasAiMatchScore { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public List<string> CategoryCodes { get; set; } = new();
        public List<string> RoleCodes { get; set; } = new();
        public List<string> CriterionCodes { get; set; } = new();
        public List<JobOfferCriterionMatch> Criteria { get; set; } = new();
        public string Description { get; set; } = "";
    }

    public class JobOfferCriterionMatch
    {
        public string Kind { get; set; } = "";
        public string Code { get; set; } = "";
        public bool IsRequired { get; set; }
    }

    public class JobRoleOption
    {
        public string CategoryCode { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string RoleCode { get; set; } = "";
        public string RoleName { get; set; } = "";
        public int ActiveOfferCount { get; set; }
    }

    public class JobCriterionOption
    {
        public string Kind { get; set; } = "";
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int ActiveOfferCount { get; set; }
    }
}
