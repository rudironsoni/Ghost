using System.Security.Cryptography;
using System.Text;

namespace Ghost.Stealth.TLS;

/// <summary>
/// Represents a JA3 TLS fingerprint profile.
/// JA3 fingerprints are composed of: TLS version, cipher suites, extensions, elliptic curves, and EC point formats.
/// </summary>
public sealed class JA3Profile
{
    /// <summary>
    /// TLS version (769 = TLS 1.0, 770 = TLS 1.1, 771 = TLS 1.2, 772 = TLS 1.3).
    /// </summary>
    public int TLSVersion { get; set; }

    /// <summary>
    /// List of cipher suite identifiers in preferred order.
    /// </summary>
    public List<int> CipherSuites { get; set; } = [];

    /// <summary>
    /// List of TLS extension identifiers in order.
    /// </summary>
    public List<int> Extensions { get; set; } = [];

    /// <summary>
    /// List of supported elliptic curves (named groups).
    /// </summary>
    public List<int> EllipticCurves { get; set; } = [];

    /// <summary>
    /// List of EC point format identifiers.
    /// </summary>
    public List<int> ECPointFormats { get; set; } = [];

    /// <summary>
    /// Converts the profile to a JA3 string representation.
    /// Format: TLSVersion,CipherSuites,Extensions,EllipticCurves,ECPointFormats
    /// </summary>
    public string ToJA3String()
    {
        string cipherStr = string.Join("-", CipherSuites);
        string extStr = string.Join("-", Extensions);
        string curveStr = string.Join("-", EllipticCurves);
        string formatStr = string.Join("-", ECPointFormats);

        return $"{TLSVersion},{cipherStr},{extStr},{curveStr},{formatStr}";
    }

    /// <summary>
    /// Generates the MD5 hash of the JA3 string (standard JA3 fingerprint).
    /// MD5 is used here by specification, not for cryptographic security.
    /// </summary>
    // MD5 is required for JA3 TLS fingerprinting (industry standard per JA3 specification).
    // We encapsulate the MD5 usage in an internal helper to limit the scope and make
    // the intent explicit. Although MD5 is a broken cryptographic hash for security
    // use, JA3 uses it only for deterministic fingerprinting of TLS ClientHello
    // packets as per the JA3 specification.
    public string ToJA3Hash()
    {
        string ja3String = ToJA3String();
        return JA3HashHelper.ComputeMd5Hex(ja3String);
    }

    internal static class JA3HashHelper
    {
        /// <summary>
        /// Compute the MD5 hash of the input and return a lowercase hex string.
        /// MD5 is used here because the JA3 fingerprint specification requires it.
        /// </summary>
        internal static string ComputeMd5Hex(string input)
        {
            // TODO: Open an issue to document MD5 rationale and any future migration.
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            // Use the static API preferred by analyzers when available.
            // MD5 is used per JA3 spec for fingerprinting only.
            byte[] hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Creates a deep copy of this profile.
    /// </summary>
    public JA3Profile Clone()
    {
        return new JA3Profile
        {
            TLSVersion = TLSVersion,
            CipherSuites = new List<int>(CipherSuites),
            Extensions = new List<int>(Extensions),
            EllipticCurves = new List<int>(EllipticCurves),
            ECPointFormats = new List<int>(ECPointFormats)
        };
    }
}
