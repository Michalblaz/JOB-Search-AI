using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MauiApp1.testowe
{
    public class GeminiMatchService
    {
        private const string EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4)
        };

        private readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        private readonly AppSettingsProvider _settingsProvider;

        public GeminiMatchService(AppSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        public string LastError { get; private set; } = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settingsProvider.GetSettings().Gemini.ApiKey);

        public async Task<bool> ScoreOffersAsync(JobSearchService searchService, IReadOnlyList<JobOffer> offers)
        {
            LastError = string.Empty;
            var settings = _settingsProvider.GetSettings().Gemini;
            if (string.IsNullOrWhiteSpace(settings.ApiKey) || offers.Count == 0)
            {
                LastError = "Brak klucza Gemini albo brak ofert do oceny.";
                return false;
            }

            var limitedOffers = offers
                .Take(Math.Max(1, settings.MaxOffersPerRequest))
                .Select((offer, index) => new GeminiOfferInput
                {
                    OfferId = $"offer-{index + 1}",
                    Title = offer.Title,
                    Company = offer.Company,
                    Location = offer.Location,
                    Salary = offer.SalaryDetails,
                    Description = TrimOfferDescription(offer.Description),
                    Experience = offer.Experience,
                    Education = offer.Education,
                    ContractType = offer.ContractType,
                    ContractTime = offer.ContractTime,
                    Languages = offer.Languages,
                    Source = offer.Source,
                    IsRemote = offer.IsRemote
                })
                .ToList();

            var url = string.Format(
                EndpointTemplate,
                Uri.EscapeDataString(settings.Model),
                Uri.EscapeDataString(settings.ApiKey));

            var batchSize = Math.Max(1, settings.BatchSize);
            var hasAnySuccess = false;
            var lastBatchError = string.Empty;

            foreach (var batch in limitedOffers.Chunk(batchSize))
            {
                var request = new GeminiRequest
                {
                    Contents = new List<GeminiContent>
                    {
                        new()
                        {
                            Parts = new List<GeminiPart>
                            {
                                new()
                                {
                                    Text = BuildPrompt(searchService, batch.ToList())
                                }
                            }
                        }
                    },
                    GenerationConfig = new GeminiGenerationConfig
                    {
                        ResponseMimeType = "application/json",
                        Temperature = 0.2
                    }
                };

                var (isSuccess, body, statusCode) = await PostWithRetryAsync(url, request);
                if (!isSuccess)
                {
                    lastBatchError = $"Gemini zwróciło {(int)statusCode}: {TrimForMessage(body)}";
                    continue;
                }

                var geminiResponse = JsonConvert.DeserializeObject<GeminiResponse>(body);
                var jsonText = geminiResponse?.Candidates?
                    .SelectMany(candidate => candidate.Content?.Parts ?? new List<GeminiPart>())
                    .Select(part => part.Text)
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    lastBatchError = "Gemini nie zwróciło treści z oceną ofert.";
                    continue;
                }

                var scores = ParseScores(jsonText);
                if (!scores.Any())
                {
                    lastBatchError = $"Gemini zwróciło odpowiedź, ale nie udało się odczytać ocen. Odpowiedź: {TrimForMessage(jsonText)}";
                    continue;
                }

                var scoreMap = scores
                    .Where(item => !string.IsNullOrWhiteSpace(item.OfferId))
                    .ToDictionary(item => item.OfferId!, item => item, StringComparer.OrdinalIgnoreCase);

                if (!scoreMap.Any())
                {
                    lastBatchError = "Gemini nie zwróciło żadnych poprawnych identyfikatorów ofert.";
                    continue;
                }

                foreach (var sourceOffer in batch)
                {
                    if (!scoreMap.TryGetValue(sourceOffer.OfferId, out var score))
                    {
                        continue;
                    }

                    var offerIndex = ParseOfferIndex(sourceOffer.OfferId);
                    if (offerIndex < 0 || offerIndex >= offers.Count)
                    {
                        continue;
                    }

                    var targetOffer = offers[offerIndex];
                    targetOffer.MatchScore = Math.Clamp(score.Score, 0, 100);
                    targetOffer.MatchReason = string.IsNullOrWhiteSpace(score.Reason)
                        ? "Gemini nie zwróciło uzasadnienia."
                        : score.Reason.Trim();
                    targetOffer.HasAiMatchScore = true;
                    hasAnySuccess = true;
                }
            }

            LastError = hasAnySuccess ? string.Empty : lastBatchError;
            return hasAnySuccess;
        }

        private async Task<(bool IsSuccess, string Body, HttpStatusCode StatusCode)> PostWithRetryAsync(string url, GeminiRequest request)
        {
            string body = string.Empty;
            HttpStatusCode lastStatusCode = 0;

            for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
            {
                using var content = new StringContent(
                    JsonConvert.SerializeObject(request),
                    Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.PostAsync(url, content);
                body = await ReadUtf8Async(response);
                lastStatusCode = response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    return (true, body, response.StatusCode);
                }

                var shouldRetry = response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                  (int)response.StatusCode == 429;

                if (!shouldRetry || attempt >= RetryDelays.Length)
                {
                    return (false, body, response.StatusCode);
                }

                await Task.Delay(RetryDelays[attempt]);
            }

            return (false, body, lastStatusCode);
        }

        private static int ParseOfferIndex(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId) || !offerId.StartsWith("offer-", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            return int.TryParse(offerId["offer-".Length..], out var parsedIndex)
                ? parsedIndex - 1
                : -1;
        }

        private static List<GeminiOfferScore> ParseScores(string jsonText)
        {
            var cleaned = jsonText.Trim()
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            try
            {
                var directArray = JsonConvert.DeserializeObject<List<GeminiOfferScore>>(cleaned);
                if (directArray?.Any() == true)
                {
                    return directArray;
                }
            }
            catch
            {
            }

            try
            {
                var token = JToken.Parse(cleaned);
                if (token is JArray array)
                {
                    return array.ToObject<List<GeminiOfferScore>>() ?? new List<GeminiOfferScore>();
                }

                if (token is JObject obj)
                {
                    var wrappedArray = obj["scores"] ?? obj["results"] ?? obj["offers"] ?? obj["items"];
                    if (wrappedArray is JArray resultArray)
                    {
                        return resultArray.ToObject<List<GeminiOfferScore>>() ?? new List<GeminiOfferScore>();
                    }
                }
            }
            catch
            {
            }

            return new List<GeminiOfferScore>();
        }

        private static async Task<string> ReadUtf8Async(HttpResponseMessage response)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return Encoding.UTF8.GetString(bytes);
        }

        private static string TrimForMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 220 ? normalized : normalized[..220] + "...";
        }

        private static string TrimOfferDescription(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 420 ? normalized : normalized[..420] + "...";
        }

        private static string BuildPrompt(JobSearchService searchService, List<GeminiOfferInput> offers)
        {
            var selectedLanguages = searchService.LanguageFilters
                .Where(item => item.Value)
                .Select(item => item.Key)
                .ToList();

            var profileLanguages = searchService.CurrentUser.Settings.KnownLanguages
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preferredContracts = searchService.CurrentUser.Settings.PreferredContractTypes
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            var payload = new GeminiPromptPayload
            {
                UserContext = new GeminiUserContext
                {
                    SearchText = searchService.SearchText,
                    Location = searchService.Location,
                    DistanceKm = searchService.Distance,
                    RemoteOnly = searchService.RemoteOnly,
                    MinimumHourlyRatePln = searchService.MinSalary,
                    MaximumHourlyRatePln = searchService.MaxSalary,
                    JobRange = searchService.JobRange,
                    Experience = searchService.SelectedExperience,
                    Education = searchService.SelectedEducation,
                    SelectedLanguages = selectedLanguages,
                    ProfileJobTitle = searchService.CurrentUser.Settings.JobTitle,
                    ProfileExpectedSalary = searchService.CurrentUser.Settings.ExpectedSalary,
                    ProfileExperience = searchService.CurrentUser.Settings.Experience,
                    ProfileEducation = searchService.CurrentUser.Settings.Education,
                    ProfileKnownLanguages = profileLanguages,
                    PreferredContracts = preferredContracts
                },
                Offers = offers
            };

            return
                """
                Oceń dopasowanie ofert pracy do preferencji użytkownika.
                Zwróć wyłącznie poprawny JSON jako tablicę obiektów bez markdownu i bez komentarzy.
                Każdy obiekt ma mieć dokładnie pola:
                - offerId
                - score
                - reason

                Zasady:
                - score to liczba całkowita 0-100
                - reason napisz po polsku, maksymalnie 2 krótkie zdania
                - jeśli w ofercie brakuje danych, potraktuj to neutralnie, nie jako automatyczny minus
                - uwzględnij tytuł, lokalizację, zdalność, widełki, doświadczenie, wykształcenie, języki i typ pracy
                - jeśli oferta dobrze pasuje do kilku kryteriów naraz, wynik powinien być wyraźnie wyższy
                - jeśli oferta rozmija się z ważnymi kryteriami użytkownika, wynik obniż

                Dane wejściowe:
                """ + Environment.NewLine + JsonConvert.SerializeObject(payload, Formatting.Indented);
        }
    }

    public class GeminiPromptPayload
    {
        [JsonProperty("userContext")]
        public GeminiUserContext UserContext { get; set; } = new();

        [JsonProperty("offers")]
        public List<GeminiOfferInput> Offers { get; set; } = new();
    }

    public class GeminiUserContext
    {
        [JsonProperty("searchText")]
        public string SearchText { get; set; } = string.Empty;

        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("distanceKm")]
        public int DistanceKm { get; set; }

        [JsonProperty("remoteOnly")]
        public bool RemoteOnly { get; set; }

        [JsonProperty("minimumHourlyRatePln")]
        public int MinimumHourlyRatePln { get; set; }

        [JsonProperty("maximumHourlyRatePln")]
        public int MaximumHourlyRatePln { get; set; }

        [JsonProperty("jobRange")]
        public string JobRange { get; set; } = string.Empty;

        [JsonProperty("experience")]
        public string Experience { get; set; } = string.Empty;

        [JsonProperty("education")]
        public string Education { get; set; } = string.Empty;

        [JsonProperty("selectedLanguages")]
        public List<string> SelectedLanguages { get; set; } = new();

        [JsonProperty("profileJobTitle")]
        public string ProfileJobTitle { get; set; } = string.Empty;

        [JsonProperty("profileExpectedSalary")]
        public string ProfileExpectedSalary { get; set; } = string.Empty;

        [JsonProperty("profileExperience")]
        public string ProfileExperience { get; set; } = string.Empty;

        [JsonProperty("profileEducation")]
        public string ProfileEducation { get; set; } = string.Empty;

        [JsonProperty("profileKnownLanguages")]
        public List<string> ProfileKnownLanguages { get; set; } = new();

        [JsonProperty("preferredContracts")]
        public List<string> PreferredContracts { get; set; } = new();
    }

    public class GeminiOfferInput
    {
        [JsonProperty("offerId")]
        public string OfferId { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("company")]
        public string Company { get; set; } = string.Empty;

        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("salary")]
        public string Salary { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("experience")]
        public string Experience { get; set; } = string.Empty;

        [JsonProperty("education")]
        public string Education { get; set; } = string.Empty;

        [JsonProperty("contractType")]
        public string ContractType { get; set; } = string.Empty;

        [JsonProperty("contractTime")]
        public string ContractTime { get; set; } = string.Empty;

        [JsonProperty("languages")]
        public List<string> Languages { get; set; } = new();

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("isRemote")]
        public bool IsRemote { get; set; }
    }

    public class GeminiRequest
    {
        [JsonProperty("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonProperty("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; set; } = new();
    }

    public class GeminiGenerationConfig
    {
        [JsonProperty("responseMimeType")]
        public string ResponseMimeType { get; set; } = "application/json";

        [JsonProperty("temperature")]
        public double Temperature { get; set; } = 0.2;
    }

    public class GeminiContent
    {
        [JsonProperty("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class GeminiResponse
    {
        [JsonProperty("candidates")]
        public List<GeminiCandidate> Candidates { get; set; } = new();
    }

    public class GeminiCandidate
    {
        [JsonProperty("content")]
        public GeminiContent? Content { get; set; }
    }

    public class GeminiOfferScore
    {
        [JsonProperty("offerId")]
        public string? OfferId { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
