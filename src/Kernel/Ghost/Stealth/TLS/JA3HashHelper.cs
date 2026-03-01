using System;
using System.Security.Cryptography;
using System.Text;

namespace Ghost.Stealth.TLS;

/// <summary>
/// Internal helper for computing the JA3 MD5 fingerprint hex string.
///
/// Rationale: The JA3 specification explicitly uses MD5 for generating a
/// 32-character hex fingerprint of a TLS ClientHello. Although MD5 is
/// cryptographically broken for security-sensitive scenarios, this helper
/// encapsulates and documents a very narrow, auditable use of MD5 strictly
/// for deterministic fingerprinting per the JA3 spec.
///
/// The helper exposes span-based and string-based overloads and uses the
/// runtime-provided MD5 hashing APIs (TryHashData / HashData) together with
/// Convert.ToHexString to avoid custom/hand-rolled MD5 implementations.
/// </summary>
internal static class JA3HashHelper
{
    /// <summary>
    /// Compute the JA3 MD5 digest as a lowercase hex string from the provided bytes.
    /// </summary>
    /// <param name="data">The input bytes (JA3 string encoded as UTF-8, or raw ClientHello bytes).</param>
    /// <returns>Lowercase 32-character MD5 hex digest.</returns>
    internal static string ComputeJa3Md5Hex(ReadOnlySpan<byte> data)
    {
        // Early exit for empty input is still a valid MD5 computation.
        // Allocate a 16-byte stack buffer for the MD5 digest (MD5 outputs 16 bytes).
        Span<byte> dest = stackalloc byte[16];

        // Prefer the TryHashData API which writes directly into the destination
        // span without extra allocations when available. Fall back to HashData
        // which returns a byte[] if TryHashData isn't supported on a specific runtime.
        try
        {
            if (MD5.TryHashData(data, dest, out _))
            {
                return Convert.ToHexString(dest).ToLowerInvariant();
            }
        }
        catch (MissingMethodException)
        {
            // Some older runtimes may not expose TryHashData as a static method on MD5.
            // Fall through to the HashData call below which is broadly available.
        }

        // Fallback: use HashData which returns a byte[] and then convert to hex.
        byte[] result = MD5.HashData(data);
        return Convert.ToHexString(result).ToLowerInvariant();
    }

    /// <summary>
    /// Compute the JA3 MD5 digest as a lowercase hex string from the provided JA3 string.
    /// </summary>
    /// <param name="ja3String">The JA3 string (ASCII/UTF-8) to hash.</param>
    /// <returns>Lowercase 32-character MD5 hex digest.</returns>
    internal static string ComputeJa3Md5Hex(string ja3String)
    {
        ArgumentNullException.ThrowIfNull(ja3String);

        // Encode to UTF8 bytes and reuse the span-based implementation.
        byte[] bytes = Encoding.UTF8.GetBytes(ja3String);
        return ComputeJa3Md5Hex(bytes);
    }
}
