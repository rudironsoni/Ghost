using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X.Internal;

/// <summary>
/// Handles authentication for X (Twitter) using cookie-based storage state.
/// </summary>
public partial class XAuthenticator
{
    private readonly IBrowserSession _session;
    private readonly XOptions _options;
    private readonly ILogger<XAuthenticator> _logger;

    public XAuthenticator(
        IBrowserSession session,
        IOptions<XOptions> options,
        ILogger<XAuthenticator> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
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
            IElement? accountMenu = await page.QuerySelectorAsync("[data-testid='AppTabBar_More_Menu']", ct).ConfigureAwait(false)
                ?? await page.QuerySelectorAsync("[data-testid='SideNav_AccountSwitcher_Button']", ct).ConfigureAwait(false)
                ?? await page.QuerySelectorAsync("[data-testid='PrimaryColumn']", ct).ConfigureAwait(false);

            if (accountMenu != null)
            {
                Log.UserLoggedIn(_logger);
                return true;
            }

            // Check for login button which indicates logged out state
            IElement? loginButton = await page.QuerySelectorAsync("a[href='/login']", ct).ConfigureAwait(false)
                ?? await page.QuerySelectorAsync("[data-testid='loginButton']", ct).ConfigureAwait(false);

            if (loginButton != null)
            {
                Log.UserLoggedOut(_logger);
                return false;
            }

            Log.LoginStateUndetermined(_logger);
            return false;
        }
        catch (Exception ex)
        {
            Log.LoginStateCheckError(_logger, ex);
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
            Log.AlreadyAuthenticated(_logger);
            return;
        }

        // Try to load storage state if path is configured
        if (!string.IsNullOrWhiteSpace(_options.StorageStatePath) && File.Exists(_options.StorageStatePath))
        {
            try
            {
                Log.LoadingStorageState(_logger, _options.StorageStatePath);
                await _session.SaveStorageStateAsync(_options.StorageStatePath).ConfigureAwait(false);

                // Reload page to apply cookies
                await page.NavigateAsync(_options.BaseUrl, ct: ct).ConfigureAwait(false);
                await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

                if (await IsLoggedInAsync(page, ct).ConfigureAwait(false))
                {
                    Log.AuthenticationSuccessful(_logger);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.StorageStateLoadFailed(_logger, ex, _options.StorageStatePath);
            }
        }

        Log.NotAuthenticated(_logger);
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
            Log.WarmingUp(_logger);
            IPage page = await _session.NewPageAsync(ct: ct).ConfigureAwait(false);

            try
            {
                await page.NavigateAsync(_options.BaseUrl, ct: ct).ConfigureAwait(false);
                await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

                // Check if logged in
                bool isLoggedIn = await IsLoggedInAsync(page, ct).ConfigureAwait(false);
                Log.WarmUpComplete(_logger, isLoggedIn);
            }
            finally
            {
                try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { _logger.LogError(ex, "Failed to dispose page"); }
            }
        }
        catch (Exception ex)
        {
            Log.WarmUpFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Saves the current authentication state to disk.
    /// </summary>
    public virtual async Task SaveAuthenticationStateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.StorageStatePath))
        {
            Log.NoStorageStatePath(_logger);
            return;
        }

        try
        {
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(_options.StorageStatePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await _session.SaveStorageStateAsync(_options.StorageStatePath).ConfigureAwait(false);
            Log.AuthenticationStateSaved(_logger, _options.StorageStatePath);
        }
        catch (Exception ex)
        {
            Log.AuthenticationStateSaveFailed(_logger, ex, _options.StorageStatePath);
            throw;
        }
    }
}
