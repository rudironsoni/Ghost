using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using StackExchange.Redis;

namespace Ghost.Session;

/// <summary>
/// Manages browser session persistence with support for filesystem and Redis storage.
/// </summary>
public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly SessionManagerOptions _options;
    private readonly ILogger<SessionManager> _logger;
    private readonly byte[] _encryptionKey;
    private readonly ConnectionMultiplexer? _redis;
    private readonly IDatabase? _redisDb;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, Exception?> _logInitializedRedis =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "InitializedRedis"), "SessionManager initialized with Redis backend");

    private static readonly Action<ILogger, string, Exception?> _logInitializedFileSystem =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "InitializedFileSystem"), "SessionManager initialized with FileSystem backend at {Path}");

    private static readonly Action<ILogger, string, string, Exception?> _logSessionSaved =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(3, "SessionSaved"), "Saved session {SessionId} for platform {Platform}");

    private static readonly Action<ILogger, string, string, Exception?> _logSessionSaveFailed =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(4, "SessionSaveFailed"), "Failed to save session {SessionId} for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logNoSessionFound =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, "NoSessionFound"), "No valid session found for platform {Platform}");

    private static readonly Action<ILogger, int, string, Exception?> _logRestoredCookies =
        LoggerMessage.Define<int, string>(LogLevel.Debug, new EventId(6, "RestoredCookies"), "Restored {Count} cookies for session {SessionId}");

    private static readonly Action<ILogger, string, string, Exception?> _logSessionRestored =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(7, "SessionRestored"), "Restored session {SessionId} for platform {Platform}");

    private static readonly Action<ILogger, string, string, Exception?> _logSessionRestoreFailed =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(8, "SessionRestoreFailed"), "Failed to restore session {SessionId} for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logSessionLoadDeserializeFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(9, "SessionLoadDeserializeFailed"), "Failed to deserialize session for platform {Platform}");

    private static readonly Action<ILogger, string, string, Exception?> _logSessionExpired =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(10, "SessionExpired"), "Session {SessionId} for platform {Platform} has expired");

    private static readonly Action<ILogger, string, Exception?> _logSessionLoadFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(11, "SessionLoadFailed"), "Failed to load session for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logSessionDeleted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(12, "SessionDeleted"), "Deleted session(s) for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logSessionDeleteFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(13, "SessionDeleteFailed"), "Failed to delete session for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logSessionListFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(14, "SessionListFailed"), "Failed to list sessions for platform {Platform}");

    private static readonly Action<ILogger, Exception?> _logRedisHandlesExpiry =
        LoggerMessage.Define(LogLevel.Information, new EventId(15, "RedisHandlesExpiry"), "Redis backend handles expiry automatically");

    private static readonly Action<ILogger, int, Exception?> _logCleanedExpiredSessions =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(16, "CleanedExpiredSessions"), "Cleaned up {Count} expired sessions");

    private static readonly Action<ILogger, Exception?> _logCleanupFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(17, "CleanupFailed"), "Failed to cleanup expired sessions");

    public SessionManager(IOptions<SessionManagerOptions> options, ILogger<SessionManager>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? NullLogger<SessionManager>.Instance;

        // Initialize encryption key
        if (_options.EnableEncryption)
        {
            _encryptionKey = _options.EncryptionKey ?? SessionEncryption.GenerateKey();
        }
        else
        {
            _encryptionKey = Array.Empty<byte>();
        }

        // Initialize storage backend
        if (_options.Backend == SessionStorageBackend.Redis)
        {
            if (string.IsNullOrWhiteSpace(_options.RedisConnectionString))
            {
                throw new InvalidOperationException("Redis connection string is required when using Redis backend");
            }

            _redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
            _redisDb = _redis.GetDatabase();
            _logInitializedRedis(_logger, null);
        }
        else
        {
            // Ensure storage directory exists for filesystem backend
            Directory.CreateDirectory(_options.StoragePath);
            _logInitializedFileSystem(_logger, _options.StoragePath, null);
        }
    }

    public async Task<string> SaveSessionAsync(
        IBrowserContext context,
        string platform,
        string? sessionId = null,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        string id = sessionId ?? Guid.NewGuid().ToString();
        TimeSpan expiry = ttl ?? _options.DefaultTtl;

        try
        {
            // Get cookies
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);

            // Get storage state (includes localStorage and sessionStorage)
            string storageState = await context.StorageStateAsync().ConfigureAwait(false);
            StorageStateJson? storageStateJson = JsonSerializer.Deserialize<StorageStateJson>(storageState);

            var session = new BrowserSession
            {
                SessionId = id,
                Platform = platform,
                Cookies = cookies.ToList(),
                LocalStorage = new Dictionary<string, string>(),
                SessionStorage = new Dictionary<string, string>(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiry)
            };

            // Extract localStorage and sessionStorage from origins
            if (storageStateJson?.Origins != null)
            {
                foreach (OriginState origin in storageStateJson.Origins)
                {
                    if (origin.LocalStorage != null)
                    {
                        foreach (StorageItem item in origin.LocalStorage)
                        {
                            string key = $"{origin.Origin}::{item.Name}";
                            session.LocalStorage[key] = item.Value;
                        }
                    }
                }
            }

            // Serialize session
            string json = JsonSerializer.Serialize(session, JsonOptions);

            // Optionally compress
            if (_options.EnableCompression)
            {
                json = await CompressAsync(json).ConfigureAwait(false);
            }

            // Optionally encrypt
            if (_options.EnableEncryption)
            {
                json = SessionEncryption.Encrypt(json, _encryptionKey);
            }

            // Store based on backend
            if (_options.Backend == SessionStorageBackend.Redis)
            {
                await SaveToRedisAsync(platform, id, json, expiry, ct).ConfigureAwait(false);
            }
            else
            {
                await SaveToFileSystemAsync(platform, id, json, ct).ConfigureAwait(false);
            }

            _logSessionSaved(_logger, id, platform, null);
            return id;
        }
        catch (Exception ex)
        {
            _logSessionSaveFailed(_logger, id, platform, ex);
            throw;
        }
    }

    public async Task<bool> RestoreSessionAsync(
        IBrowserContext context,
        string platform,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        BrowserSession? session = await LoadSessionAsync(platform, sessionId, ct).ConfigureAwait(false);
        if (session == null)
        {
            _logNoSessionFound(_logger, platform, null);
            return false;
        }

        try
        {
            // Restore cookies (convert BrowserContextCookiesResult to Playwright Cookie)
            if (session.Cookies.Count > 0)
            {
                var cookiesToAdd = session.Cookies.Select(c => new Microsoft.Playwright.Cookie
                {
                    Name = c.Name,
                    Value = c.Value,
                    Domain = c.Domain,
                    Path = c.Path,
                    Expires = c.Expires,
                    HttpOnly = c.HttpOnly,
                    Secure = c.Secure,
                    SameSite = c.SameSite
                }).ToList();

                await context.AddCookiesAsync(cookiesToAdd).ConfigureAwait(false);
                _logRestoredCookies(_logger, session.Cookies.Count, session.SessionId, null);
            }

            // Note: Restoring localStorage and sessionStorage requires page-level injection
            // This will be done via AddInitScriptAsync when a page is created
            // Store the session data in context for later use

            _logSessionRestored(_logger, session.SessionId, platform, null);
            return true;
        }
        catch (Exception ex)
        {
            _logSessionRestoreFailed(_logger, session.SessionId, platform, ex);
            throw;
        }
    }

    public async Task<BrowserSession?> LoadSessionAsync(
        string platform,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        try
        {
            string? json;

            if (_options.Backend == SessionStorageBackend.Redis)
            {
                json = await LoadFromRedisAsync(platform, sessionId, ct).ConfigureAwait(false);
            }
            else
            {
                json = await LoadFromFileSystemAsync(platform, sessionId, ct).ConfigureAwait(false);
            }

            if (json == null)
            {
                return null;
            }

            // Optionally decrypt
            if (_options.EnableEncryption)
            {
                json = SessionEncryption.Decrypt(json, _encryptionKey);
            }

            // Optionally decompress
            if (_options.EnableCompression)
            {
                json = await DecompressAsync(json).ConfigureAwait(false);
            }

            BrowserSession? session = JsonSerializer.Deserialize<BrowserSession>(json, JsonOptions);

            if (session == null)
            {
                _logSessionLoadDeserializeFailed(_logger, platform, null);
                return null;
            }

            // Check if expired
            if (session.IsExpired())
            {
                _logSessionExpired(_logger, session.SessionId, platform, null);
                await DeleteSessionAsync(platform, session.SessionId, ct).ConfigureAwait(false);
                return null;
            }

            return session;
        }
        catch (Exception ex)
        {
            _logSessionLoadFailed(_logger, platform, ex);
            return null;
        }
    }

    public async Task DeleteSessionAsync(string platform, string? sessionId = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        try
        {
            if (_options.Backend == SessionStorageBackend.Redis)
            {
                await DeleteFromRedisAsync(platform, sessionId, ct).ConfigureAwait(false);
            }
            else
            {
                await DeleteFromFileSystemAsync(platform, sessionId, ct).ConfigureAwait(false);
            }

            _logSessionDeleted(_logger, platform, null);
        }
        catch (Exception ex)
        {
            _logSessionDeleteFailed(_logger, platform, ex);
            throw;
        }
    }

    public async Task<List<string>> ListSessionsAsync(string platform, bool includeExpired = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        try
        {
            if (_options.Backend == SessionStorageBackend.Redis)
            {
                return await ListSessionsFromRedisAsync(platform, includeExpired, ct).ConfigureAwait(false);
            }
            else
            {
                return await ListSessionsFromFileSystemAsync(platform, includeExpired, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logSessionListFailed(_logger, platform, ex);
            return new List<string>();
        }
    }

    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken ct = default)
    {
        int count = 0;

        try
        {
            if (_options.Backend == SessionStorageBackend.Redis)
            {
                // Redis handles expiry automatically
                _logRedisHandlesExpiry(_logger, null);
                return 0;
            }
            else
            {
                string[] platformDirs = Directory.GetDirectories(_options.StoragePath);
                foreach (string platformDir in platformDirs)
                {
                    string platform = Path.GetFileName(platformDir);
                    List<string> sessions = await ListSessionsAsync(platform, includeExpired: true, ct).ConfigureAwait(false);

                    foreach (string sessionId in sessions)
                    {
                        BrowserSession? session = await LoadSessionAsync(platform, sessionId, ct).ConfigureAwait(false);
                        if (session?.IsExpired() == true)
                        {
                            await DeleteSessionAsync(platform, sessionId, ct).ConfigureAwait(false);
                            count++;
                        }
                    }
                }

                _logCleanedExpiredSessions(_logger, count, null);
            }
        }
        catch (Exception ex)
        {
            _logCleanupFailed(_logger, ex);
        }

        return count;
    }

    // Redis Backend Methods
    private async Task SaveToRedisAsync(string platform, string sessionId, string data, TimeSpan expiry, CancellationToken ct)
    {
        if (_redisDb == null) throw new InvalidOperationException("Redis not initialized");

        string key = GetRedisKey(platform, sessionId);
        await _redisDb.StringSetAsync(key, data, expiry).ConfigureAwait(false);
    }

    private async Task<string?> LoadFromRedisAsync(string platform, string? sessionId, CancellationToken ct)
    {
        if (_redisDb == null) throw new InvalidOperationException("Redis not initialized");

        if (sessionId != null)
        {
            string key = GetRedisKey(platform, sessionId);
            RedisValue value = await _redisDb.StringGetAsync(key).ConfigureAwait(false);
            return value.HasValue ? value.ToString() : null;
        }
        else
        {
            // Load latest session
            List<string> sessions = await ListSessionsFromRedisAsync(platform, includeExpired: false, ct).ConfigureAwait(false);
            if (sessions.Count == 0) return null;

            string latestKey = GetRedisKey(platform, sessions[0]);
            RedisValue value = await _redisDb.StringGetAsync(latestKey).ConfigureAwait(false);
            return value.HasValue ? value.ToString() : null;
        }
    }

    private async Task DeleteFromRedisAsync(string platform, string? sessionId, CancellationToken ct)
    {
        if (_redisDb == null) throw new InvalidOperationException("Redis not initialized");

        if (sessionId != null)
        {
            string key = GetRedisKey(platform, sessionId);
            await _redisDb.KeyDeleteAsync(key).ConfigureAwait(false);
        }
        else
        {
            // Delete all sessions for platform
            List<string> sessions = await ListSessionsFromRedisAsync(platform, includeExpired: true, ct).ConfigureAwait(false);
            foreach (string id in sessions)
            {
                string key = GetRedisKey(platform, id);
                await _redisDb.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }
    }

    private async Task<List<string>> ListSessionsFromRedisAsync(string platform, bool includeExpired, CancellationToken ct)
    {
        if (_redis == null) throw new InvalidOperationException("Redis not initialized");

        string pattern = $"ghost:session:{platform}:*";
        var keys = new List<string>();

        IServer server = _redis.GetServer(_redis.GetEndPoints().First());
        await foreach (RedisKey key in server.KeysAsync(pattern: pattern).ConfigureAwait(false))
        {
            string sessionId = key.ToString().Split(':').Last();
            keys.Add(sessionId);
        }

        return keys;
    }

    // FileSystem Backend Methods
    private async Task SaveToFileSystemAsync(string platform, string sessionId, string data, CancellationToken ct)
    {
        string filePath = GetFilePath(platform, sessionId);
        string? directory = Path.GetDirectoryName(filePath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, data, ct).ConfigureAwait(false);
    }

    private async Task<string?> LoadFromFileSystemAsync(string platform, string? sessionId, CancellationToken ct)
    {
        if (sessionId != null)
        {
            string filePath = GetFilePath(platform, sessionId);
            if (!File.Exists(filePath)) return null;

            return await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }
        else
        {
            // Load latest session
            List<string> sessions = await ListSessionsFromFileSystemAsync(platform, includeExpired: false, ct).ConfigureAwait(false);
            if (sessions.Count == 0) return null;

            string latestPath = GetFilePath(platform, sessions[0]);
            return await File.ReadAllTextAsync(latestPath, ct).ConfigureAwait(false);
        }
    }

    private async Task DeleteFromFileSystemAsync(string platform, string? sessionId, CancellationToken ct)
    {
        if (sessionId != null)
        {
            string filePath = GetFilePath(platform, sessionId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        else
        {
            // Delete all sessions for platform
            string platformDir = Path.Combine(_options.StoragePath, platform);
            if (Directory.Exists(platformDir))
            {
                Directory.Delete(platformDir, recursive: true);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task<List<string>> ListSessionsFromFileSystemAsync(string platform, bool includeExpired, CancellationToken ct)
    {
        string platformDir = Path.Combine(_options.StoragePath, platform);
        if (!Directory.Exists(platformDir))
        {
            return new List<string>();
        }

        string[] files = Directory.GetFiles(platformDir, "*.session");
        var sessionIds = new List<string>();

        foreach (string file in files)
        {
            string sessionId = Path.GetFileNameWithoutExtension(file);
            sessionIds.Add(sessionId);
        }

        // Sort by modification time (newest first)
        sessionIds = sessionIds
            .OrderByDescending(id => File.GetLastWriteTimeUtc(GetFilePath(platform, id)))
            .ToList();

        await Task.CompletedTask.ConfigureAwait(false);
        return sessionIds;
    }

    // Helper Methods
    private static string GetRedisKey(string platform, string sessionId)
    {
        return $"ghost:session:{platform}:{sessionId}";
    }

    private string GetFilePath(string platform, string sessionId)
    {
        return Path.Combine(_options.StoragePath, platform, $"{sessionId}.session");
    }

    private static async Task<string> CompressAsync(string text)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            await gzipStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        return Convert.ToBase64String(outputStream.ToArray());
    }

    private static async Task<string> DecompressAsync(string compressedText)
    {
        byte[] bytes = Convert.FromBase64String(compressedText);
        using var inputStream = new MemoryStream(bytes);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        await gzipStream.CopyToAsync(outputStream).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
    }

    public void Dispose()
    {
        if (_disposed) return;

        _redis?.Dispose();
        _disposed = true;
    }

    // Helper class for deserializing storage state
    private sealed class StorageStateJson
    {
        public List<OriginState>? Origins { get; set; }
    }

    private sealed class OriginState
    {
        public string Origin { get; set; } = string.Empty;
        public List<StorageItem>? LocalStorage { get; set; }
    }

    private sealed class StorageItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
