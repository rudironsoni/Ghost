using System;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Pipelines;

/// <summary>
/// Logger messages for MediaPipeline.
/// </summary>
public static partial class MediaPipelineLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing media download from URL: {Url}")]
    public static partial void LogProcessingMedia(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File size ({ContentLength} bytes) exceeds maximum allowed size ({MaxFileSize} bytes)")]
    public static partial void LogFileSizeExceeded(this ILogger logger, long contentLength, long maxFileSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sanitized filename: {FileName}")]
    public static partial void LogSanitizedFilename(this ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File extension '{Extension}' is not in allowed list")]
    public static partial void LogExtensionNotAllowed(this ILogger logger, string extension);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading file to: {LocalPath}")]
    public static partial void LogDownloadingFile(this ILogger logger, string localPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saving file content ({ContentLength} bytes) to: {LocalPath}")]
    public static partial void LogSavingFile(this ILogger logger, long contentLength, string localPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully saved file: {LocalPath} ({Size} bytes)")]
    public static partial void LogFileSaved(this ILogger logger, string localPath, long size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Calculated checksum for {LocalPath}: {Checksum}")]
    public static partial void LogChecksumCalculated(this ILogger logger, string localPath, string checksum);
}

/// <summary>
/// Implementation of media pipeline for downloading and processing files.
/// </summary>
public class MediaPipeline : IMediaPipeline
{
    private readonly HttpClient _httpClient;
    private readonly MediaPipelineOptions _options;
    private readonly ILogger<MediaPipeline>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaPipeline"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for downloads.</param>
    /// <param name="options">Pipeline configuration options.</param>
    public MediaPipeline(HttpClient httpClient, MediaPipelineOptions options)
        : this(httpClient, options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaPipeline"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for downloads.</param>
    /// <param name="options">Pipeline configuration options.</param>
    /// <param name="logger">Optional logger for file operations.</param>
    public MediaPipeline(HttpClient httpClient, MediaPipelineOptions options, ILogger<MediaPipeline>? logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MediaItem> ProcessAsync(MediaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("URL cannot be empty.", nameof(request));
        }

        // Validate output path exists and is within allowed boundaries
        ValidateOutputPath(request.OutputPath);

        _logger?.LogProcessingMedia(request.Url);

        // Download file
        using HttpResponseMessage response = await _httpClient.GetAsync(request.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Check file size if Content-Length is available
        if (response.Content.Headers.ContentLength.HasValue)
        {
            long contentLength = response.Content.Headers.ContentLength.Value;
            if (contentLength > _options.MaxFileSize)
            {
                _logger?.LogFileSizeExceeded(contentLength, _options.MaxFileSize);
                throw new InvalidOperationException($"File size ({contentLength} bytes) exceeds maximum allowed size ({_options.MaxFileSize} bytes).");
            }
        }

        // Extract and validate filename from URL or request
        string fileName = ValidateAndSanitizeFileName(request.FileName ?? GetFileNameFromUrl(request.Url));
        _logger?.LogSanitizedFilename(fileName);

        // Validate extension if AllowedExtensions is specified
        if (_options.AllowedExtensions.Count > 0)
        {
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension.ToLowerInvariant()))
            {
                _logger?.LogExtensionNotAllowed(extension);
                throw new InvalidOperationException($"File extension '{extension}' is not allowed.");
            }
        }

        // Validate and construct secure local path
        string localPath = GetSecureLocalPath(request.OutputPath, fileName);

        _logger?.LogDownloadingFile(localPath);

        // Ensure directory exists
        string? directoryName = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        // Save file
        _logger?.LogSavingFile(response.Content.Headers.ContentLength ?? -1, localPath);

        FileStream fs = File.Create(localPath);
        try
        {
            await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }
        finally
        {
            await fs.DisposeAsync().ConfigureAwait(false);
        }

        long fileLength = new FileInfo(localPath).Length;
        _logger?.LogFileSaved(localPath, fileLength);

        // Calculate checksum if enabled
        string? checksum = null;
        if (_options.CalculateChecksum)
        {
            checksum = await CalculateChecksumAsync(localPath, ct).ConfigureAwait(false);
            if (checksum != null)
            {
                _logger?.LogChecksumCalculated(localPath, checksum);
            }
        }

        return new MediaItem
        {
            Url = request.Url,
            LocalPath = localPath,
            Size = new FileInfo(localPath).Length,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            Checksum = checksum
        };
    }

    /// <summary>
    /// Validates that the output path is safe and does not contain path traversal sequences.
    /// </summary>
    /// <param name="outputPath">The output path to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the path is invalid.</exception>
    /// <exception cref="SecurityException">Thrown when path traversal is detected.</exception>
    private static void ValidateOutputPath(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        // Check for path traversal sequences in the output path itself
        if (ContainsPathTraversal(outputPath))
        {
            throw new SecurityException($"Output path contains path traversal sequences: '{outputPath}'");
        }
    }

    /// <summary>
    /// Checks if a string contains path traversal sequences.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if path traversal sequences are found; otherwise, false.</returns>
    private static bool ContainsPathTraversal(string path)
    {
        // Check for common path traversal patterns
        // Use normalized separators for consistent checking
        string normalizedPath = path.Replace('\\', '/');

        // Check for ".." sequences with various separators
        if (normalizedPath.Contains("../", StringComparison.Ordinal) ||
            normalizedPath.Contains("/..", StringComparison.Ordinal) ||
            normalizedPath == "..")
        {
            return true;
        }

        // Check for URL-encoded traversal attempts
        if (path.Contains("..%2F", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2F..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%5C", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..%5c", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for double encoding attempts
        if (path.Contains("%252e", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%252f", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for null byte injection (both raw and URL-encoded)
        if (path.Contains('\0') || path.Contains("%00", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string GetFileNameFromUrl(string url)
    {
        string fileName;

        try
        {
            var uri = new Uri(url);
            string path = uri.AbsolutePath;

            // URL-decode the path first to handle encoded characters
            path = Uri.UnescapeDataString(path);

            // Path.GetFileName removes directory components from the URL path
            // This is called AFTER decoding to properly handle encoded traversal sequences
            fileName = Path.GetFileName(path);
        }
        catch (UriFormatException)
        {
            // If URL parsing fails, try to extract last path segment
            int lastSlash = url.LastIndexOf('/');
            fileName = lastSlash >= 0 ? url[(lastSlash + 1)..] : url;
            // URL-decode the extracted filename
            fileName = Uri.UnescapeDataString(fileName);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "download";
        }

        return fileName;
    }

    /// <summary>
    /// Validates and sanitizes a filename to prevent path traversal attacks.
    /// Rejects filenames that contain directory components or path traversal sequences.
    /// </summary>
    /// <param name="fileName">The filename to validate and sanitize.</param>
    /// <returns>A sanitized filename safe for file system operations.</returns>
    /// <exception cref="ArgumentException">Thrown when the filename is null or empty.</exception>
    /// <exception cref="SecurityException">Thrown when path traversal is detected.</exception>
    private static string ValidateAndSanitizeFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // Reject filenames that start with path separators (absolute paths)
        if (fileName.StartsWith('/') || fileName.StartsWith('\\'))
        {
            throw new SecurityException($"Filename starts with path separator (absolute path not allowed): '{fileName}'");
        }

        // First check for path traversal sequences before any processing
        if (ContainsPathTraversal(fileName))
        {
            throw new SecurityException($"Filename contains path traversal sequences: '{fileName}'");
        }

        // Extract only the filename component (strips any directory paths)
        string sanitizedFileName = Path.GetFileName(fileName);

        // Double-check after Path.GetFileName
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            throw new ArgumentException("Filename is invalid or contains only directory components.");
        }

        // Verify no directory separators remain
        if (sanitizedFileName.Contains('/') || sanitizedFileName.Contains('\\'))
        {
            throw new SecurityException($"Filename contains directory separators: '{sanitizedFileName}'");
        }

        // Verify no parent directory references remain
        if (sanitizedFileName.Contains("..", StringComparison.Ordinal))
        {
            throw new SecurityException($"Filename contains parent directory references: '{sanitizedFileName}'");
        }

        // Check for and replace invalid filename characters
        char[] invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(sanitizedFileName.Length);
        foreach (char c in sanitizedFileName)
        {
            if (Array.Exists(invalidChars, invalid => invalid == c))
            {
                sanitized.Append('_');
            }
            else
            {
                sanitized.Append(c);
            }
        }

        string result = sanitized.ToString();

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException("Filename contains only invalid characters.");
        }

        // Sanitize Windows reserved device names by appending underscore
        // Windows reserves: CON, PRN, AUX, NUL, COM1-9, LPT1-9
        result = SanitizeWindowsReservedName(result);

        // Final validation - ensure the result is just a filename
        if (result != Path.GetFileName(result))
        {
            throw new SecurityException($"Filename validation failed for: '{fileName}'");
        }

        return result;
    }

    /// <summary>
    /// Sanitizes Windows reserved device names by appending an underscore.
    /// Windows reserves certain names that cannot be used as filenames: CON, PRN, AUX, NUL, COM1-9, LPT1-9.
    /// See: https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#naming-conventions
    /// </summary>
    /// <param name="fileName">The filename to check (may include extension).</param>
    /// <returns>The filename with underscore appended if it matches a reserved name.</returns>
    private static string SanitizeWindowsReservedName(string fileName)
    {
        // Get the name without extension for comparison
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        // Check if the name matches any Windows reserved device name (case-insensitive)
        if (IsWindowsReservedName(nameWithoutExtension))
        {
            return nameWithoutExtension + "_" + extension;
        }

        return fileName;
    }

    /// <summary>
    /// Checks if a filename (without extension) is a Windows reserved device name.
    /// </summary>
    /// <param name="name">The filename without extension.</param>
    /// <returns>True if the name is reserved on Windows.</returns>
    private static bool IsWindowsReservedName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Use case-insensitive comparison
        string upperName = name.ToUpperInvariant();

        // Check standard reserved names
        switch (upperName)
        {
            case "CON":
            case "PRN":
            case "AUX":
            case "NUL":
                return true;
        }

        // Check COM1-COM9 and LPT1-LPT9
        if (upperName.Length == 4)
        {
            if (upperName.StartsWith("COM", StringComparison.Ordinal) ||
                upperName.StartsWith("LPT", StringComparison.Ordinal))
            {
                // Check if last character is digit 1-9
                char lastChar = upperName[3];
                return lastChar >= '1' && lastChar <= '9';
            }
        }

        return false;
    }

    /// <summary>
    /// Constructs a secure local file path and validates it is within the intended directory.
    /// Uses defense-in-depth with multiple validation layers.
    /// </summary>
    /// <param name="outputPath">The base output directory.</param>
    /// <param name="fileName">The sanitized filename.</param>
    /// <returns>A fully qualified path that is guaranteed to be within the output directory.</returns>
    /// <exception cref="ArgumentException">Thrown when inputs are invalid.</exception>
    /// <exception cref="SecurityException">Thrown when the resolved path is outside the intended directory.</exception>
    private static string GetSecureLocalPath(string outputPath, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // Validate that filename contains no path separators
        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new SecurityException($"Filename contains path separators: '{fileName}'");
        }

        // Get fully qualified paths to prevent any relative path manipulation
        string fullyQualifiedOutputPath = Path.GetFullPath(outputPath);
        string localPath = Path.GetFullPath(Path.Combine(fullyQualifiedOutputPath, fileName));

        // Ensure the final path is within the intended output directory
        // Use OrdinalIgnoreCase for cross-platform compatibility
        // Also ensure the path has a trailing separator for proper prefix matching
        string outputPathWithSeparator = fullyQualifiedOutputPath.EndsWith(Path.DirectorySeparatorChar)
            || fullyQualifiedOutputPath.EndsWith(Path.AltDirectorySeparatorChar)
                ? fullyQualifiedOutputPath
                : fullyQualifiedOutputPath + Path.DirectorySeparatorChar;

        if (!localPath.StartsWith(fullyQualifiedOutputPath, StringComparison.OrdinalIgnoreCase) &&
            !localPath.StartsWith(outputPathWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Security violation: resolved path '{localPath}' is outside the allowed directory '{fullyQualifiedOutputPath}'.");
        }

        // Final verification: ensure the resolved path's directory exists or is creatable within output
        string? resolvedDirectory = Path.GetDirectoryName(localPath);
        if (string.IsNullOrEmpty(resolvedDirectory))
        {
            throw new SecurityException("Could not determine directory for resolved path.");
        }

        // Ensure resolved directory starts with output path (final defense)
        if (!resolvedDirectory.StartsWith(fullyQualifiedOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Security violation: file would be written outside allowed directory.");
        }

        return localPath;
    }

    private static async Task<string> CalculateChecksumAsync(string path, CancellationToken ct)
    {
        FileStream fs = File.OpenRead(path);
        try
        {
            byte[] hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }
        finally
        {
            await fs.DisposeAsync().ConfigureAwait(false);
        }
    }
}
