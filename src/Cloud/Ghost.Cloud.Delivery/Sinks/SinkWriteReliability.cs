using System.Security.Cryptography;
using System.Text;

namespace Ghost.Cloud.Delivery.Sinks;

internal sealed record SinkWritePlan(
    string ObjectName,
    string IdempotencyKey,
    string IntegritySha256);

internal static class SinkWritePlanner
{
    public static SinkWritePlan Create(string prefix, string extension, string? cursor, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentNullException.ThrowIfNull(payload);

        string normalizedPrefix = NormalizePrefix(prefix);
        string integritySha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string idempotencyKey = BuildIdempotencyKey(cursor, integritySha256);
        string objectName = $"{normalizedPrefix}/{idempotencyKey}.{extension}";

        return new SinkWritePlan(objectName, idempotencyKey, integritySha256);
    }

    private static string NormalizePrefix(string prefix) =>
        string.IsNullOrWhiteSpace(prefix) ? "results" : prefix.Trim('/');

    private static string BuildIdempotencyKey(string? cursor, string integritySha256) =>
        string.IsNullOrWhiteSpace(cursor)
            ? $"payload_{integritySha256[..24]}"
            : $"cursor_{Sanitize(cursor)}";

    private static string Sanitize(string value)
    {
        const int maxLength = 64;
        StringBuilder builder = new(capacity: Math.Min(value.Length, maxLength));

        foreach (char character in value.Take(maxLength))
        {
            _ = char.IsLetterOrDigit(character) || character is '-' or '_'
                ? builder.Append(character)
                : builder.Append('_');
        }

        return builder.ToString();
    }
}

internal sealed class SinkWriteTracker
{
    private readonly HashSet<string> _writeKeys = new(StringComparer.Ordinal);

    public bool TryStart(SinkWritePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return _writeKeys.Add(plan.IdempotencyKey);
    }

    public void MarkFailed(SinkWritePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _ = _writeKeys.Remove(plan.IdempotencyKey);
    }
}
