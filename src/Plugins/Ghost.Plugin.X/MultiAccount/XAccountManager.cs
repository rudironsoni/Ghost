using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X.MultiAccount;

/// <summary>
/// Manages multiple X accounts for high-volume posting with rotation.
/// </summary>
public interface IXAccountManager
{
    /// <summary>
    /// Registers an account for use.
    /// </summary>
    public void RegisterAccount(string accountId, XAccountOptions options);

    /// <summary>
    /// Gets the next available account using round-robin rotation.
    /// </summary>
    public XAccount? GetNextAccount();

    /// <summary>
    /// Marks an account as rate-limited.
    /// </summary>
    public void MarkRateLimited(string accountId, TimeSpan duration);

    /// <summary>
    /// Gets all registered accounts.
    /// </summary>
    public IReadOnlyList<XAccount> GetAllAccounts();

    /// <summary>
    /// Gets account by ID.
    /// </summary>
    public XAccount? GetAccount(string accountId);
}

/// <summary>
/// Account configuration options.
/// </summary>
public class XAccountOptions
{
    /// <summary>
    /// Unique account identifier.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Path to storage state file for this account.
    /// </summary>
    public string StorageStatePath { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the account.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Maximum posts per hour for this account.
    /// </summary>
    public int MaxPostsPerHour { get; set; } = 50;

    /// <summary>
    /// Whether this account is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Priority for account selection (higher = preferred).
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>
/// Represents an X account with runtime state.
/// </summary>
public class XAccount
{
    public string AccountId { get; set; } = string.Empty;
    public string StorageStatePath { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int MaxPostsPerHour { get; set; }
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }

    public bool IsRateLimited { get; set; }
    public DateTime? RateLimitExpiresAt { get; set; }
    public int PostsThisHour { get; set; }
    public DateTime LastPostAt { get; set; }
    public int TotalPosts { get; set; }
    public int FailedPosts { get; set; }

    /// <summary>
    /// Checks if account can post based on rate limits.
    /// </summary>
    public bool CanPost()
    {
        if (!IsEnabled) return false;
        if (IsRateLimited && RateLimitExpiresAt > DateTime.UtcNow) return false;
        if (PostsThisHour >= MaxPostsPerHour) return false;
        return true;
    }
}

/// <summary>
/// Implementation of account manager with round-robin rotation.
/// </summary>
public partial class XAccountManager : IXAccountManager
{
    private readonly Dictionary<string, XAccount> _accounts = new();
    private readonly List<string> _accountIds = new();
    private int _currentIndex;
    private readonly object _lock = new();
    private readonly ILogger<XAccountManager> _logger;

    public XAccountManager(ILogger<XAccountManager> logger)
    {
        _logger = logger;
    }

    public void RegisterAccount(string accountId, XAccountOptions options)
    {
        lock (_lock)
        {
            var account = new XAccount
            {
                AccountId = accountId,
                StorageStatePath = options.StorageStatePath,
                DisplayName = options.DisplayName ?? accountId,
                MaxPostsPerHour = options.MaxPostsPerHour,
                IsEnabled = options.IsEnabled,
#pragma warning disable CA1805
                Priority = options.Priority
#pragma warning restore CA1805
            };

            _accounts[accountId] = account;

            if (!_accountIds.Contains(accountId))
            {
                _accountIds.Add(accountId);
                // Sort by priority (higher first)
                _accountIds.Sort((a, b) => _accounts[b].Priority.CompareTo(_accounts[a].Priority));
            }

            Log.AccountRegistered(_logger, accountId);
        }
    }

    public XAccount? GetNextAccount()
    {
        lock (_lock)
        {
            if (_accountIds.Count == 0)
            {
                Log.NoAccountsRegistered(_logger);
                return null;
            }

            // Try to find an available account
            for (int i = 0; i < _accountIds.Count; i++)
            {
                int index = (_currentIndex + i) % _accountIds.Count;
                string accountId = _accountIds[index];
                XAccount account = _accounts[accountId];

                if (account.CanPost())
                {
                    _currentIndex = (index + 1) % _accountIds.Count;
                    Log.AccountSelected(_logger, accountId);
                    return account;
                }
            }

            Log.AllAccountsRateLimited(_logger);
            return null;
        }
    }

    public void MarkRateLimited(string accountId, TimeSpan duration)
    {
        lock (_lock)
        {
            if (_accounts.TryGetValue(accountId, out XAccount? account))
            {
                account.IsRateLimited = true;
                account.RateLimitExpiresAt = DateTime.UtcNow.Add(duration);
                Log.AccountMarkedRateLimited(_logger, accountId, duration);
            }
        }
    }

    public IReadOnlyList<XAccount> GetAllAccounts()
    {
        lock (_lock)
        {
            return _accounts.Values.ToList().AsReadOnly();
        }
    }

    public XAccount? GetAccount(string accountId)
    {
        lock (_lock)
        {
            return _accounts.TryGetValue(accountId, out XAccount? account) ? account : null;
        }
    }

    /// <summary>
    /// Records a successful post for an account.
    /// </summary>
    public void RecordPost(string accountId)
    {
        lock (_lock)
        {
            if (_accounts.TryGetValue(accountId, out XAccount? account))
            {
                account.PostsThisHour++;
                account.TotalPosts++;
                account.LastPostAt = DateTime.UtcNow;

                // Reset hourly counter if needed
                if (account.LastPostAt.AddHours(1) < DateTime.UtcNow)
                {
                    account.PostsThisHour = 1;
                }
            }
        }
    }

    /// <summary>
    /// Records a failed post for an account.
    /// </summary>
    public void RecordFailure(string accountId)
    {
        lock (_lock)
        {
            if (_accounts.TryGetValue(accountId, out XAccount? account))
            {
                account.FailedPosts++;
            }
        }
    }
}
