using System;
using System.Security.Cryptography;
using System.Text;

namespace Ghost.Stealth.TLS;

/// <summary>
/// Internal helper to compute the JA3 MD5 hash.
/// MD5 is required by the JA3 specification for fingerprinting TLS ClientHello messages.
/// The helper is internal and limits the scope of MD5 usage to this purpose only.
/// TODO: Open a follow-up issue to audit MD5 usage if analyzers still flag it.
/// </summary>
internal static class JA3HashHelper
{
    internal static string ComputeJa3Md5Hex(ReadOnlySpan<byte> input)
    {
        // MD5 is used here because the JA3 fingerprint specification requires it.
        // This usage is non-cryptographic and intended only for deterministic
        // fingerprinting of TLS ClientHello bytes.
        Span<byte> hash = stackalloc byte[16];
#if NET6_0_OR_GREATER
        // Use static HashData API when available
        byte[] result = MD5.HashData(input);
        return Convert.ToHexString(result).ToLowerInvariant();
#else
        using MD5 md5 = MD5.Create();
        byte[] result = md5.ComputeHash(input.ToArray());
        return Convert.ToHexString(result).ToLowerInvariant();
#endif
    }

    internal static string ComputeJa3Md5Hex(ReadOnlySpan<char> input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input.ToString());
        return ComputeJa3Md5Hex(bytes);
    }

    internal static string ComputeJa3Md5Hex(ReadOnlySpan<byte> inputBytes, bool unused = false)
    {
        return ComputeJa3Md5Hex(inputBytes);
    }

    internal static string ComputeJa3Md5Hex(ReadOnlySpan<char> input, int dummy)
    {
        return ComputeJa3Md5Hex(input);
    }
}
