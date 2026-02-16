using System.Text.Json;
using Ghost.Plugin.X.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X.Configuration;

/// <summary>
/// Validates X platform configuration on startup.
/// </summary>
public partial class XConfigurationValidator : IValidateOptions<XOptions>
{
    private readonly ILogger<XConfigurationValidator> _logger;

    public XConfigurationValidator(ILogger<XConfigurationValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, XOptions options)
    {
        List<string> failures = [];

        // Validate BaseUrl
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add("BaseUrl is required");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? uri))
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
            (bool IsValid, string? ErrorMessage) validation = ValidateStorageStatePath(options.StorageStatePath);
            if (!validation.IsValid)
            {
                failures.Add(validation.ErrorMessage ?? "Invalid storage state path");
            }
        }
        else
        {
            Log.StorageStatePathNotConfigured(_logger);
        }

        // Validate timeouts
        if (options.PageLoadTimeout <= 0)
        {
            failures.Add("PageLoadTimeout must be greater than 0");
        }
        else if (options.PageLoadTimeout < 5)
        {
            Log.PageLoadTimeoutTooShort(_logger, options.PageLoadTimeout);
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
            Log.MaxImageSizeExceeded(_logger, options.MaxImageSizeMB);
        }
        if (options.MaxVideoSizeMB > 512)
        {
            Log.MaxVideoSizeExceeded(_logger, options.MaxVideoSizeMB);
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        Log.ConfigurationValidated(_logger);
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
            string content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, $"Storage state file '{path}' is empty");
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                Log.StorageStateFileValid(_logger, path);
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
public partial class XPlatformHealthCheck
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
            Log.StartingHealthCheck(_logger);

            // Check browser connectivity
            result.BrowserAvailable = await CheckBrowserConnectivityAsync(ct).ConfigureAwait(false);
            if (!result.BrowserAvailable)
            {
                result.Status = HealthStatus.Degraded;
                result.Messages.Add("Browser session is not available");
            }

            // Check authentication
            if (!string.IsNullOrWhiteSpace(_options.StorageStatePath) && File.Exists(_options.StorageStatePath))
            {
                result.AuthStateValid = await CheckAuthStateAsync(ct).ConfigureAwait(false);
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
            result.XPlatformReachable = await CheckXConnectivityAsync(ct).ConfigureAwait(false);
            if (!result.XPlatformReachable)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Messages.Add("Cannot reach X platform");
            }

            if (result.Status == HealthStatus.Healthy)
            {
                Log.HealthCheckPassed(_logger);
            }
            else
            {
                Log.HealthCheckStatus(_logger, result.Status);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.HealthCheckException(_logger, ex);
            result.Status = HealthStatus.Unhealthy;
            result.Messages.Add($"Health check exception: {ex.Message}");
            return result;
        }
    }

    private async Task<bool> CheckBrowserConnectivityAsync(CancellationToken ct)
    {
        IPage? page = null;
        try
        {
            page = await _session.NewPageAsync(ct: ct).ConfigureAwait(false);
            await page.NavigateAsync("about:blank", ct: ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.BrowserConnectivityFailed(_logger, ex);
            return false;
        }
        finally
        {
            if (page is not null)
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> CheckAuthStateAsync(CancellationToken ct)
    {
        try
        {
            // Try to load storage state
            await _session.SaveStorageStateAsync(_options.StorageStatePath!).ConfigureAwait(false);
            Log.AuthStateFileReadable(_logger);
            return true;
        }
        catch (Exception ex)
        {
            Log.AuthStateCheckFailed(_logger, ex);
            return false;
        }
    }

    private async Task<bool> CheckXConnectivityAsync(CancellationToken ct)
    {
        IPage? page = null;
        try
        {
            page = await _session.NewPageAsync(ct: ct).ConfigureAwait(false);
            await page.NavigateAsync(_options.BaseUrl, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
            return page.Url.Contains("x.com") || page.Url.Contains("twitter.com");
        }
        catch (Exception ex)
        {
            Log.XConnectivityCheckFailed(_logger, ex);
            return false;
        }
        finally
        {
            if (page is not null)
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
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
    public List<string> Messages { get; set; } = [];
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
