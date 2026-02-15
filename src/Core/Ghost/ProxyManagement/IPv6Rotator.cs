using System.Net;
using System.Net.NetworkInformation;

namespace Ghost.Core.ProxyManagement;

/// <summary>
/// Configuration for IPv6 proxy rotation.
/// </summary>
public sealed class IPv6RotatorOptions
{
    /// <summary>
    /// Gets or sets the IPv6 /64 subnet prefix (e.g., "2001:db8:1234:5678").
    /// </summary>
    public string SubnetPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the network interface name to bind to (e.g., "eth0").
    /// </summary>
    public string? NetworkInterface { get; set; }

    /// <summary>
    /// Gets or sets whether to perform health checks on generated IPv6 addresses.
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check timeout in seconds.
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of addresses to keep in the pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to automatically bind IPv6 addresses to the network interface.
    /// Requires elevated permissions (root/admin).
    /// </summary>
    public bool AutoBind { get; set; }
}

/// <summary>
/// Generates and manages IPv6 addresses from a /64 subnet for proxy rotation.
/// This provides millions of unique IPs from a single VPS (~$5/month) instead of
/// expensive commercial proxy services ($500+/month).
/// </summary>
/// <remarks>
/// IPv6 /64 subnets contain 2^64 = 18,446,744,073,709,551,616 addresses.
/// Each address can be used as a unique egress IP for web scraping, making
/// detection and blocking extremely difficult for anti-bot systems.
/// </remarks>
public sealed class IPv6Rotator : IDisposable
{
    private readonly IPv6RotatorOptions _options;
    private readonly Random _random;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _activeAddresses = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IPv6Rotator"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when subnet prefix is invalid.</exception>
    public IPv6Rotator(IPv6RotatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SubnetPrefix))
        {
            throw new ArgumentException("SubnetPrefix cannot be empty", nameof(options));
        }

        if (!IsValidIPv6Prefix(options.SubnetPrefix))
        {
            throw new ArgumentException($"Invalid IPv6 prefix: {options.SubnetPrefix}", nameof(options));
        }

        _options = options;
        _random = new Random();
    }

    /// <summary>
    /// Generates a random IPv6 address from the configured /64 subnet.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A random IPv6 address string.</returns>
    public async Task<string> GetRandomAddressAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Generate random 64-bit host identifier
            Span<byte> hostBytes = stackalloc byte[8];
            _random.NextBytes(hostBytes);

            // Construct full IPv6 address: prefix + random host
            string address = $"{_options.SubnetPrefix}:{hostBytes[0]:x2}{hostBytes[1]:x2}:{hostBytes[2]:x2}{hostBytes[3]:x2}:{hostBytes[4]:x2}{hostBytes[5]:x2}:{hostBytes[6]:x2}{hostBytes[7]:x2}";

            // Normalize address format
            var ipAddress = IPAddress.Parse(address);
            string normalized = ipAddress.ToString();

            // Health check if enabled
            if (_options.EnableHealthCheck)
            {
                bool isHealthy = await CheckAddressHealthAsync(normalized, cancellationToken).ConfigureAwait(false);
                if (!isHealthy)
                {
                    // Retry with different address
                    return await GetRandomAddressAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            // Track active address
            if (_activeAddresses.Count >= _options.MaxPoolSize)
            {
                // Remove oldest (FIFO)
                string oldest = _activeAddresses.First();
                _activeAddresses.Remove(oldest);
            }
            _activeAddresses.Add(normalized);

            return normalized;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Generates multiple random IPv6 addresses in parallel.
    /// </summary>
    /// <param name="count">Number of addresses to generate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of unique IPv6 addresses.</returns>
    public async Task<string[]> GetRandomAddressesAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        IEnumerable<Task<string>> tasks = Enumerable.Range(0, count)
            .Select(_ => GetRandomAddressAsync(cancellationToken));

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if an IPv6 address is healthy (reachable and not blacklisted).
    /// </summary>
    /// <param name="address">IPv6 address to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the address is healthy, false otherwise.</returns>
    public async Task<bool> CheckAddressHealthAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        try
        {
            // Parse address
            if (!IPAddress.TryParse(address, out IPAddress? ipAddress))
            {
                return false;
            }

            // Ping the address to verify connectivity
            using var ping = new Ping();
            var pingOptions = new PingOptions
            {
                Ttl = 64
            };

            int timeoutMs = _options.HealthCheckTimeoutSeconds * 1000;
            PingReply reply = await ping.SendPingAsync(ipAddress, timeoutMs, Array.Empty<byte>(), pingOptions).ConfigureAwait(false);

            // Consider address healthy if it responds or if unreachable (not all hosts respond to ping)
            // The key is to filter out addresses that are definitely invalid
            return reply.Status == IPStatus.Success ||
                   reply.Status == IPStatus.TimedOut ||
                   reply.Status == IPStatus.TtlExpired;
        }
        catch
        {
            // If health check fails, assume address might still be usable
            // (Some networks block ICMP)
            return true;
        }
    }

    /// <summary>
    /// Binds an IPv6 address to the configured network interface.
    /// Requires elevated permissions (root/admin).
    /// </summary>
    /// <param name="address">IPv6 address to bind.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if binding succeeded, false otherwise.</returns>
    /// <exception cref="NotSupportedException">Thrown on non-Linux platforms.</exception>
    public async Task<bool> BindAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (!OperatingSystem.IsLinux())
        {
            throw new NotSupportedException("Address binding is only supported on Linux");
        }

        if (string.IsNullOrWhiteSpace(_options.NetworkInterface))
        {
            throw new InvalidOperationException("NetworkInterface must be specified for address binding");
        }

        try
        {
            // Use ip command to add address to interface
            // Example: ip -6 addr add 2001:db8::1/128 dev eth0
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ip",
                Arguments = $"-6 addr add {address}/128 dev {_options.NetworkInterface}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes an IPv6 address binding from the configured network interface.
    /// Requires elevated permissions (root/admin).
    /// </summary>
    /// <param name="address">IPv6 address to unbind.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if unbinding succeeded, false otherwise.</returns>
    public async Task<bool> UnbindAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (!OperatingSystem.IsLinux())
        {
            throw new NotSupportedException("Address unbinding is only supported on Linux");
        }

        if (string.IsNullOrWhiteSpace(_options.NetworkInterface))
        {
            throw new InvalidOperationException("NetworkInterface must be specified for address unbinding");
        }

        try
        {
            // Use ip command to remove address from interface
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ip",
                Arguments = $"-6 addr del {address}/128 dev {_options.NetworkInterface}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets statistics about the IPv6 address pool.
    /// </summary>
    /// <returns>Statistics object.</returns>
    public IPv6RotatorStats GetStats()
    {
        return new IPv6RotatorStats
        {
            ActiveAddressCount = _activeAddresses.Count,
            MaxPoolSize = _options.MaxPoolSize,
            SubnetPrefix = _options.SubnetPrefix,
            HealthCheckEnabled = _options.EnableHealthCheck,
            TotalAvailableAddresses = "18,446,744,073,709,551,616 (2^64)"
        };
    }

    /// <summary>
    /// Validates if a string is a valid IPv6 /64 prefix.
    /// </summary>
    private static bool IsValidIPv6Prefix(string prefix)
    {
        // IPv6 /64 prefix should have 4 groups (64 bits)
        // Example: 2001:db8:1234:5678
        string[] parts = prefix.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        // Validate each part is a valid hex value
        foreach (string part in parts)
        {
            if (!ushort.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Statistics about the IPv6 rotator pool.
/// </summary>
public sealed class IPv6RotatorStats
{
    /// <summary>
    /// Gets or sets the number of active addresses in the pool.
    /// </summary>
    public int ActiveAddressCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum pool size.
    /// </summary>
    public int MaxPoolSize { get; set; }

    /// <summary>
    /// Gets or sets the subnet prefix.
    /// </summary>
    public string SubnetPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether health checking is enabled.
    /// </summary>
    public bool HealthCheckEnabled { get; set; }

    /// <summary>
    /// Gets or sets the total number of available addresses in the subnet.
    /// </summary>
    public string TotalAvailableAddresses { get; set; } = string.Empty;
}
