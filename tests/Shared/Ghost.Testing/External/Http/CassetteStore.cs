using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ghost.Testing.External.Http;

public sealed class CassetteStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _baseDirectory;

    public CassetteStore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Cassette base directory is required.", nameof(baseDirectory));
        }

        _baseDirectory = baseDirectory;
        Directory.CreateDirectory(_baseDirectory);
    }

    public string BuildKey(HttpMethod method, Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(requestUri);

        string normalizedRequest = $"{method.Method.ToUpperInvariant()} {NormalizeUri(requestUri)}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRequest));
        return Convert.ToHexString(hash).ToLowerInvariant()[..20];
    }

    public async Task<CassetteEnvelope?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        string path = GetPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CassetteEnvelope>(stream, SerializerOptions, cancellationToken)
            ;
    }

    public async Task WriteAsync(string key, CassetteEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        string path = GetPath(key);
        string tempPath = $"{path}.tmp";

        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, envelope, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public string GetPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cassette key is required.", nameof(key));
        }

        return Path.Combine(_baseDirectory, $"{key}.json");
    }

    private static string NormalizeUri(Uri uri)
    {
        IReadOnlyList<KeyValuePair<string, string>> sortedPairs = QueryStringUtilities
            .Parse(uri.Query)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToList();

        UriBuilder builder = new(uri)
        {
            Fragment = string.Empty,
            Query = QueryStringUtilities.BuildNormalizedQuery(sortedPairs)
        };

        return builder.Uri.AbsoluteUri;
    }
}
