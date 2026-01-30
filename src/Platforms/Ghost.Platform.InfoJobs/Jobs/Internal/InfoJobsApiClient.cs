using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ghost.Contracts.Jobs;

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

    public InfoJobsApiClient(HttpClient http, InfoJobsOptions options, ILogger<InfoJobsApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? new InfoJobsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        return $"{_options.ApiEndpoint}9/offer?{queryString}";
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

            var company = offer.GetProperty("profile").GetProperty("name").GetString() ?? string.Empty;
            var location = offer.GetProperty("province").GetProperty("value").GetString() ?? string.Empty;
            var id = offer.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
            
            // Parse salary information
            string? salary = null;
            if (offer.TryGetProperty("minPay", out var minPay) && minPay.ValueKind != JsonValueKind.Null)
            {
                var amount = minPay.GetProperty("amountValue").GetString();
                var period = minPay.GetProperty("periodValue").GetString();
                if (!string.IsNullOrEmpty(amount) && !string.IsNullOrEmpty(period))
                    salary = $"{amount} {period}";
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

            // Parse description
            var description = offer.GetProperty("description").GetString() ?? string.Empty;
            
            // Parse publication date
            DateTimeOffset postedAt = DateTimeOffset.UtcNow;
            if (offer.TryGetProperty("updateDate", out var updateDate))
            {
                var dateStr = updateDate.GetString();
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
                Salary = salary,
                JobType = jobType,
                PostedAt = postedAt,
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