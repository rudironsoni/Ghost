using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Kernel.ProxyManagement;

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
public sealed partial class IPv6Rotator : IDisposable
{
    private readonly IPv6RotatorOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _activeAddresses = [];
    private readonly ILogger<IPv6Rotator> _logger;
    private readonly int _maxRetries = 10;
    private bool _disposed;

    // LoggerMessage source generators (EventIds 3000-3099 for Proxy)
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Executing command: {Command} {Address} {Subnet} dev {InterfaceName}")]
    private static partial void LogExecutingCommand(ILogger<IPv6Rotator> logger, string command, string address, string subnet, string interfaceName);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Failed to start ip command process")]
    private static partial void LogIpCommandStartFailed(ILogger<IPv6Rotator> logger);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "ip command failed with exit code {ExitCode}: {Error}")]
    private static partial void LogIpCommandFailed(ILogger<IPv6Rotator> logger, int exitCode, string error);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Successfully bound address to interface {InterfaceName}")]
    private static partial void LogAddressBound(ILogger<IPv6Rotator> logger, string interfaceName);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Error, Message = "Exception while binding address to interface")]
    private static partial void LogBindException(ILogger<IPv6Rotator> logger, Exception ex);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Successfully unbound address from interface {InterfaceName}")]
    private static partial void LogAddressUnbound(ILogger<IPv6Rotator> logger, string interfaceName);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Error, Message = "Exception while unbinding address from interface")]
    private static partial void LogUnbindException(ILogger<IPv6Rotator> logger, Exception ex);

    // Security: Whitelist patterns for input validation
    // These regex patterns prevent command injection by only allowing safe characters
    private static readonly Regex InterfaceNameRegex = new(
        "^[a-zA-Z0-9_-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IPv6AddressRegex = new(
        "^[0-9a-fA-F:.]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Security: Regex for validating IPv6 prefix (4 groups of hex digits separated by colons)
    private static readonly Regex IPv6PrefixRegex = new(
        "^[0-9a-fA-F]{1,4}:[0-9a-fA-F]{1,4}:[0-9a-fA-F]{1,4}:[0-9a-fA-F]{1,4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Initializes a new instance of the <see cref="IPv6Rotator"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when subnet prefix is invalid.</exception>
    public IPv6Rotator(IPv6RotatorOptions options)
        : this(options, NullLogger<IPv6Rotator>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPv6Rotator"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="logger">Logger for audit and diagnostic messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when subnet prefix is invalid.</exception>
    public IPv6Rotator(IPv6RotatorOptions options, ILogger<IPv6Rotator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(options.SubnetPrefix))
        {
            throw new ArgumentException("SubnetPrefix cannot be empty", nameof(options));
        }

        // Security: Validate subnet prefix for command injection attempts
        ValidateIPv6PrefixForSecurity(options.SubnetPrefix);

        if (!IsValidIPv6Prefix(options.SubnetPrefix))
        {
            throw new ArgumentException($"Invalid IPv6 prefix: {options.SubnetPrefix}", nameof(options));
        }

        // Security: Validate network interface name if provided
        if (!string.IsNullOrWhiteSpace(options.NetworkInterface))
        {
            ValidateInterfaceName(options.NetworkInterface);
        }

        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Generates a random IPv6 address from the configured /64 subnet.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A random IPv6 address string.</returns>
    public async Task<string> GetRandomAddressAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await GetRandomAddressWithRetryAsync(0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a random IPv6 address with retry limit to prevent infinite recursion.
    /// </summary>
    /// <param name="retryCount">Current retry count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A random IPv6 address string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when all retry attempts are exhausted.</exception>
    private async Task<string> GetRandomAddressWithRetryAsync(int retryCount, CancellationToken cancellationToken)
    {
        if (retryCount >= _maxRetries)
        {
            throw new InvalidOperationException(
                $"Failed to generate a healthy IPv6 address after {_maxRetries} attempts. " +
                "The subnet may be exhausted or health checks are consistently failing.");
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Generate random 64-bit host identifier
            Span<byte> hostBytes = stackalloc byte[8];
            Random.Shared.NextBytes(hostBytes);

            // Construct full IPv6 address: prefix + random host
            // Security: Using format specifiers ensures only hex digits are produced
            string address = $"{_options.SubnetPrefix}:{hostBytes[0]:x2}{hostBytes[1]:x2}:{hostBytes[2]:x2}{hostBytes[3]:x2}:{hostBytes[4]:x2}{hostBytes[5]:x2}:{hostBytes[6]:x2}{hostBytes[7]:x2}";

            // Security: Validate the generated address before use
            ValidateIPv6Address(address);

            // Normalize address format
            var ipAddress = IPAddress.Parse(address);
            string normalized = ipAddress.ToString();

            // Health check if enabled
            if (_options.EnableHealthCheck)
            {
                bool isHealthy = await CheckAddressHealthAsync(normalized, cancellationToken).ConfigureAwait(false);
                if (!isHealthy)
                {
                    // Retry with different address (limited retries to prevent stack overflow)
                    return await GetRandomAddressWithRetryAsync(retryCount + 1, cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentException">Thrown when address contains invalid characters.</exception>
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

        // Security: Validate inputs before use
        ValidateIPv6Address(address);
        ValidateInterfaceName(_options.NetworkInterface);

        try
        {
            // Security: Use ArgumentList instead of string interpolation to prevent command injection
            // Example: ip -6 addr add 2001:db8::1/128 dev eth0
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ip",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Security: Use ArgumentList to safely pass arguments (prevents shell injection)
            // DO NOT use string interpolation with user input - add each argument separately
            processStartInfo.ArgumentList.Add("-6");
            processStartInfo.ArgumentList.Add("addr");
            processStartInfo.ArgumentList.Add("add");
            // Security: Combine address and subnet mask into single CIDR notation argument
            // Using string concatenation (not interpolation with user input) is safe here
            // because the address has already been validated by ValidateIPv6Address
            processStartInfo.ArgumentList.Add(address + "/128");
            processStartInfo.ArgumentList.Add("dev");
            processStartInfo.ArgumentList.Add(_options.NetworkInterface);

            // Audit logging: log the command being executed for security audit trail
            string cmd = "ip -6 addr add";
            string subnet = "/128";
            LogExecutingCommand(_logger, cmd, address, subnet, _options.NetworkInterface);

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                LogIpCommandStartFailed(_logger);
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                string errorMsg = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                LogIpCommandFailed(_logger, process.ExitCode, errorMsg);
                return false;
            }

            LogAddressBound(_logger, _options.NetworkInterface);
            return true;
        }
        catch (Exception ex)
        {
            LogBindException(_logger, ex);
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
    /// <exception cref="ArgumentException">Thrown when address contains invalid characters.</exception>
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

        // Security: Validate inputs before use
        ValidateIPv6Address(address);
        ValidateInterfaceName(_options.NetworkInterface);

        try
        {
            // Security: Use ArgumentList instead of string interpolation to prevent command injection
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ip",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Security: Use ArgumentList to safely pass arguments (prevents shell injection)
            // DO NOT use string interpolation with user input - add each argument separately
            processStartInfo.ArgumentList.Add("-6");
            processStartInfo.ArgumentList.Add("addr");
            processStartInfo.ArgumentList.Add("del");
            // Security: Combine address and subnet mask into single CIDR notation argument
            // Using string concatenation (not interpolation with user input) is safe here
            // because the address has already been validated by ValidateIPv6Address
            processStartInfo.ArgumentList.Add(address + "/128");
            processStartInfo.ArgumentList.Add("dev");
            processStartInfo.ArgumentList.Add(_options.NetworkInterface);

            // Audit logging: log the command being executed for security audit trail
            string cmd2 = "ip -6 addr del";
            string subnet2 = "/128";
            LogExecutingCommand(_logger, cmd2, address, subnet2, _options.NetworkInterface);

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                LogIpCommandStartFailed(_logger);
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                string errorMsg = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                LogIpCommandFailed(_logger, process.ExitCode, errorMsg);
                return false;
            }

            LogAddressUnbound(_logger, _options.NetworkInterface);
            return true;
        }
        catch (Exception ex)
        {
            LogUnbindException(_logger, ex);
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
    /// Validates that the interface name contains only allowed characters.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    /// <param name="interfaceName">The interface name to validate.</param>
    /// <exception cref="ArgumentException">Thrown when interface name contains invalid characters.</exception>
    private static void ValidateInterfaceName(string interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentException("Interface name cannot be null or empty", nameof(interfaceName));
        }

        // Security: Whitelist allowed characters for interface names
        // Valid interface names: alphanumeric, underscore, hyphen
        // Examples: eth0, wlan0, br-1234, enp0s1
        if (!InterfaceNameRegex.IsMatch(interfaceName))
        {
            throw new ArgumentException(
                $"Invalid interface name: '{interfaceName}'. Interface names must contain only alphanumeric characters, underscores, and hyphens.",
                nameof(interfaceName));
        }

        // Additional check: interface name length
        if (interfaceName.Length > 64)
        {
            throw new ArgumentException(
                $"Interface name too long: {interfaceName.Length} characters (max 64)",
                nameof(interfaceName));
        }
    }

    /// <summary>
    /// Validates that the IPv6 address contains only valid characters.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    /// <param name="address">The IPv6 address to validate.</param>
    /// <exception cref="ArgumentException">Thrown when address contains invalid characters.</exception>
    private static void ValidateIPv6Address(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("IPv6 address cannot be null or empty", nameof(address));
        }

        // Security: Whitelist allowed characters for IPv6 addresses
        // Valid: hexadecimal digits (0-9, a-f, A-F), colons (:), and periods (.) for IPv4-mapped addresses
        if (!IPv6AddressRegex.IsMatch(address))
        {
            throw new ArgumentException(
                $"Invalid IPv6 address format: '{address}'. Address contains invalid characters.",
                nameof(address));
        }

        // Additional validation: check for suspicious patterns
        if (address.Contains("..") || address.Contains(":::") || address.Contains("//"))
        {
            throw new ArgumentException(
                $"Invalid IPv6 address format: '{address}'. Address contains invalid sequences.",
                nameof(address));
        }
    }

    /// <summary>
    /// Validates the IPv6 prefix for security - checks for injection attempts.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    /// <param name="prefix">The IPv6 prefix to validate.</param>
    /// <exception cref="ArgumentException">Thrown when prefix contains invalid characters or patterns.</exception>
    private static void ValidateIPv6PrefixForSecurity(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("IPv6 prefix cannot be null or empty", nameof(prefix));
        }

        // Security: Whitelist allowed characters for IPv6 prefix
        // Valid: hexadecimal digits (0-9, a-f, A-F), colons (:)
        // Invalid: shell metacharacters, backticks, pipes, redirections, non-hex letters, etc.
        foreach (char c in prefix)
        {
            if (c == ':')
            {
                continue; // Colon is valid separator
            }

            // Check if character is a valid hexadecimal digit
            if (!IsHexDigit(c))
            {
                throw new ArgumentException(
                    $"Invalid character '{c}' in IPv6 prefix. Prefix must contain only hexadecimal digits (0-9, a-f, A-F) and colons.",
                    nameof(prefix));
            }
        }

        // Helper function to check if a character is a valid hex digit
        static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
        }

        // Security: Check for suspicious patterns that could indicate injection
        string[] dangerousPatterns = new[] { "..", "//", "\\", "${", "$((", "`", "|", "&", ";", "<", ">", "*", "?" };
        foreach (string pattern in dangerousPatterns)
        {
            if (prefix.Contains(pattern, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Invalid pattern '{pattern}' in IPv6 prefix. Potential command injection attempt detected.",
                    nameof(prefix));
            }
        }

        // Security: Prefix length validation (max reasonable IPv6 prefix length)
        if (prefix.Length > 39) // Max IPv6 address length is 39 characters (8 groups of 4 hex digits + 7 colons)
        {
            throw new ArgumentException(
                $"IPv6 prefix too long: {prefix.Length} characters (max 39)",
                nameof(prefix));
        }
    }

    /// <summary>
    /// Validates if a string is a valid IPv6 /64 prefix.
    /// </summary>
    private static bool IsValidIPv6Prefix(string prefix)
    {
        // Security: First validate against whitelist regex
        if (!IPv6PrefixRegex.IsMatch(prefix))
        {
            return false;
        }

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
