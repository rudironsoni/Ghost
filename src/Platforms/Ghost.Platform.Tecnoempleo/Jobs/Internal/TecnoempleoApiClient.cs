using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Threading;

namespace Ghost.Platform.Tecnoempleo.Jobs.Internal;

public class TecnoempleoApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TecnoempleoOptions _options;
    private readonly ILogger<TecnoempleoApiClient> _logger;

    public TecnoempleoApiClient(HttpClient httpClient, TecnoempleoOptions options, ILogger<TecnoempleoApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static readonly Action<ILogger, string, Exception?> LogSearchError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, nameof(LogSearchError)), "Error searching Tecnoempleo jobs for query: {Query}");

    private static readonly Action<ILogger, string, Exception?> LogDetailsError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, nameof(LogDetailsError)), "Error fetching Tecnoempleo job details for ID: {JobId}");

    private static readonly Action<ILogger, string, long, Exception?> LogApiRequestTiming =
        LoggerMessage.Define<string, long>(LogLevel.Debug, new EventId(3, nameof(LogApiRequestTiming)), "Tecnoempleo API {Operation} completed in {ElapsedMs}ms");

    private static readonly Action<ILogger, int, Exception?> LogRateLimitHit =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(4, nameof(LogRateLimitHit)), "Rate limit reached, waiting {DelayMs}ms");

    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _requestsThisMinute;
    private int _requestsThisHour;
    private DateTime _minuteWindowStart = DateTime.Now;
    private DateTime _hourWindowStart = DateTime.Now;

    private async Task CheckAndEnforceRateLimitsAsync()
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var now = DateTime.Now;
            
            // Reset counters if window has expired
            if (now - _minuteWindowStart > TimeSpan.FromMinutes(1))
            {
                _requestsThisMinute = 0;
                _minuteWindowStart = now;
            }
            
            if (now - _hourWindowStart > TimeSpan.FromHours(1))
            {
                _requestsThisHour = 0;
                _hourWindowStart = now;
            }
            
            // Check if we've hit rate limits
            if (_requestsThisMinute >= _options.MaxRequestsPerMinute || 
                _requestsThisHour >= _options.MaxRequestsPerHour)
            {
                var delayMs = 1000; // Default delay if both limits hit
                
                if (_requestsThisMinute >= _options.MaxRequestsPerMinute)
                {
                    var timeUntilMinuteReset = TimeSpan.FromMinutes(1) - (now - _minuteWindowStart);
                    delayMs = Math.Max(delayMs, (int)timeUntilMinuteReset.TotalMilliseconds);
                }
                
                if (_requestsThisHour >= _options.MaxRequestsPerHour)
                {
                    var timeUntilHourReset = TimeSpan.FromHours(1) - (now - _hourWindowStart);
                    delayMs = Math.Max(delayMs, (int)timeUntilHourReset.TotalMilliseconds);
                }
                
                LogRateLimitHit(_logger, delayMs, null);
                await Task.Delay(delayMs);
                
                // Reset counters after delay
                _requestsThisMinute = 0;
                _requestsThisHour = 0;
                _minuteWindowStart = now;
                _hourWindowStart = now;
            }
            
            // Increment counters
            _requestsThisMinute++;
            _requestsThisHour++;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public void Dispose()
    {
        _rateLimiter?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<List<JobListing>> SearchJobsAsync(string query, string location = "", int page = 1, int pageSize = 20)
    {
        var operation = "SearchJobs";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (_options.EnableRateLimiting)
            {
                await CheckAndEnforceRateLimitsAsync();
                await Task.Delay(_options.RequestDelay);
            }

            var searchParams = new Dictionary<string, string>
            {
                ["q"] = query,
                ["page"] = page.ToString(CultureInfo.InvariantCulture),
                ["size"] = pageSize.ToString(CultureInfo.InvariantCulture)
            };

            if (!string.IsNullOrEmpty(location))
            {
                searchParams["location"] = location;
            }

            var queryString = string.Join("&", searchParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            var url = $"/api/jobs/search?{queryString}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<TecnoempleoSearchResult>(content, _jsonOptions);

            return searchResult?.Jobs?.Select(job => MapTecnoempleoJob(job)).ToList() ?? new List<JobListing>();
        }
        catch (Exception ex)
        {
            LogSearchError(_logger, query, ex);
            throw;
        }
        finally
        {
            sw.Stop();
            LogApiRequestTiming(_logger, operation, sw.ElapsedMilliseconds, null);
        }
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId)
    {
        var operation = "GetJobDetails";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (_options.EnableRateLimiting)
            {
                await CheckAndEnforceRateLimitsAsync();
                await Task.Delay(_options.RequestDelay);
            }

            var url = $"/api/jobs/{jobId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jobDetail = JsonSerializer.Deserialize<TecnoempleoJobDetail>(content, _jsonOptions);

            return MapTecnoempleoJobDetail(jobDetail ?? throw new InvalidOperationException("Job detail deserialization failed"));
        }
        catch (Exception ex)
        {
            LogDetailsError(_logger, jobId, ex);
            throw;
        }
        finally
        {
            sw.Stop();
            LogApiRequestTiming(_logger, operation, sw.ElapsedMilliseconds, null);
        }
    }

    private static JobListing MapTecnoempleoJob(TecnoempleoJob tecnoempleoJob)
    {
        var salaryInfo = TecnoempleoConstants.ParseSpanishSalary(tecnoempleoJob.Salary ?? "");
        var jobType = TecnoempleoConstants.MapSpanishJobType(tecnoempleoJob.ContractType ?? "");

        return new JobListing
        {
            Id = tecnoempleoJob.Id,
            Title = tecnoempleoJob.Title,
            Company = tecnoempleoJob.Company,
            Location = tecnoempleoJob.Location,
            Description = tecnoempleoJob.Description,
            Salary = $"{salaryInfo.Amount:N0} {salaryInfo.Currency}",
            JobType = jobType,
            Url = tecnoempleoJob.Url,
            PostedAt = tecnoempleoJob.PostedAt,
            Source = "Tecnoempleo"
        };
    }

    private static JobListing MapTecnoempleoJobDetail(TecnoempleoJobDetail jobDetail)
    {
        var salaryInfo = TecnoempleoConstants.ParseSpanishSalary(jobDetail.Salary ?? "");
        var jobType = TecnoempleoConstants.MapSpanishJobType(jobDetail.ContractType ?? "");

        var description = jobDetail.Description;
        if (jobDetail.Requirements?.Count > 0)
        {
            description += "\n\nRequisitos:\n" + string.Join("\n", jobDetail.Requirements);
        }
        if (jobDetail.Benefits?.Count > 0)
        {
            description += "\n\nBeneficios:\n" + string.Join("\n", jobDetail.Benefits);
        }

        return new JobListing
        {
            Id = jobDetail.Id,
            Title = jobDetail.Title,
            Company = jobDetail.Company,
            Location = jobDetail.Location,
            Description = description,
            Salary = $"{salaryInfo.Amount:N0} {salaryInfo.Currency}",
            JobType = jobType,
            Url = jobDetail.Url,
            PostedAt = jobDetail.PostedAt,
            Source = "Tecnoempleo"
        };
    }
}

public class TecnoempleoSearchResult
{
    public List<TecnoempleoJob> Jobs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TecnoempleoJob
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
}

public class TecnoempleoJobDetail
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
    public List<string> Requirements { get; set; } = new();
    public List<string> Benefits { get; set; } = new();
    public List<string> Skills { get; set; } = new();
}

public class Job
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public string Currency { get; set; } = "EUR";
    public JobType JobType { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
    public bool IsTechnologyJob { get; set; }
    public string Source { get; set; } = "Tecnoempleo";
    public List<string> Requirements { get; set; } = new();
    public List<string> Benefits { get; set; } = new();
    public List<string> Skills { get; set; } = new();
}