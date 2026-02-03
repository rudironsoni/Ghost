using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.X.Internal;

/// <summary>
/// Handles authentication for X (Twitter) using cookie-based storage state.
/// </summary>
public class XAuthenticator
{
    private readonly IBrowserSession _session;
    private readonly XOptions _options;
    private readonly ILogger<XAuthenticator> _logger;

    public XAuthenticator(
        IBrowserSession session,
        IOptions<XOptions> options,
        ILogger<XAuthenticator> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _options = options?.Value ?? new XOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XAuthenticator>.Instance;
    }

    /// <summary>
    /// Checks if the user is currently logged in to X.
    /// </summary>
    public virtual async Task<bool> IsLoggedInAsync(IPage page, CancellationToken ct = default)
    {
        try
        {
            // Check for logged-in indicators
            var accountMenu = await page.QuerySelectorAsync("[data-testid='AppTabBar_More_Menu']", ct)
                ?? await page.QuerySelectorAsync("[data-testid='SideNav_AccountSwitcher_Button']", ct)
                ?? await page.QuerySelectorAsync("[data-testid='PrimaryColumn']", ct);

            if (accountMenu != null)
            {
                _logger.LogDebug("User appears to be logged in to X");
                return true;
            }

            // Check for login button which indicates logged out state
            var loginButton = await page.QuerySelectorAsync("a[href='/login']", ct)
                ?? await page.QuerySelectorAsync("[data-testid='loginButton']", ct);

            if (loginButton != null)
            {
                _logger.LogDebug("User appears to be logged out of X");
                return false;
            }

            _logger.LogWarning("Could not determine login state, assuming not logged in");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking login state");
            return false;
        }
    }

    /// <summary>
    /// Ensures the user is authenticated, loading storage state if available.
    /// </summary>
    public virtual async Task EnsureAuthenticatedAsync(IPage page, CancellationToken ct = default)
    {
        if (await IsLoggedInAsync(page, ct).ConfigureAwait(false))
        {
            _logger.LogDebug("User is already authenticated");
            return;
        }

        // Try to load storage state if path is configured
        if (!string.IsNullOrWhiteSpace(_options.StorageStatePath) && File.Exists(_options.StorageStatePath))
        {
            try
            {
                _logger.LogInformation("Loading storage state from {Path}", _options.StorageStatePath);
                await _session.SaveStorageStateAsync(_options.StorageStatePath);
                
                // Reload page to apply cookies
                await page.NavigateAsync(_options.BaseUrl, ct: ct);
                await page.WaitForLoadStateAsync(ct: ct);

                if (await IsLoggedInAsync(page, ct).ConfigureAwait(false))
                {
                    _logger.LogInformation("Successfully authenticated using storage state");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load storage state from {Path}", _options.StorageStatePath);
            }
        }

        _logger.LogError("User is not authenticated and no valid storage state found");
        throw new InvalidOperationException("Not authenticated to X. Please log in and save storage state.");
    }

    /// <summary>
    /// Warms up the session by navigating to the home page.
    /// </summary>
    public virtual async Task WarmUpAsync(CancellationToken ct = default)
    {
        if (!_options.WarmUpEnabled)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Warming up X session");
            var page = await _session.NewPageAsync(ct: ct);
            
            try
            {
                await page.NavigateAsync(_options.BaseUrl, ct: ct);
                await page.WaitForLoadStateAsync(ct: ct);
                
                // Check if logged in
                var isLoggedIn = await IsLoggedInAsync(page, ct);
                _logger.LogInformation("Warm-up complete. Logged in: {IsLoggedIn}", isLoggedIn);
            }
            finally
            {
                try { await page.DisposeAsync(); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warm-up failed, but continuing anyway");
        }
    }

    /// <summary>
    /// Saves the current authentication state to disk.
    /// </summary>
    public virtual async Task SaveAuthenticationStateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.StorageStatePath))
        {
            _logger.LogWarning("No storage state path configured, cannot save authentication");
            return;
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_options.StorageStatePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await _session.SaveStorageStateAsync(_options.StorageStatePath);
            _logger.LogInformation("Authentication state saved to {Path}", _options.StorageStatePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save authentication state to {Path}", _options.StorageStatePath);
            throw;
        }
    }
}
