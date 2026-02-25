using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// In-memory implementation of <see cref="ICookieMiddleware"/> with thread-safe cookie storage by domain.
/// Supports loading and saving cookies to JSON files for persistence.
/// </summary>
public sealed class CookieMiddleware : ICookieMiddleware
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<string, List<Cookie>> _cookies = new();
    private readonly object _fileLock = new();
    private const int MaxCookiesPerDomain = 1000;

    /// <summary>
    /// Validates that the file path is safe to use (prevents directory traversal).
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the path contains invalid characters or traversal patterns.</exception>
    private static void ValidateFilePath(string filePath)
    {
        // Check for directory traversal patterns and invalid characters
        if (filePath.Contains("..", StringComparison.Ordinal) ||
            filePath.Contains('~') ||
            filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException("Invalid file path: contains potentially unsafe characters or patterns.", nameof(filePath));
        }

        // Ensure the path doesn't contain null bytes or other dangerous patterns
        if (filePath.Contains('\0'))
        {
            throw new ArgumentException("Invalid file path: contains null bytes.", nameof(filePath));
        }
    }

    /// <summary>
    /// Retrieves all cookies for the specified domain.
    /// </summary>
    public Task<IReadOnlyList<Cookie>> GetCookiesAsync(string domain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);

        if (_cookies.TryGetValue(domain, out List<Cookie>? domainCookies))
        {
            lock (domainCookies)
            {
                return Task.FromResult<IReadOnlyList<Cookie>>(domainCookies.ToList());
            }
        }

        return Task.FromResult<IReadOnlyList<Cookie>>(Array.Empty<Cookie>());
    }

    /// <summary>
    /// Sets a cookie for the specified domain.
    /// If a cookie with the same name already exists, it will be replaced.
    /// </summary>
    public Task SetCookieAsync(string domain, Cookie cookie, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(cookie);

        List<Cookie> domainCookies = _cookies.GetOrAdd(domain, _ => new List<Cookie>());
        lock (domainCookies)
        {
            // Remove existing cookie with the same name
            domainCookies.RemoveAll(c => c.Name == cookie.Name);

            // Enforce maximum cookies per domain to prevent unbounded growth
            if (domainCookies.Count >= MaxCookiesPerDomain)
            {
                // Remove oldest cookies (FIFO)
                int removeCount = domainCookies.Count - MaxCookiesPerDomain + 1;
                domainCookies.RemoveRange(0, removeCount);
            }

            domainCookies.Add(cookie);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads cookies from a JSON file.
    /// The JSON file should contain a dictionary of domain names to cookie arrays.
    /// </summary>
    public async Task LoadCookiesAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ValidateFilePath(filePath);

        if (!File.Exists(filePath))
        {
            return;
        }

        string json;
        lock (_fileLock)
        {
            json = File.ReadAllText(filePath);
        }

        Dictionary<string, List<Cookie>>? cookieData = JsonSerializer.Deserialize<Dictionary<string, List<Cookie>>>(json);
        if (cookieData is null)
        {
            return;
        }

        foreach ((string? domain, List<Cookie>? domainCookies) in cookieData)
        {
            _cookies[domain] = new List<Cookie>(domainCookies);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Saves all cookies to a JSON file.
    /// The cookies are serialized as a dictionary of domain names to cookie arrays.
    /// </summary>
    public async Task SaveCookiesAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ValidateFilePath(filePath);

        var cookieData = _cookies.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                lock (kvp.Value)
                {
                    return kvp.Value.ToList();
                }
            });

        string json = JsonSerializer.Serialize(cookieData, s_jsonOptions);

        lock (_fileLock)
        {
            File.WriteAllText(filePath, json);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all cookies for the specified domain.
    /// </summary>
    public Task ClearCookiesAsync(string domain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);

        _cookies.TryRemove(domain, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all cookies.
    /// </summary>
    public Task ClearAllCookiesAsync(CancellationToken ct = default)
    {
        _cookies.Clear();
        return Task.CompletedTask;
    }
}
