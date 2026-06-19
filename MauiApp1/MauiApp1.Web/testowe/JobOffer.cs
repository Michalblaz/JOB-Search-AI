using System.Collections.Generic;

namespace MauiApp1.testowe
{
    public class JobOffer
    {
        public long? JobOfferId { get; set; }
        public string? ExternalId { get; set; }
        public string Title { get; set; } = "";
        public string Company { get; set; } = "";
        public string? CompanyLogoUrl { get; set; }
        public string Location { get; set; } = "";
        public string Salary { get; set; } = "";
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
        public string Source { get; set; } = "";
        public string? SourceCode { get; set; }
        public string PostedAgo { get; set; } = "";
        public string ContractType { get; set; } = "";
        public string ContractTime { get; set; } = "";
        public string WorkMode { get; set; } = "unknown";
        public string WorkTimeType { get; set; } = "unknown";
        public string DescriptionQuality { get; set; } = "unknown";
        public decimal DataQualityScore { get; set; }
        public decimal ExtractionScore { get; set; }
        public string Category { get; set; } = "";
        public string PublishedAt { get; set; } = "";
        public bool IsRemote { get; set; }
        public string? Url { get; set; }
        public int MatchScore { get; set; }
        public string MatchReason { get; set; } = "";
        public bool HasAiMatchScore { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public List<string> CategoryCodes { get; set; } = new();
        public List<string> RoleCodes { get; set; } = new();
        public List<string> CriterionCodes { get; set; } = new();
        public List<string> ContractTypes { get; set; } = new();
        public List<string> BenefitCodes { get; set; } = new();
        public List<string> ScheduleFlags { get; set; } = new();
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

    public class JobFilterOption
    {
        public string Kind { get; set; } = "";
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int ActiveOfferCount { get; set; }
    }
}
