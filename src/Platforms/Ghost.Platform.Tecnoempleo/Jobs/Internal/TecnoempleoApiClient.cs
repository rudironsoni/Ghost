using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace Ghost.Platform.Tecnoempleo.Jobs.Internal;

public class TecnoempleoApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TecnoempleoOptions _options;
    private readonly ILogger<TecnoempleoApiClient> _logger;

    public TecnoempleoApiClient(HttpClient httpClient, IOptions<TecnoempleoOptions> options, ILogger<TecnoempleoApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.ApiUrl);
        
        foreach (var header in TecnoempleoConstants.ApiHeaders)
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(_options.ClientId) && !string.IsNullOrEmpty(_options.ClientSecret))
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}");
            var authHeader = Convert.ToBase64String(authBytes);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<List<JobListing>> SearchJobsAsync(string query, string location = "", int page = 1, int pageSize = 20)
    {
        try
        {
            if (_options.EnableRateLimiting)
            {
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
            _logger.LogError(ex, "Error searching Tecnoempleo jobs for query: {Query}", query);
            throw;
        }
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId)
    {
        try
        {
            if (_options.EnableRateLimiting)
            {
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
            _logger.LogError(ex, "Error fetching Tecnoempleo job details for ID: {JobId}", jobId);
            throw;
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