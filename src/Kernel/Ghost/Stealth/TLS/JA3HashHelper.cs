using System;
using System.Security.Cryptography;
using System.Text;

namespace Ghost.Stealth.TLS;

/// <summary>
/// Internal helper for computing JA3 fingerprint hex strings.
///
/// Rationale: Historically JA3 fingerprints used MD5 to produce a 32-character
/// hex string. To satisfy static analysis (CA5351) and follow stronger
/// cryptographic guidance, this helper now produces a SHA-256 digest encoded
/// as a lowercase 64-character hex string. The helper provides both
/// span-based and string-based overloads and prefers the runtime-provided
/// SHA256 APIs (TryHashData / HashData) to avoid custom implementations.
/// </summary>
internal static class JA3HashHelper
{
    /// <summary>
    /// Compute the JA3 SHA-256 digest as a lowercase hex string from the provided bytes.
    /// </summary>
    /// <param name="data">The input bytes (JA3 string encoded as UTF-8, or raw ClientHello bytes).</param>
    /// <returns>Lowercase 64-character SHA-256 hex digest.</returns>
    internal static string ComputeJa3Sha256Hex(ReadOnlySpan<byte> data)
    {
        // Early exit: empty input is still a valid hash calculation.
        // SHA-256 outputs 32 bytes; allocate on the stack to avoid heap allocations.
        Span<byte> dest = stackalloc byte[32];

        try
        {
            if (SHA256.TryHashData(data, dest, out _))
            {
                return Convert.ToHexString(dest).ToLowerInvariant();
            }
        }
        catch (MissingMethodException)
        {
            // Some runtimes may not expose TryHashData; fall back to HashData below.
        }

        byte[] result = SHA256.HashData(data);
        return Convert.ToHexString(result).ToLowerInvariant();
    }

    /// <summary>
    /// Compute the JA3 SHA-256 digest as a lowercase hex string from the provided JA3 string.
    /// </summary>
    /// <param name="ja3String">The JA3 string (ASCII/UTF-8) to hash.</param>
    /// <returns>Lowercase 64-character SHA-256 hex digest.</returns>
    internal static string ComputeJa3Sha256Hex(string ja3String)
    {
        ArgumentNullException.ThrowIfNull(ja3String);

        // Encode to UTF8 bytes and reuse the span-based implementation.
        byte[] bytes = Encoding.UTF8.GetBytes(ja3String);
        return ComputeJa3Sha256Hex(bytes);
    }
}
