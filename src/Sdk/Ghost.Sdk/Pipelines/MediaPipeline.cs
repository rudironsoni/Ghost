using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Sdk.Pipelines;

/// <summary>
/// Implementation of media pipeline for downloading and processing files.
/// </summary>
public class MediaPipeline : IMediaPipeline
{
    private readonly HttpClient _httpClient;
    private readonly MediaPipelineOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaPipeline"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for downloads.</param>
    /// <param name="options">Pipeline configuration options.</param>
    public MediaPipeline(HttpClient httpClient, MediaPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<MediaItem> ProcessAsync(MediaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("URL cannot be empty.", nameof(request));
        }

        // Download file
        using HttpResponseMessage response = await _httpClient.GetAsync(request.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Check file size if Content-Length is available
        if (response.Content.Headers.ContentLength.HasValue)
        {
            long contentLength = response.Content.Headers.ContentLength.Value;
            if (contentLength > _options.MaxFileSize)
            {
                throw new InvalidOperationException($"File size ({contentLength} bytes) exceeds maximum allowed size ({_options.MaxFileSize} bytes).");
            }
        }

        // Determine filename
        string fileName = request.FileName ?? GetFileNameFromUrl(request.Url);

        // Validate extension if AllowedExtensions is specified
        if (_options.AllowedExtensions.Count > 0)
        {
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension.ToLowerInvariant()))
            {
                throw new InvalidOperationException($"File extension '{extension}' is not allowed.");
            }
        }

        string localPath = Path.Combine(request.OutputPath, fileName);

        // Ensure directory exists
        Directory.CreateDirectory(request.OutputPath);

        // Save file
        await using (FileStream fs = File.Create(localPath))
        {
            await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        // Calculate checksum if enabled
        string? checksum = null;
        if (_options.CalculateChecksum)
        {
            checksum = await CalculateChecksumAsync(localPath, ct).ConfigureAwait(false);
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

    private static string GetFileNameFromUrl(string url)
    {
        var uri = new Uri(url);
        string path = uri.AbsolutePath;
        string fileName = Path.GetFileName(path);

        if (string.IsNullOrEmpty(fileName))
        {
            return "download";
        }

        // Sanitize filename
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName;
    }

    private static async Task<string> CalculateChecksumAsync(string path, CancellationToken ct)
    {
        await using FileStream fs = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
