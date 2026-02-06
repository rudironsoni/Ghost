using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.InfoJobs.Jobs.Internal;

public sealed class InfoJobsApiClient
{
    private readonly HttpClient _http;
    private readonly InfoJobsOptions _options;
    private readonly ILogger<InfoJobsApiClient> _logger;

    private static readonly Action<ILogger, string, Exception?> LogFetchingJobs =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogFetchingJobs)), "Fetching InfoJobs from: {Url}");

    private static readonly Action<ILogger, string, Exception?> LogReceivedEmptyResponse =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, nameof(LogReceivedEmptyResponse)), "Received empty response from InfoJobs for url {Url}");

    private static readonly Action<ILogger, int, Exception?> LogReceivedResponse =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(LogReceivedResponse)), "Received response: {Length} bytes");

    private static readonly Action<ILogger, string, Exception?> LogParsingError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, nameof(LogParsingError)), "Error parsing InfoJobs response: {Error}");

    private static readonly Action<ILogger, int, Exception?> LogParsedJobs =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(5, nameof(LogParsedJobs)), "Parsed {Count} jobs from InfoJobs");

    private static readonly Action<ILogger, int, string, Exception?> LogHttpResponse =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(6, nameof(LogHttpResponse)), "InfoJobs API response: StatusCode={StatusCode}, Body={Body}");

    private static readonly Action<ILogger, Exception?> LogMissingCredentials =
        LoggerMessage.Define(LogLevel.Warning, new EventId(7, nameof(LogMissingCredentials)), "InfoJobs API credentials (ClientId/ClientSecret) are missing. API requires valid credentials. Add them to .env: GHOST__EXTENSIONS__INFOJOBS__CLIENTID and GHOST__EXTENSIONS__INFOJOBS__CLIENTSECRET");

    public InfoJobsApiClient(HttpClient http, InfoJobsOptions options, ILogger<InfoJobsApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? new InfoJobsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Log warning if credentials are missing
        if (string.IsNullOrEmpty(_options.ClientId) || string.IsNullOrEmpty(_options.ClientSecret))
        {
            LogMissingCredentials(_logger, null);
        }
    }

    public async Task<IReadOnlyList<JobListing>> SearchAsync(string query, string location, CancellationToken ct = default)
    {
        // Build search URL with parameters
        var url = BuildSearchUrl(query, location);
        LogFetchingJobs(_logger, url, null);

        var req = new HttpRequestMessage(HttpMethod.Get, url);

        // Set authentication headers (Basic Auth with Client ID/Secret)
        if (!string.IsNullOrEmpty(_options.ClientId) && !string.IsNullOrEmpty(_options.ClientSecret))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        // Add InfoJobs API headers
        foreach (var header in InfoJobsConstants.ApiHeaders)
        {
            req.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Log HTTP status and response for debugging
            var statusCode = (int)res.StatusCode;
            var bodyPreview = json.Length > 500 ? json[..500] + "..." : json;
            LogHttpResponse(_logger, statusCode, bodyPreview, null);

            if (string.IsNullOrEmpty(json))
            {
                LogReceivedEmptyResponse(_logger, url, null);
                return Array.Empty<JobListing>();
            }

            LogReceivedResponse(_logger, json.Length, null);

            // Parse JSON response
            var jobs = ParseInfoJobsResponse(json);
            LogParsedJobs(_logger, jobs.Count, null);

            return jobs;
        }
        catch (Exception ex)
        {
            LogParsingError(_logger, ex.Message, ex);
            return Array.Empty<JobListing>();
        }
    }

    private string BuildSearchUrl(string query, string location)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrEmpty(query))
            parameters.Add($"q={Uri.EscapeDataString(query)}");

        if (!string.IsNullOrEmpty(location))
            parameters.Add($"province={Uri.EscapeDataString(location)}");

        // Add Spanish locale
        parameters.Add($"locale={_options.Language}");

        // Limit results for efficiency
        parameters.Add("maxResults=50");

        var queryString = string.Join("&", parameters);
        return $"{_options.ApiEndpoint}1/offer?{queryString}";
    }

    private IReadOnlyList<JobListing> ParseInfoJobsResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("offers", out var offersArray) ||
                offersArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<JobListing>();
            }

            var jobs = new List<JobListing>();

            foreach (var offer in offersArray.EnumerateArray())
            {
                var job = ParseJobOffer(offer);
                if (job != null)
                    jobs.Add(job);
            }

            return jobs;
        }
        catch (Exception ex)
        {
            LogParsingError(_logger, $"Failed to parse InfoJobs JSON: {ex.Message}", ex);
            return Array.Empty<JobListing>();
        }
    }

    private JobListing? ParseJobOffer(JsonElement offer)
    {
        try
        {
            var title = offer.GetProperty("title").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                return null;

            // Parse company from author field (correct per InfoJobs API)
            var company = offer.GetProperty("author").GetProperty("name").GetString() ?? string.Empty;

            // Parse location (optional city + mandatory province)
            var location = string.Empty;
            if (offer.TryGetProperty("city", out var city))
            {
                var cityValue = city.GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(cityValue))
                    location = cityValue;
            }

            if (offer.TryGetProperty("province", out var province))
            {
                var provinceValue = province.GetProperty("value").GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(provinceValue))
                {
                    if (!string.IsNullOrEmpty(location))
                        location += $", {provinceValue}";
                    else
                        location = provinceValue;
                }
            }

            var id = offer.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();

            // Parse URL (link field)
            string? url = null;
            if (offer.TryGetProperty("link", out var link))
            {
                url = link.GetString();
            }

            // Parse salary information (salaryMin/salaryMax per InfoJobs API)
            string? salary = null;
            var hasSalaryMin = offer.TryGetProperty("salaryMin", out var salaryMin) && salaryMin.ValueKind != JsonValueKind.Null;
            var hasSalaryMax = offer.TryGetProperty("salaryMax", out var salaryMax) && salaryMax.ValueKind != JsonValueKind.Null;

            if (hasSalaryMin)
            {
                var minAmount = salaryMin.GetProperty("value").GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(minAmount))
                {
                    if (hasSalaryMax)
                    {
                        var maxAmount = salaryMax.GetProperty("value").GetString() ?? string.Empty;
                        salary = $"{minAmount} - {maxAmount}";
                    }
                    else
                    {
                        salary = minAmount;
                    }
                }
            }
            else if (hasSalaryMax)
            {
                salary = salaryMax.GetProperty("value").GetString() ?? string.Empty;
            }

            // Parse job type from contract type
            JobType jobType = JobType.Unknown;
            if (offer.TryGetProperty("contractType", out var contractType))
            {
                var contractValue = contractType.GetProperty("value").GetString()?.ToLowerInvariant() ?? string.Empty;

                foreach (var mapping in InfoJobsConstants.JobTypeMapping)
                {
                    if (contractValue.Contains(mapping.Key))
                    {
                        jobType = mapping.Value;
                        break;
                    }
                }
            }

            // Parse description from requirementMin field (per InfoJobs API)
            var description = string.Empty;
            if (offer.TryGetProperty("requirementMin", out var requirementMin))
            {
                description = requirementMin.GetString() ?? string.Empty;
            }

            // Parse publication date (updated field per InfoJobs API)
            DateTimeOffset postedAt = DateTimeOffset.UtcNow;
            if (offer.TryGetProperty("updated", out var updated))
            {
                var dateStr = updated.GetString();
                if (!string.IsNullOrEmpty(dateStr) && DateTimeOffset.TryParse(dateStr, out var dt))
                    postedAt = dt;
            }

            return new JobListing
            {
                Id = id,
                Title = title,
                Company = company,
                Location = location,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Salary = string.IsNullOrWhiteSpace(salary) ? null : salary,
                JobType = jobType,
                PostedAt = postedAt,
                Url = url,
                Source = "InfoJobs"
            };
        }
        catch (Exception ex)
        {
            LogParsingError(_logger, $"Failed to parse individual job offer: {ex.Message}", ex);
            return null;
        }
    }
}
