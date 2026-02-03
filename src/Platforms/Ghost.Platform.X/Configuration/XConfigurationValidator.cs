using Ghost.Platform.X.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ghost.Platform.X.Configuration;

/// <summary>
/// Validates X platform configuration on startup.
/// </summary>
public class XConfigurationValidator : IValidateOptions<XOptions>
{
    private readonly ILogger<XConfigurationValidator> _logger;

    public XConfigurationValidator(ILogger<XConfigurationValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, XOptions options)
    {
        var failures = new List<string>();

        // Validate BaseUrl
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add("BaseUrl is required");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri))
        {
            failures.Add($"BaseUrl '{options.BaseUrl}' is not a valid URL");
        }
        else if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("BaseUrl must use HTTPS scheme for security");
        }

        // Validate StorageStatePath
        if (!string.IsNullOrWhiteSpace(options.StorageStatePath))
        {
            var validation = ValidateStorageStatePath(options.StorageStatePath);
            if (!validation.IsValid)
            {
                failures.Add(validation.ErrorMessage ?? "Invalid storage state path");
            }
        }
        else
        {
            _logger.LogWarning("StorageStatePath is not configured. Authentication will fail. " +
                             "Please authenticate and save storage state to use X platform.");
        }

        // Validate timeouts
        if (options.PageLoadTimeout <= 0)
        {
            failures.Add("PageLoadTimeout must be greater than 0");
        }
        else if (options.PageLoadTimeout < 5)
        {
            _logger.LogWarning("PageLoadTimeout is set to {Timeout}s which may be too short for slow connections", 
                options.PageLoadTimeout);
        }

        // Validate retry settings
        if (options.MaxRetries < 0)
        {
            failures.Add("MaxRetries must be non-negative");
        }
        if (options.RetryDelayMs < 0)
        {
            failures.Add("RetryDelayMs must be non-negative");
        }
        if (options.ThreadDelayMs < 0)
        {
            failures.Add("ThreadDelayMs must be non-negative");
        }

        // Validate media limits (read-only, but log warnings)
        if (options.MaxImageSizeMB > 10)
        {
            _logger.LogWarning("MaxImageSizeMB is set to {Size}MB which exceeds X's limit of 5MB", 
                options.MaxImageSizeMB);
        }
        if (options.MaxVideoSizeMB > 512)
        {
            _logger.LogWarning("MaxVideoSizeMB is set to {Size}MB which exceeds X's limit of 512MB", 
                options.MaxVideoSizeMB);
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        _logger.LogInformation("X platform configuration validated successfully");
        return ValidateOptionsResult.Success;
    }

    private (bool IsValid, string? ErrorMessage) ValidateStorageStatePath(string path)
    {
        try
        {
            // Check if file exists
            if (!File.Exists(path))
            {
                return (true, null); // File doesn't exist yet, will be created on first auth
            }

            // Validate JSON format
            var content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, $"Storage state file '{path}' is empty");
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                _logger.LogDebug("Storage state file '{Path}' is valid JSON", path);
                return (true, null);
            }
            catch (JsonException ex)
            {
                return (false, $"Storage state file '{path}' is not valid JSON: {ex.Message}");
            }
        }
        catch (UnauthorizedAccessException)
        {
            return (false, $"Access denied to storage state file '{path}'. Check file permissions.");
        }
        catch (Exception ex)
        {
            return (false, $"Error reading storage state file '{path}': {ex.Message}");
        }
    }
}

/// <summary>
/// Startup health check for X platform.
/// </summary>
public class XPlatformHealthCheck
{
    private readonly IBrowserSession _session;
    private readonly XOptions _options;
    private readonly ILogger<XPlatformHealthCheck> _logger;

    public XPlatformHealthCheck(
        IBrowserSession session, 
        IOptions<XOptions> options,
        ILogger<XPlatformHealthCheck> logger)
    {
        _session = session;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Performs a health check of the X platform configuration.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var result = new HealthCheckResult();

        try
        {
            _logger.LogDebug("Starting X platform health check");

            // Check browser connectivity
            result.BrowserAvailable = await CheckBrowserConnectivityAsync(ct);
            if (!result.BrowserAvailable)
            {
                result.Status = HealthStatus.Degraded;
                result.Messages.Add("Browser session is not available");
            }

            // Check authentication
            if (!string.IsNullOrWhiteSpace(_options.StorageStatePath) && File.Exists(_options.StorageStatePath))
            {
                result.AuthStateValid = await CheckAuthStateAsync(ct);
                if (!result.AuthStateValid)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Messages.Add("Authentication state may be invalid or expired");
                }
            }
            else
            {
                result.Status = HealthStatus.Degraded;
                result.Messages.Add("No authentication state file configured");
            }

            // Check X connectivity
            result.XPlatformReachable = await CheckXConnectivityAsync(ct);
            if (!result.XPlatformReachable)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Messages.Add("Cannot reach X platform");
            }

            if (result.Status == HealthStatus.Healthy)
            {
                _logger.LogInformation("X platform health check passed");
            }
            else
            {
                _logger.LogWarning("X platform health check returned status: {Status}", result.Status);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            result.Status = HealthStatus.Unhealthy;
            result.Messages.Add($"Health check exception: {ex.Message}");
            return result;
        }
    }

    private async Task<bool> CheckBrowserConnectivityAsync(CancellationToken ct)
    {
        try
        {
            await using var page = await _session.NewPageAsync(ct: ct);
            await page.NavigateAsync("about:blank", ct: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Browser connectivity check failed");
            return false;
        }
    }

    private async Task<bool> CheckAuthStateAsync(CancellationToken ct)
    {
        try
        {
            // Try to load storage state
            await _session.SaveStorageStateAsync(_options.StorageStatePath!);
            _logger.LogDebug("Authentication state file is readable");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Authentication state check failed");
            return false;
        }
    }

    private async Task<bool> CheckXConnectivityAsync(CancellationToken ct)
    {
        try
        {
            await using var page = await _session.NewPageAsync(ct: ct);
            await page.NavigateAsync(_options.BaseUrl, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);
            return page.Url.Contains("x.com") || page.Url.Contains("twitter.com");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "X platform connectivity check failed");
            return false;
        }
    }
}

/// <summary>
/// Health check result.
/// </summary>
public class HealthCheckResult
{
    public HealthStatus Status { get; set; } = HealthStatus.Healthy;
    public bool BrowserAvailable { get; set; }
    public bool AuthStateValid { get; set; }
    public bool XPlatformReachable { get; set; }
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// Health status.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}
