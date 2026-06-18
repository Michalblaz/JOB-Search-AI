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
    /// <summary>
    /// Ocenia dopasowanie ofert pracy do preferencji użytkownika przy użyciu modelu Gemini.
    /// </summary>
    /// <remarks>
    /// Usługa działa jako opcjonalne rozszerzenie lokalnego rankingu z <see cref="JobSearchService.CalculateMatchingScore"/>.
    /// Jeżeli klucz API nie jest skonfigurowany albo zewnętrzne API zwróci błąd, aplikacja może nadal używać lokalnych wyników.
    /// </remarks>
    /// <seealso cref="JobSearchService"/>
    /// <seealso cref="JobOffer"/>
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

        /// <summary>
        /// Tworzy usługę scoringu AI na podstawie ustawień aplikacji.
        /// </summary>
        /// <param name="settingsProvider">Provider konfiguracji zawierający klucz API, model i rozmiar paczki zapytań.</param>
        public GeminiMatchService(AppSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        /// <summary>
        /// Ostatni błąd zwrócony przez usługę AI albo parser odpowiedzi.
        /// </summary>
        /// <value>Pusty tekst oznacza, że ostatnia próba zakończyła się sukcesem albo nie była wykonywana.</value>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Informuje, czy w konfiguracji dostępny jest klucz Gemini.
        /// </summary>
        /// <value><see langword="true"/>, gdy można próbować zewnętrznego scoringu AI.</value>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settingsProvider.GetSettings().Gemini.ApiKey);

        /// <summary>
        /// Wysyła oferty do Gemini i zapisuje wynik oraz uzasadnienie bezpośrednio w obiektach <see cref="JobOffer"/>.
        /// </summary>
        /// <param name="searchService">Aktualny stan wyszukiwania używany do zbudowania kontekstu preferencji użytkownika.</param>
        /// <param name="offers">Lista ofert, z której zostanie oceniona maksymalna liczba skonfigurowana w ustawieniach.</param>
        /// <returns><see langword="true"/>, gdy przynajmniej jedna paczka ofert została oceniona poprawnie.</returns>
        /// <remarks>
        /// Metoda dzieli oferty na paczki i ponawia zapytania przy limitach lub chwilowej niedostępności API. Odpowiedź modelu
        /// musi być parsowalnym JSON-em, ponieważ UI potrzebuje stabilnych pól <c>offerId</c>, <c>score</c> i <c>reason</c>.
        /// </remarks>
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

        private static string BuildPrompt(JobSearchService searchService, IReadOnlyList<GeminiOfferInput> offers)
        {
            var selectedLanguages = searchService.LanguageFilters
                .Where(item => item.Value)
                .Select(item => item.Key)
                .ToList();

            var prompt = new
            {
                instruction = "Oceń dopasowanie ofert pracy do preferencji użytkownika. Zwróć wyłącznie tablicę JSON. Każdy element ma mieć pola: offerId, score, reason. score od 0 do 100. reason ma być krótkim uzasadnieniem po polsku. Jeśli stanowisko lub branża nie pasują do głównego słowa kluczowego użytkownika, przyznaj niski wynik.",
                userProfile = new
                {
                    searchText = searchService.SearchText,
                    location = searchService.Location,
                    remoteOnly = searchService.RemoteOnly,
                    minSalaryHourlyPln = searchService.MinSalary,
                    maxSalaryHourlyPln = searchService.MaxSalary,
                    jobRange = searchService.JobRange,
                    selectedExperience = searchService.SelectedExperience,
                    selectedEducation = searchService.SelectedEducation,
                    languages = selectedLanguages,
                    profileJobTitle = searchService.CurrentUser.Settings.JobTitle,
                    profileExpectedSalary = searchService.CurrentUser.Settings.ExpectedSalary,
                    profileExperience = searchService.CurrentUser.Settings.Experience,
                    profileEducation = searchService.CurrentUser.Settings.Education,
                    profileKnownLanguages = searchService.CurrentUser.Settings.KnownLanguages
                },
                offers
            };

            return JsonConvert.SerializeObject(prompt, Formatting.Indented);
        }

        private static string TrimOfferDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            const int maxLength = 700;
            var normalized = description.Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized[..maxLength] + "...";
        }

        private static string TrimForMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            const int maxLength = 220;
            var normalized = text.Trim().Replace(Environment.NewLine, " ");
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }
    }

    public class GeminiRequest
    {
        [JsonProperty("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonProperty("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; set; } = new();
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

    public class GeminiGenerationConfig
    {
        [JsonProperty("responseMimeType")]
        public string ResponseMimeType { get; set; } = "application/json";

        [JsonProperty("temperature")]
        public double Temperature { get; set; }
    }

    public class GeminiResponse
    {
        [JsonProperty("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
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
        public string? Reason { get; set; }
    }

    public class GeminiOfferInput
    {
        public string OfferId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Salary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Experience { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string ContractType { get; set; } = string.Empty;
        public string ContractTime { get; set; } = string.Empty;
        public List<string> Languages { get; set; } = new();
        public string Source { get; set; } = string.Empty;
        public bool IsRemote { get; set; }
    }
}
