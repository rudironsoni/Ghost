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
    /// Generates the SHA-256 hash of the JA3 string.
    ///
    /// NOTE: This repository historically used MD5 to match the JA3 specification.
    /// To eliminate usages of broken algorithms (CA5351) we now produce a
    /// SHA-256-based fingerprint encoded as a lowercase 64-character hex string.
    /// Callers should be aware this changes the fingerprint semantics and length.
    public string ToJA3Hash()
    {
        string ja3String = ToJA3String();
        return JA3HashHelper.ComputeJa3Sha256Hex(ja3String);
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
