namespace Ghost.Session;

/// <summary>
/// Configuration options for SessionManager.
/// </summary>
public sealed class SessionManagerOptions
{
    /// <summary>
    /// Storage backend to use.
    /// </summary>
    public SessionStorageBackend Backend { get; set; } = SessionStorageBackend.FileSystem;

    /// <summary>
    /// Directory path for filesystem storage (used when Backend is FileSystem).
    /// </summary>
    public string StoragePath { get; set; } = Path.Combine(Path.GetTempPath(), "ghost-sessions");

    /// <summary>
    /// Redis connection string (used when Backend is Redis).
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Default time-to-live for sessions.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether to encrypt session data.
    /// </summary>
    public bool EnableEncryption { get; set; } = true;

    /// <summary>
    /// Encryption key for session data. If null, a key will be generated.
    /// </summary>
    public byte[]? EncryptionKey { get; set; }

    /// <summary>
    /// Whether to compress session data before storage.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Time provider for time-based operations. Defaults to <see cref="TimeProvider.System"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}

/// <summary>
/// Storage backend options for session persistence.
/// </summary>
public enum SessionStorageBackend
{
    /// <summary>
    /// Store sessions as encrypted JSON files on the filesystem.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Store sessions in Redis with automatic expiry.
    /// </summary>
    Redis
}
