using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MauiApp1.Importer;

public static class JobClassificationRules
{
    private const string RequiredLevel = "required";
    private const string OptionalLevel = "optional";
    private const string MentionedLevel = "mentioned";
    private const string ExtractorVersion = ExtractorVersionProvider.Current;

    private static readonly Regex RequiredHeader = new(
        @"^(wymagania|wymagane|nasze wymagania|nasze oczekiwania|oczekujemy|czego oczekujemy|kwalifikacje|profil kandydata|must have|must-have|must haves|required|required skills|requirements|key requirements|minimum qualifications|qualifications|expected|you have|you should have|what you need|what we expect|skills required|anforderungen|qualifikationen|dein profil|was du mitbringst)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OptionalHeader = new(
        @"^(mile widziane|mile widziane bedzie|dodatkowy atut|atutem bedzie|nice to have|nice-to-have|nice to haves|optional|optional skills|preferred|preferred qualifications|bonus skills|bonus points if|would be a plus|will be a plus|is a plus|wunsch|von vorteil)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NeutralHeader = new(
        @"^(obowiazki|zadania|zakres obowiazkow|twoj zakres obowiazkow|responsibilities|your responsibilities|what you will do|oferujemy|to oferujemy|benefity|benefits|about us|o firmie|about the company|wir bieten|aufgaben)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RequiredCue = new(
        @"\b(wymagan\w*|oczek\w*|koniecz\w*|niezbedn\w*|musisz|musi|powinien\w*|znajomosc|umiejetnosc|doswiadczenie z|doswiadczenie w|minimum|must|required|requirement|requirements|proficiency|experience with|knowledge of|strong knowledge|hands[- ]on|muss|erforderlich|erfahrung mit|kenntnisse)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OptionalCue = new(
        @"\b(mile widzian\w*|dodatkowy atut|atutem bedzie|nice to have|nice-to-have|preferred|optional|bonus skills|bonus points if|would be a plus|will be a plus|is a plus|as a plus|wunsch|von vorteil)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompensationBonusCue = new(
        @"\b(bonus rekrutacyjny|premia|dodatek|benefit|benefity|relocation bonus|signing bonus|uncapped commission|commission|wynagrodzenie|zarobki)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AiDisclaimerCue = new(
        @"\b(ai[- ]?assisted recruitment|ai in recruitment process|rekrutacja wspierana przez ai|proces rekrutacji wspierany przez ai|ki[- ]?gestutzte systeme|ki[- ]?gestuetzte systeme|eu ai act|artificial intelligence act|nie uzywamy danych do trenowania ai|automatyczne przetwarzanie danych)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StackCue = new(
        @"\b(tech stack|technology stack|technologie|technologies|stack|narzedzia|tools|pracujemy z|you will work with|we use|uzywamy|wykorzystujemy)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TechContextCue = new(
        @"\b(programming|developer|software|backend|frontend|fullstack|stack|technology|technologies|framework|language|jezyk|programowania|technologie|technologii|aplikacja|system|kod|code|engineer|programista|entwickler|entwicklung|softwareentwicklung|informatik|it-support|systemadministrator)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ItRoleTitleCue = new(
        @"\b(developer|programista|engineer|software|frontend|backend|fullstack|devops|tester|qa|administrator|data engineer|cloud engineer|architect|entwickler|softwareentwickler|systemadministrator|informatiker)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static CriterionRule[]? DatabaseCriterionRules;

    private static readonly TaxonomyRule[] CategoryRules =
    {
        new("it", "IT i technologia", "it", "informatyk", "programista", "developer", "software", "frontend", "backend", ".net", "java", "python", "tester", "devops", "administrator", "helpdesk", "support it", "data analyst"),
        new("sales", "Sprzedaż i handel", "sprzedawca", "sprzedaz", "handel", "kasjer", "ekspedient", "doradca klienta", "przedstawiciel handlowy", "sales"),
        new("marketing", "Marketing i social media", "marketing", "social media", "influencer", "e-commerce", "kampania", "campaign"),
        new("customer_service", "Obsługa klienta", "obsluga klienta", "customer service", "call center", "konsultant", "help desk"),
        new("logistics", "Logistyka i magazyn", "magazynier", "magazyn", "logistyka", "warehouse", "operator wozka", "forklift"),
        new("transport", "Transport i dostawy", "kierowca", "kurier", "dostawca", "driver", "transport"),
        new("care", "Opieka i zdrowie", "opiekun", "opiekunka", "osoby starsze", "senior", "caregiver", "pielegniarka", "ratownik", "medycz"),
        new("office", "Administracja i biuro", "pracownik biurowy", "administracja", "asystent biura", "sekretarka", "office assistant", "recepcjon"),
        new("finance", "Finanse i księgowość", "ksiegow", "accountant", "finanse", "kadry", "place", "payroll"),
        new("construction", "Budownictwo", "budowa", "budowlany", "murarz", "ciesla", "dekarz", "zbrojarz", "operator koparki"),
        new("production", "Produkcja", "produkcja", "operator produkcji", "monter", "pakowacz", "pracownik produkcji"),
        new("gastronomy", "Gastronomia", "kucharz", "kelner", "barista", "gastronomia", "restaurac", "pomoc kuchenna"),
        new("technical", "Techniczne i serwis", "mechanik", "elektryk", "spawacz", "slusarz", "serwisant", "utrzymanie ruchu", "automatyk"),
        new("it", "IT i technologia", "entwickler", "softwareentwickler", "informatik", "it-support", "software development", "systemadministrator", "product engineer"),
        new("finance", "Finanse i ksiegowosc", "buchhalter", "finanzbuchhalter", "bilanzbuchhalter", "entgeltabrechner", "gehaltsabrechner", "accounts payable", "accounting", "finance"),
        new("customer_service", "Obsluga klienta", "kundenberater", "kundenservice", "beratung"),
        new("marketing", "Marketing i social media", "content creator", "video content", "campaign management", "direct marketing", "influencer marketing"),
        new("technical", "Techniczne i serwis", "techniker", "ingenieur", "fachplaner", "facility management", "versorgungstechnik", "hkls", "hlsk", "engineering"),
        new("office", "Administracja i biuro", "sachbearbeiter", "office management", "personalreferent", "personalentwicklung"),
        new("production", "Produkcja", "qualitatssicherung", "qualitaetssicherung", "quality assurance", "backerei", "baeckerei"),
        new("data", "Dane i analityka", "data analyst", "online data analyst", "analityk danych", "business intelligence", "bi analyst"),
        new("gastronomy", "Gastronomia", "kfc", "restauracji", "restaurant crew", "game presenter"),
        new("construction", "Budownictwo", "prace elewacyjne", "ogolnobudowlany", "remontowo-budowlany")
    };

    private static readonly RoleRule[] RoleRules =
    {
        new("software_developer", "Programista / Developer", "it", "programista", "developer", "software engineer", "backend", "frontend", "fullstack", ".net", "java developer", "python developer"),
        new("frontend_developer", "Frontend Developer", "it", "frontend developer", "front-end developer", "front end developer", "react developer", "javascript developer"),
        new("data_engineer", "Data Engineer", "it", "data engineer", "data platform engineer", "data engineering"),
        new("ai_engineer", "AI / ML Engineer", "it", "ai engineer", "ml engineer", "machine learning engineer", "genai", "ki engineer"),
        new("solutions_architect", "Solution Architect", "it", "solutions architect", "solution architect", "systemarchitektur", "system architecture"),
        new("it_support", "Wsparcie IT / Helpdesk", "it", "helpdesk", "support it", "wsparcie it", "technik informatyk", "administrator it"),
        new("tester", "Tester oprogramowania", "it", "tester oprogramowania", "software tester", "qa engineer", "qa tester", "quality assurance"),
        new("devops", "DevOps / Administrator systemów", "it", "devops", "administrator linux", "administrator systemow", "cloud engineer"),
        new("seller", "Sprzedawca / Kasjer", "sales", "sprzedawca", "kasjer", "ekspedient", "pracownik sklepu"),
        new("sales_representative", "Przedstawiciel handlowy", "sales", "przedstawiciel handlowy", "handlowiec", "sales representative"),
        new("customer_service_agent", "Konsultant obsługi klienta", "customer_service", "obsluga klienta", "konsultant", "call center", "customer service"),
        new("warehouse_worker", "Magazynier", "logistics", "magazynier", "pracownik magazynu", "warehouse"),
        new("forklift_operator", "Operator wózka widłowego", "logistics", "operator wozka", "forklift"),
        new("driver", "Kierowca / Kurier", "transport", "kierowca", "kurier", "dostawca", "driver"),
        new("caregiver", "Opiekun osób starszych", "care", "opiekun osob starszych", "opiekunka osob starszych", "caregiver", "senior care"),
        new("office_assistant", "Pracownik biurowy", "office", "pracownik biurowy", "asystent biura", "administracja biurowa", "office assistant"),
        new("accountant", "Księgowość", "finance", "ksiegow", "accountant", "kadry", "place", "payroll"),
        new("finance_manager", "Finance / Accounting Manager", "finance", "finance accounting", "leitung finance", "accounting manager", "bilanzbuchhalter"),
        new("social_media_marketing", "Social Media / Influencer Marketing", "marketing", "social media", "influencer marketing", "marketing", "e-commerce"),
        new("construction_worker", "Pracownik budowlany", "construction", "budowlany", "murarz", "ciesla", "dekarz", "zbrojarz"),
        new("production_worker", "Pracownik produkcji", "production", "produkcja", "operator produkcji", "monter", "pakowacz"),
        new("cook", "Kucharz / Pomoc kuchenna", "gastronomy", "kucharz", "pomoc kuchenna", "cook", "chef"),
        new("waiter", "Kelner / Obsługa sali", "gastronomy", "kelner", "obsluga sali", "waiter"),
        new("mechanic", "Mechanik / Serwisant", "technical", "mechanik", "serwisant", "technik serwisu"),
        new("software_developer", "Programista / Developer", "it", "entwickler", "softwareentwickler", "software development", "product engineer"),
        new("it_support", "Wsparcie IT / Helpdesk", "it", "it-support", "1st level support", "first level support", "anwender support", "technical support"),
        new("tester", "Tester oprogramowania", "it", "test engineer", "manual tester", "test automation", "qa lead"),
        new("devops", "DevOps / Administrator systemow", "it", "platform engineer", "platform devops", "systemadministrator", "site reliability"),
        new("customer_service_agent", "Konsultant obslugi klienta", "customer_service", "kundenberater", "kundenservice", "customer success"),
        new("accountant", "Ksiegowosc", "finance", "buchhalter", "finanzbuchhalter", "bilanzbuchhalter", "entgeltabrechner", "gehaltsabrechner", "accounts payable"),
        new("finance_manager", "Finance / Accounting Manager", "finance", "leitung finance", "abteilungsleiter finance", "head of finance"),
        new("social_media_marketing", "Social Media / Influencer Marketing", "marketing", "video content creator", "campaign management", "content creator", "direct marketing"),
        new("mechanic", "Mechanik / Serwisant", "technical", "service techniker", "servicetechniker", "techniker", "facility management"),
        new("electrician", "Elektryk / Automatyk", "technical", "betriebsingenieur", "fachplaner", "versorgungstechnik", "hkls", "hlsk", "tga"),
        new("electrician", "Elektryk / Automatyk", "technical", "elektryk", "automatyk", "elektromonter"),
        new("data_analyst", "Analityk danych", "data", "data analyst", "online data analyst", "analityk danych", "business intelligence analyst", "bi analyst"),
        new("mechanical_engineer", "Inzynier mechanik / konstruktor", "technical", "mechanical engineer", "konstruktor", "cad", "nx", "budowy maszyn"),
        new("restaurant_worker", "Pracownik restauracji", "gastronomy", "pracownik restauracji", "kfc", "serwowanie zamowien", "przygotowywanie produktow"),
        new("construction_worker", "Pracownik budowlany", "construction", "ogolnobudowlany", "remontowo-budowlany", "prace elewacyjne"),
        new("welder", "Spawacz / Ślusarz", "technical", "spawacz", "slusarz", "monter konstrukcji")
    };

    private static readonly CriterionRule[] CriterionRules =
    {
        new("programming_language", "csharp", "C#", "c#", "c sharp"),
        new("programming_language", "cpp", "C++", "c++", "cpp"),
        new("programming_language", "c", "C", "jezyk c", "c language"),
        new("programming_language", "java", "Java", "java"),
        new("programming_language", "go", "Go", "go", "golang", "go language"),
        new("programming_language", "rust", "Rust", "rust"),
        new("programming_language", "kotlin", "Kotlin", "kotlin"),
        new("programming_language", "swift", "Swift", "swift"),
        new("programming_language", "python", "Python", "python"),
        new("programming_language", "scala", "Scala", "scala"),
        new("programming_language", "javascript", "JavaScript", "javascript", "js"),
        new("programming_language", "typescript", "TypeScript", "typescript", "ts"),
        new("programming_language", "php", "PHP", "php"),
        new("programming_language", "ruby", "Ruby", "ruby"),
        new("programming_language", "abap", "ABAP", "abap"),
        new("programming_language", "matlab", "MATLAB", "matlab"),

        new("framework", "dotnet", ".NET", ".net", "dotnet", "net developer", "net core", "net framework"),
        new("framework", "aspnet_core", "ASP.NET Core", "asp.net core", "aspnet core", "asp.net"),
        new("framework", "entity_framework", "Entity Framework", "entity framework", "ef core"),
        new("framework", "blazor", "Blazor", "blazor"),
        new("framework", "spring", "Spring", "spring"),
        new("framework", "spring_boot", "Spring Boot", "spring boot", "springboot"),
        new("framework", "hibernate", "Hibernate", "hibernate"),
        new("framework", "react", "React", "react"),
        new("framework", "react_native", "React Native", "react native"),
        new("framework", "nextjs", "Next.js", "next.js", "nextjs"),
        new("framework", "angular", "Angular", "angular"),
        new("framework", "vue", "Vue", "vue"),
        new("framework", "nodejs", "Node.js", "node.js", "nodejs", "node js"),
        new("framework", "nestjs", "NestJS", "nestjs", "nest.js"),
        new("framework", "expressjs", "Express.js", "express.js", "express js", "express"),
        new("framework", "django", "Django", "django"),
        new("framework", "flask", "Flask", "flask"),
        new("framework", "fastapi", "FastAPI", "fastapi"),
        new("framework", "laravel", "Laravel", "laravel"),
        new("framework", "magento", "Magento / Adobe Commerce", "magento", "magento 2", "adobe commerce"),
        new("framework", "shopify", "Shopify", "shopify"),
        new("framework", "weweb", "WeWeb", "weweb"),
        new("framework", "softr", "Softr", "softr"),
        new("framework", "symfony", "Symfony", "symfony"),
        new("framework", "rails", "Ruby on Rails", "ruby on rails", "rails"),
        new("framework", "tailwind", "Tailwind", "tailwind", "tailwind css"),

        new("database", "sql", "SQL", "sql", "bazy danych"),
        new("database", "mssql", "SQL Server", "sql server", "mssql", "ms sql", "t-sql"),
        new("database", "postgresql", "PostgreSQL", "postgres", "postgresql"),
        new("database", "mysql", "MySQL", "mysql"),
        new("database", "mongodb", "MongoDB", "mongodb", "mongo db"),
        new("database", "oracle", "Oracle", "oracle"),
        new("database", "plsql", "PL/SQL", "pl/sql", "plsql"),
        new("database", "redis", "Redis", "redis"),
        new("database", "cassandra", "Cassandra", "cassandra"),
        new("database", "snowflake", "Snowflake", "snowflake"),
        new("database", "bigquery", "BigQuery", "bigquery", "google bigquery"),
        new("database", "redshift", "Redshift", "redshift"),
        new("database", "vector_database", "Bazy wektorowe", "vector database", "wektordanych", "bazy wektorowe"),
        new("database", "qdrant", "Qdrant", "qdrant"),
        new("database", "weaviate", "Weaviate", "weaviate"),
        new("database", "milvus", "Milvus", "milvus"),
        new("database", "pgvector", "pgvector", "pgvector"),

        new("cloud", "aws", "AWS", "aws", "amazon web services"),
        new("cloud", "azure", "Azure", "azure", "microsoft azure"),
        new("cloud", "gcp", "Google Cloud", "gcp", "google cloud", "google cloud platform"),
        new("cloud", "openshift", "OpenShift", "openshift", "open shift"),

        new("devops_tool", "docker", "Docker", "docker"),
        new("devops_tool", "kubernetes", "Kubernetes", "kubernetes", "k8s"),
        new("devops_tool", "terraform", "Terraform", "terraform"),
        new("devops_tool", "harness", "Harness", "harness"),
        new("devops_tool", "jfrog_artifactory", "JFrog Artifactory", "jfrog", "jfrog artifactory", "artifactory"),
        new("devops_tool", "argo_cd", "Argo CD", "argocd", "argo cd"),
        new("devops_tool", "flux_cd", "FluxCD", "fluxcd", "flux cd"),
        new("devops_tool", "conan", "Conan", "conan"),
        new("devops_tool", "ansible", "Ansible", "ansible"),
        new("devops_tool", "jenkins", "Jenkins", "jenkins"),
        new("devops_tool", "git", "Git", "git"),
        new("devops_tool", "github", "GitHub", "github"),
        new("devops_tool", "gitlab", "GitLab", "gitlab"),
        new("devops_tool", "bitbucket", "Bitbucket", "bitbucket"),
        new("devops_tool", "ci_cd", "CI/CD", "ci/cd", "cicd"),
        new("devops_tool", "helm", "Helm", "helm"),
        new("devops_tool", "linux", "Linux", "linux"),
        new("devops_tool", "windows_server", "Windows Server", "windows server"),
        new("devops_tool", "active_directory", "Active Directory", "active directory", "ad"),
        new("devops_tool", "intune", "Intune", "intune"),
        new("devops_tool", "vmware", "VMware", "vmware"),
        new("devops_tool", "grafana", "Grafana", "grafana"),
        new("devops_tool", "prometheus", "Prometheus", "prometheus"),
        new("devops_tool", "datadog", "Datadog", "datadog"),

        new("testing_tool", "jest", "Jest", "jest.js", "jest testing"),
        new("testing_tool", "junit", "JUnit", "junit"),
        new("testing_tool", "mockito", "Mockito", "mockito"),
        new("testing_tool", "cypress", "Cypress", "cypress"),
        new("testing_tool", "playwright", "Playwright", "playwright"),
        new("testing_tool", "robot_framework", "Robot Framework", "robot framework"),
        new("testing_tool", "selenium", "Selenium", "selenium"),
        new("testing_tool", "postman", "Postman", "postman"),
        new("testing_tool", "testing", "Testowanie", "testing", "testy", "quality assurance"),

        new("methodology", "scrum", "Scrum", "scrum"),
        new("methodology", "agile", "Agile", "agile"),

        new("data_tool", "spark", "Spark", "spark", "apache spark"),
        new("data_tool", "databricks", "Databricks", "databricks"),
        new("data_tool", "power_bi", "Power BI", "power bi"),
        new("data_tool", "tableau", "Tableau", "tableau"),
        new("data_tool", "dbt", "dbt", "dbt"),
        new("data_tool", "airflow", "Airflow", "airflow"),
        new("data_tool", "dagster", "Dagster", "dagster"),
        new("data_tool", "etl", "ETL/ELT", "etl", "elt"),
        new("data_tool", "numpy", "NumPy", "numpy"),
        new("data_tool", "pandas", "Pandas", "pandas"),
        new("data_tool", "scikit_learn", "Scikit-learn", "scikit-learn", "sklearn"),

        new("ai_tool", "ai_ml", "AI/ML", "ai/ml", "machine learning", "ki", "genai", "generative ai", "llm"),
        new("ai_tool", "nlp", "NLP", "nlp", "natural language processing"),
        new("ai_tool", "computer_vision", "Computer Vision", "computer vision"),
        new("ai_tool", "hugging_face", "Hugging Face", "hugging face"),
        new("ai_tool", "langchain", "LangChain", "langchain"),
        new("ai_tool", "llamaindex", "LlamaIndex", "llamaindex", "llama index"),
        new("ai_tool", "langgraph", "LangGraph", "langgraph"),
        new("ai_tool", "rag", "RAG", "rag", "retrieval"),
        new("ai_tool", "llmops", "LLMOps", "llmops"),

        new("integration_tool", "graphql", "GraphQL", "graphql"),
        new("integration_tool", "rest", "REST API", "rest api", "restful"),
        new("integration_tool", "soap", "SOAP", "soap"),
        new("integration_tool", "swagger", "Swagger", "swagger", "openapi", "open api"),
        new("integration_tool", "json", "JSON", "json"),
        new("integration_tool", "kafka", "Kafka", "kafka", "apache kafka"),
        new("integration_tool", "rabbitmq", "RabbitMQ", "rabbitmq", "rabbit mq"),
        new("integration_tool", "microservices", "Mikroserwisy", "microservices", "mikroserwisy"),
        new("integration_tool", "n8n", "N8N", "n8n"),
        new("integration_tool", "make", "Make.com", "make.com", "make automation"),
        new("integration_tool", "low_code", "Low-code / No-code", "low-code", "no-code", "low code", "no code"),

        new("business_tool", "jira", "Jira", "jira"),
        new("business_tool", "servicenow", "ServiceNow", "servicenow", "service now"),
        new("business_tool", "sap", "SAP", "sap"),
        new("business_tool", "salesforce", "Salesforce", "salesforce"),
        new("business_tool", "dynamics_crm", "Dynamics CRM", "dynamics crm", "microsoft dynamics"),
        new("business_tool", "power_platform", "Power Platform", "power platform"),
        new("business_tool", "dataverse", "Dataverse", "dataverse"),
        new("business_tool", "microsoft_365", "Microsoft 365", "microsoft 365", "office 365", "m365"),
        new("business_tool", "excel", "Microsoft Excel", "microsoft excel", "excel"),
        new("business_tool", "outlook", "Outlook", "outlook"),
        new("business_tool", "exchange", "Exchange", "exchange"),
        new("business_tool", "sharepoint", "SharePoint", "sharepoint"),
        new("business_tool", "figma", "Figma", "figma"),
        new("business_tool", "ux", "UX", "ux", "user experience"),
        new("business_tool", "ui", "UI", "ui", "user interface"),
        new("business_tool", "seo", "SEO", "seo"),
        new("business_tool", "google_ads", "Google Ads", "google ads", "adwords"),
        new("business_tool", "meta_ads", "Meta Ads", "meta ads", "facebook ads"),

        new("industrial_tool", "plc", "PLC", "plc"),
        new("industrial_tool", "scada", "SCADA", "scada"),
        new("industrial_tool", "siemens", "Siemens", "siemens"),
        new("industrial_tool", "beckhoff", "Beckhoff", "beckhoff"),
        new("industrial_tool", "rockwell", "Rockwell", "rockwell"),

        new("trait", "accuracy", "Dokladnosc", "sorgfaltig", "sorgfaeltig", "careful", "accuracy"),
        new("trait", "responsibility", "Odpowiedzialnosc", "zuverlassig", "zuverlaessig", "reliable"),
        new("trait", "independence", "Samodzielnosc", "eigenstandig", "eigenstaendig", "selbststandig", "selbststaendig"),
        new("trait", "teamwork", "Praca zespolowa", "teamfahig", "teamfaehig", "team player"),

        new("work_activity", "customer_service", "Obsluga klienta", "kundenservice", "kundenberatung", "client support"),
        new("work_activity", "sales", "Sprzedaz", "vertrieb", "verkauf"),
        new("work_activity", "driving", "Prowadzenie pojazdu", "fuhrerschein", "fuehrerschein"),
        new("work_activity", "office_work", "Praca biurowa", "sachbearbeitung", "back office", "office management"),
        new("work_activity", "shift_work", "Praca zmianowa", "schichtarbeit", "shift work"),
        new("work_activity", "accounting", "Ksiegowosc i rozliczenia", "ksiegowosc", "buchhaltung", "accounting", "accounts payable", "payroll", "abrechnung"),
        new("work_activity", "accounting", "Ksiegowosc i rozliczenia", "bilanzbuchhalter", "finanzbuchhalter", "steuerfachangestellte", "rechnungswesen", "deklaracji podatkowych", "sprawozdan finansowych", "jpk", "vat", "cit", "pit"),
        new("work_activity", "hr_development", "HR i rozwoj pracownikow", "personalentwickler", "learning development", "recruitment and hr", "personalberatung", "onboarding", "rekrutacja"),
        new("work_activity", "marketing_pr", "Marketing i PR", "marketing pr", "marketing manager", "kampanie", "newsletter", "produktkommunikation", "social media"),
        new("work_activity", "gastronomy_service", "Obsluga gastronomiczna", "pracownik restauracji", "serwowanie zamowien", "przygotowywanie produktow", "kfc", "gastronomia"),
        new("work_activity", "construction_work", "Prace budowlane", "prace elewacyjne", "ocieplania", "klej i tynki", "gladzie", "plyt gk", "remontowo-budowlanych"),
        new("work_activity", "assembly", "Montaz", "pracownik montazu", "montowac", "montaz", "komponentow", "rysunkow technicznych"),
        new("work_activity", "technical_drawing", "Rysunek techniczny / CAD", "rysunki techniczne", "cad", "nx", "dokumentacji technicznej"),
        new("work_activity", "game_presenting", "Prowadzenie gier / prezentacja online", "game presenter", "live card games", "on-camera", "online players"),
        new("work_activity", "quality_assurance", "Kontrola jakosci", "kontrola jakosci", "quality assurance", "qualitatssicherung", "qualitaetssicherung"),
        new("work_activity", "content_creation", "Tworzenie tresci", "content creator", "video content", "campaign management", "direct marketing"),
        new("work_activity", "ecommerce", "E-commerce", "e-commerce", "ecommerce", "shopify", "marketplace", "amazon marketplace"),
        new("work_activity", "facility_management", "Facility management / technika budynkowa", "facility management", "tga", "hkls", "hlsk", "versorgungstechnik"),
        new("work_activity", "it_support", "Wsparcie IT", "it support", "helpdesk", "1st level support", "anwender support"),

        new("certification", "istqb", "ISTQB", "istqb"),
        new("certification", "driving_license", "Prawo jazdy", "fuhrerschein", "fuehrerschein", "driving license"),

        new("trait", "calm", "Spokój", "spokojny", "spokojna", "opanowanie", "opanowany"),
        new("trait", "patience", "Cierpliwość", "cierpliwosc", "cierpliwy", "cierpliwa"),
        new("trait", "empathy", "Empatia", "empatia", "empatyczny", "empatyczna"),
        new("trait", "communication", "Komunikatywność", "komunikatywnosc", "komunikatywny", "komunikatywna"),
        new("trait", "accuracy", "Dokładność", "dokladnosc", "dokladny", "skrupulatnosc"),
        new("trait", "responsibility", "Odpowiedzialność", "odpowiedzialnosc", "odpowiedzialny"),
        new("trait", "independence", "Samodzielność", "samodzielnosc", "samodzielny"),
        new("trait", "availability", "Dyspozycyjność", "dyspozycyjnosc", "dyspozycyjny"),
        new("trait", "teamwork", "Praca zespołowa", "praca zespolowa", "zespol", "teamwork"),
        new("trait", "ownership", "Odpowiedzialność za zadania", "ownership", "take ownership", "eigenverantwortlich", "verantwortung"),
        new("trait", "problem_solving", "Rozwiązywanie problemów", "problem-solving", "problem solving", "losungsorientiert", "lösungsorientiert"),
        new("trait", "creativity", "Kreatywność", "kreatywn", "creative", "creativity"),

        new("work_activity", "elder_care", "Praca z osobami starszymi", "osoby starsze", "senior care", "opieka nad osobami starszymi"),
        new("work_activity", "customer_service", "Obsługa klienta", "obsluga klienta", "kontakt z klientem", "customer service"),
        new("work_activity", "sales", "Sprzedaż", "sprzedaz", "aktywna sprzedaz", "sales"),
        new("work_activity", "driving", "Prowadzenie pojazdu", "prowadzenie pojazdu", "kierowca", "prawo jazdy"),
        new("work_activity", "physical_work", "Praca fizyczna", "praca fizyczna", "dzwiganie", "manualna"),
        new("work_activity", "office_work", "Praca biurowa", "praca biurowa", "dokumentacja", "administracja"),
        new("work_activity", "shift_work", "Praca zmianowa", "praca zmianowa", "zmiany", "system zmianowy"),

        new("certification", "driving_license_b", "Prawo jazdy kat. B", "prawo jazdy kat b", "prawo jazdy kategorii b", "kat. b"),
        new("certification", "forklift_license", "Uprawnienia na wózki widłowe", "wozki widlowe", "udt", "forklift"),
        new("certification", "sanepid", "Książeczka sanepidowska", "ksiazeczka sanepid", "sanepid"),
        new("certification", "sep", "Uprawnienia SEP", "sep", "uprawnienia elektryczne")
    };

    public static OfferClassification Classify(string? title, string? description, IEnumerable<string> tags)
    {
        var tagList = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var titleAndDescriptionText = $"{title} {description}";
        var normalizedTitleAndDescription = NormalizeForSearch(titleAndDescriptionText);
        var taxonomyTags = FilterTaxonomyTags(tagList, normalizedTitleAndDescription);
        var combinedText = $"{titleAndDescriptionText} {string.Join(' ', taxonomyTags)}";
        var normalizedText = NormalizeForSearch(combinedText);
        var category = FindBest(CategoryRules, normalizedText);
        var role = FindBest(RoleRules, normalizedText);
        var criteria = DetectCriteria(title, description, tagList, LooksLikeItRole(normalizedText) || HasTechContext(normalizedText));

        if (role.Rule is not null && CategoryRules.FirstOrDefault(x => x.Code == role.Rule.CategoryCode) is { } roleCategory)
        {
            category = (roleCategory, Math.Max(category.Score, role.Score), role.Evidence);
        }

        return new OfferClassification
        {
            CategoryCode = category.Rule?.Code ?? "other",
            CategoryName = category.Rule?.DisplayName ?? "Inne",
            RoleCode = role.Rule?.Code,
            RoleName = role.Rule?.DisplayName,
            RoleConfidence = role.Rule is null ? 0m : role.Score,
            RoleEvidence = role.Evidence,
            Criteria = SelectFinalCriteria(criteria),
            CriterionHits = criteria
        };
    }

    private static List<string> FilterTaxonomyTags(IEnumerable<string> tags, string normalizedTitleAndDescription)
    {
        return tags
            .Where(tag => !IsNoisyTaxonomyTag(tag, normalizedTitleAndDescription))
            .ToList();
    }

    private static bool IsNoisyTaxonomyTag(string tag, string normalizedTitleAndDescription)
    {
        var normalizedTag = NormalizeForSearch(tag);
        if (string.IsNullOrWhiteSpace(normalizedTag))
        {
            return true;
        }

        if (normalizedTag is "unknown" or "all others" or "full time" or "full_time" or "part time" or "part_time" or "remote" or "zdalna")
        {
            return true;
        }

        if (normalizedTag is "polska" or "poland" or "podkarpackie" or "rzeszow")
        {
            return true;
        }

        if (normalizedTag.EndsWith(" jobs", StringComparison.OrdinalIgnoreCase) || normalizedTag.EndsWith("-jobs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedTag is "it")
        {
            return !LooksLikeItRole(normalizedTitleAndDescription) && !HasTechContext(normalizedTitleAndDescription);
        }

        return false;
    }

    private static List<OfferCriterion> DetectCriteria(string? title, string? description, IReadOnlyCollection<string> tags, bool globalTechContext)
    {
        var segments = BuildTextSegments(title, description, tags);
        return GetCriterionRules()
            .SelectMany(rule => MatchCriterion(rule, segments, globalTechContext))
            .ToList();
    }

    public static IReadOnlyList<CriterionAliasSeed> GetBootstrapCriterionAliases()
    {
        return CriterionRules
            .SelectMany(rule => rule.Aliases.Select((alias, index) =>
            {
                var normalizedAlias = NormalizeForSearch(alias);
                var isShort = IsShortAmbiguousAlias(normalizedAlias);
                return new CriterionAliasSeed
                {
                    Kind = rule.Kind,
                    Code = rule.Code,
                    DisplayName = rule.DisplayName,
                    Alias = alias,
                    IsShortAmbiguous = isShort,
                    RequiresTechContext = isShort && IsTechnicalCriterion(rule.Kind),
                    RequiresWholeToken = true,
                    Priority = 100 + index
                };
            }))
            .ToList();
    }

    public static void ConfigureCriterionAliases(IEnumerable<CriterionAliasSeed> aliases)
    {
        var rules = aliases
            .Where(alias => alias.IsActive && !string.IsNullOrWhiteSpace(alias.Kind) && !string.IsNullOrWhiteSpace(alias.Code) && !string.IsNullOrWhiteSpace(alias.Alias))
            .GroupBy(alias => $"{alias.Kind}|{alias.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.OrderBy(alias => alias.Priority).First();
                return new CriterionRule(
                    first.Kind,
                    first.Code,
                    string.IsNullOrWhiteSpace(first.DisplayName) ? first.Code : first.DisplayName,
                    group
                        .OrderBy(alias => alias.Priority)
                        .Select(alias => alias.Alias)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            })
            .ToArray();

        DatabaseCriterionRules = rules.Length == 0 ? null : rules;
    }

    private static IReadOnlyList<CriterionRule> GetCriterionRules()
        => DatabaseCriterionRules is { Length: > 0 } ? DatabaseCriterionRules : CriterionRules;

    private static List<OfferCriterion> SelectFinalCriteria(IEnumerable<OfferCriterion> hits)
    {
        return hits
            .GroupBy(match => $"{match.Kind}|{match.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(match => RequirementRank(match.RequirementLevel))
                .ThenByDescending(match => match.Confidence)
                .First())
            .ToList();
    }

    private static IEnumerable<OfferCriterion> MatchCriterion(CriterionRule rule, IReadOnlyCollection<TextSegment> segments, bool globalTechContext)
    {
        foreach (var segment in segments)
        {
            foreach (var alias in rule.Aliases)
            {
                var normalizedAlias = NormalizeForSearch(alias);
                if (IsShortAmbiguousAlias(normalizedAlias) && !HasTechContext(segment.NormalizedText))
                {
                    continue;
                }

                if (!ContainsPhrase(segment.NormalizedText, normalizedAlias))
                {
                    continue;
                }

                if (rule.Kind == "ai_tool" && rule.Code == "ai_ml" && IsAiDisclaimerOnly(segment.NormalizedText, normalizedAlias))
                {
                    continue;
                }

                var requirementLevel = ResolveRequirementLevel(rule, segment, normalizedAlias, globalTechContext);
                var (evidenceStart, evidenceEnd) = FindEvidenceBounds(segment.Text, alias);
                yield return new OfferCriterion
                {
                    Kind = rule.Kind,
                    Code = rule.Code,
                    DisplayName = rule.DisplayName,
                    RequirementLevel = requirementLevel,
                    IsRequired = requirementLevel == RequiredLevel,
                    Confidence = ResolveConfidence(rule, requirementLevel, segment, normalizedAlias),
                    Evidence = BuildEvidence(segment, alias),
                    SourceField = segment.Source,
                    SourceSection = segment.Section,
                    EvidenceStart = evidenceStart,
                    EvidenceEnd = evidenceEnd,
                    MatchedAlias = alias,
                    ExtractorVersion = ExtractorVersion
                };

                break;
            }
        }
    }

    private static (T? Rule, decimal Score, string? Evidence) FindBest<T>(IEnumerable<T> rules, string normalizedText)
        where T : TaxonomyRule
    {
        return rules
            .Select(rule =>
            {
                var matches = rule.Aliases
                    .Where(alias => ContainsPhrase(normalizedText, NormalizeForSearch(alias)))
                    .ToList();
                var score = matches.Count == 0 ? 0m : Math.Min(0.95m, 0.55m + (matches.Count * 0.1m));
                return (Rule: (T?)rule, Score: score, Evidence: matches.FirstOrDefault());
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .FirstOrDefault();
    }

    private static List<TextSegment> BuildTextSegments(string? title, string? description, IReadOnlyCollection<string> tags)
    {
        var segments = new List<TextSegment>();

        if (!string.IsNullOrWhiteSpace(title))
        {
            segments.Add(CreateSegment("title", "title", title, MentionedLevel));
        }

        foreach (var tag in tags)
        {
            segments.Add(CreateSegment("tag", "tag", tag, MentionedLevel));
        }

        var currentLevel = MentionedLevel;
        foreach (var rawLine in PrepareDescriptionLines(description))
        {
            var line = StripListMarker(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var normalizedLine = NormalizeForSearch(line);
            var header = DetectHeaderLevel(normalizedLine);
            if (header is not null)
            {
                currentLevel = header;
                var inlineText = ExtractInlineTextAfterHeader(line);
                if (!string.IsNullOrWhiteSpace(inlineText))
                {
                    segments.Add(CreateSegment("description", currentLevel, inlineText, currentLevel));
                }

                continue;
            }

            segments.Add(CreateSegment("description", currentLevel, line, currentLevel));
        }

        return segments;
    }

    private static IEnumerable<string> PrepareDescriptionLines(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            yield break;
        }

        var text = System.Net.WebUtility.HtmlDecode(description);
        text = Regex.Replace(text, @"</(li|p|div|br|h[1-6]|ul|ol)>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"<(li|p|div|br|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, "<.*?>", " ");
        text = text.Replace("\r", "\n")
            .Replace("•", "\n•")
            .Replace("●", "\n•")
            .Replace("▪", "\n•")
            .Replace("–", "\n-")
            .Replace("—", "\n-");

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedLine = Regex.Replace(line, @"\s{2,}", " ").Trim();
            if (normalizedLine.Length > 0)
            {
                yield return normalizedLine;
            }
        }
    }

    private static TextSegment CreateSegment(string source, string section, string text, string requirementLevel)
    {
        return new TextSegment(source, section, text, NormalizeForSearch(text), requirementLevel);
    }

    private static string StripListMarker(string line)
    {
        return Regex.Replace(line.Trim(), @"^([\-*•●▪]|\d+[.)])\s*", string.Empty, RegexOptions.CultureInvariant).Trim();
    }

    private static string? DetectHeaderLevel(string normalizedLine)
    {
        var headerCandidate = normalizedLine.Trim().Trim(':', '-', '.');
        if (headerCandidate.Length > 120)
        {
            headerCandidate = headerCandidate[..120];
        }

        if (OptionalHeader.IsMatch(headerCandidate))
        {
            return OptionalLevel;
        }

        if (RequiredHeader.IsMatch(headerCandidate))
        {
            return RequiredLevel;
        }

        if (NeutralHeader.IsMatch(headerCandidate))
        {
            return MentionedLevel;
        }

        return null;
    }

    private static string ExtractInlineTextAfterHeader(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0 || colonIndex >= line.Length - 1)
        {
            return string.Empty;
        }

        return line[(colonIndex + 1)..].Trim();
    }

    private static string ResolveRequirementLevel(CriterionRule rule, TextSegment segment, string normalizedEvidence, bool globalTechContext)
    {
        var evidenceWindow = BuildEvidenceWindow(segment.NormalizedText, normalizedEvidence);

        if (!CompensationBonusCue.IsMatch(evidenceWindow) && OptionalCue.IsMatch(evidenceWindow))
        {
            return OptionalLevel;
        }

        if (RequiredCue.IsMatch(evidenceWindow))
        {
            return RequiredLevel;
        }

        if (segment.RequirementLevel == RequiredLevel || segment.RequirementLevel == OptionalLevel)
        {
            return segment.RequirementLevel;
        }

        if (segment.Source == "tag" && IsTechnicalCriterion(rule.Kind) && globalTechContext)
        {
            return RequiredLevel;
        }

        if (IsTechnicalCriterion(rule.Kind) && segment.Source == "title" && LooksLikeItRole(segment.NormalizedText))
        {
            return RequiredLevel;
        }

        if (IsTechnicalCriterion(rule.Kind) && StackCue.IsMatch(segment.NormalizedText))
        {
            return RequiredLevel;
        }

        if (rule.Kind == "certification" && segment.Source == "title")
        {
            return RequiredLevel;
        }

        return MentionedLevel;
    }

    private static string BuildEvidenceWindow(string normalizedText, string normalizedEvidence)
    {
        var index = normalizedText.IndexOf(normalizedEvidence, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return normalizedText;
        }

        var start = Math.Max(0, index - 120);
        var end = Math.Min(normalizedText.Length, index + normalizedEvidence.Length + 120);
        return normalizedText[start..end];
    }

    private static decimal ResolveConfidence(CriterionRule rule, string requirementLevel, TextSegment segment, string normalizedAlias)
    {
        var baseScore = requirementLevel switch
        {
            RequiredLevel => 0.92m,
            OptionalLevel => 0.85m,
            MentionedLevel => 0.62m,
            _ => 0.50m
        };

        var score = segment.Source switch
        {
            "title" when IsTechnicalCriterion(rule.Kind) && LooksLikeItRole(segment.NormalizedText) => 0.85m,
            "title" => 0.70m,
            "tag" => 0.60m,
            _ => baseScore
        };

        if (rule.Kind is "trait" or "work_activity")
        {
            score = Math.Min(score, requirementLevel == RequiredLevel ? 0.72m : 0.62m);
        }

        return IsShortAmbiguousAlias(normalizedAlias)
            ? Math.Min(score, 0.75m)
            : score;
    }

    private static int RequirementRank(string requirementLevel)
    {
        return requirementLevel switch
        {
            RequiredLevel => 3,
            OptionalLevel => 2,
            MentionedLevel => 1,
            _ => 0
        };
    }

    private static string BuildEvidence(TextSegment segment, string alias)
    {
        var text = segment.Text.Length <= 430 ? segment.Text : segment.Text[..430].Trim() + "...";
        return $"{segment.Source}/{segment.Section}: {alias} | {text}";
    }

    private static bool IsTechnicalCriterion(string kind)
    {
        return kind is "technology" or "programming_language" or "framework" or "database" or "cloud" or "devops_tool" or "testing_tool" or "data_tool" or "ai_tool" or "integration_tool" or "business_tool" or "industrial_tool";
    }

    private static bool IsShortAmbiguousAlias(string normalizedAlias)
    {
        return normalizedAlias is "go" or "js" or "ts" or "r" or "c" or "ad";
    }

    private static bool HasTechContext(string normalizedText)
    {
        return TechContextCue.IsMatch(normalizedText);
    }

    private static bool LooksLikeItRole(string normalizedText)
    {
        return ItRoleTitleCue.IsMatch(normalizedText);
    }

    private static bool IsAiDisclaimerOnly(string normalizedText, string normalizedAlias)
    {
        if (!AiDisclaimerCue.IsMatch(normalizedText))
        {
            return false;
        }

        return !Regex.IsMatch(normalizedText, @"\b(machine learning engineer|ml engineer|ai engineer|ai/ml engineer|llm|genai|generative ai|model training|uczenie maszynowe|sztuczna inteligencja|deep learning|computer vision|nlp)\b");
    }

    private static (int? Start, int? End) FindEvidenceBounds(string text, string alias)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(alias))
        {
            return (null, null);
        }

        var index = text.IndexOf(alias, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? (null, null) : (index, index + alias.Length);
    }

    private static bool ContainsPhrase(string normalizedText, string normalizedPhrase)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhrase))
        {
            return false;
        }

        if (normalizedPhrase.Any(char.IsLetterOrDigit))
        {
            var safePhrase = Regex.Escape(normalizedPhrase).Replace(@"\ ", @"\s+");
            return Regex.IsMatch(normalizedText, $@"(^|[^a-z0-9+#.]){safePhrase}($|[^a-z0-9+#.])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return normalizedText.Contains(normalizedPhrase, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }

    private record TaxonomyRule(string Code, string DisplayName, params string[] Aliases);

    private sealed record RoleRule(string Code, string DisplayName, string CategoryCode, params string[] Aliases)
        : TaxonomyRule(Code, DisplayName, Aliases);

    private sealed record CriterionRule(string Kind, string Code, string DisplayName, params string[] Aliases);

    private sealed record TextSegment(string Source, string Section, string Text, string NormalizedText, string RequirementLevel);
}
