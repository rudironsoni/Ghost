namespace Ghost.Stealth.TLS;

/// <summary>
/// Randomizes JA3 TLS fingerprints to prevent bot detection.
/// Generates unique fingerprints while maintaining browser-realistic characteristics.
/// </summary>
public sealed class JA3Randomizer
{
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="JA3Randomizer"/> class.
    /// </summary>
    /// <param name="seed">Optional seed for reproducible randomization (testing only).</param>
    public JA3Randomizer(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    /// <summary>
    /// Generates a randomized JA3 profile based on a real browser.
    /// </summary>
    /// <param name="browserHint">Optional browser to base the profile on. If null, picks randomly.</param>
    /// <returns>A randomized JA3 profile.</returns>
    public JA3Profile GenerateRandomProfile(string? browserHint = null)
    {
        // Pick base browser profile
        JA3Profile baseProfile = browserHint?.ToLowerInvariant() switch
        {
            "chrome" => BrowserProfiles.Chrome120.Clone(),
            "firefox" => BrowserProfiles.Firefox121.Clone(),
            "safari" => BrowserProfiles.Safari17.Clone(),
            "edge" => BrowserProfiles.Edge.Clone(),
            _ => BrowserProfiles.GetRandomProfile(_random)
        };

        // Apply randomization while maintaining realistic constraints
        RandomizeCipherSuites(baseProfile);
        RandomizeExtensions(baseProfile);
        RandomizeEllipticCurves(baseProfile);

        return baseProfile;
    }

    /// <summary>
    /// Randomizes cipher suite order while maintaining browser constraints.
    /// TLS 1.3 ciphers must stay at the beginning for proper negotiation.
    /// </summary>
    private void RandomizeCipherSuites(JA3Profile profile)
    {
        if (profile.CipherSuites.Count == 0)
            return;

        // TLS 1.3 cipher IDs (4865-4867 range)
        var tls13Ciphers = new List<int>();
        var otherCiphers = new List<int>();

        foreach (int cipher in profile.CipherSuites)
        {
            if (cipher >= 4865 && cipher <= 4867)
                tls13Ciphers.Add(cipher);
            else
                otherCiphers.Add(cipher);
        }

        // Shuffle TLS 1.3 ciphers among themselves
        Shuffle(tls13Ciphers);

        // Shuffle other ciphers
        Shuffle(otherCiphers);

        // Combine: TLS 1.3 first, then others
        profile.CipherSuites = [.. tls13Ciphers, .. otherCiphers];
    }

    /// <summary>
    /// Randomizes extension order while keeping critical extensions in proper positions.
    /// Some extensions must remain first (server_name, etc.) for compatibility.
    /// </summary>
    private void RandomizeExtensions(JA3Profile profile)
    {
        if (profile.Extensions.Count == 0)
            return;

        // Extensions that should stay at the beginning
        var criticalExtensions = new HashSet<int> { 0 }; // server_name must be first
        var critical = new List<int>();
        var shuffleable = new List<int>();

        foreach (int ext in profile.Extensions)
        {
            if (criticalExtensions.Contains(ext))
                critical.Add(ext);
            else
                shuffleable.Add(ext);
        }

        // Shuffle non-critical extensions
        Shuffle(shuffleable);

        // Combine: critical first, then shuffled
        profile.Extensions = [.. critical, .. shuffleable];
    }

    /// <summary>
    /// Randomizes elliptic curve order.
    /// X25519 is preferred by modern browsers, so keep it first.
    /// </summary>
    private void RandomizeEllipticCurves(JA3Profile profile)
    {
        if (profile.EllipticCurves.Count <= 1)
            return;

        // Keep X25519 (29) first if present, shuffle the rest
        var x25519 = new List<int>();
        var others = new List<int>();

        foreach (int curve in profile.EllipticCurves)
        {
            if (curve == 29)
                x25519.Add(curve);
            else
                others.Add(curve);
        }

        Shuffle(others);

        profile.EllipticCurves = [.. x25519, .. others];
    }

    /// <summary>
    /// Generates multiple unique JA3 profiles for testing diversity.
    /// </summary>
    /// <param name="count">Number of profiles to generate.</param>
    /// <returns>Collection of unique JA3 profiles.</returns>
    public IReadOnlyList<JA3Profile> GenerateMultipleProfiles(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var profiles = new List<JA3Profile>(count);
        var seenHashes = new HashSet<string>();

        // Try to generate unique profiles
        int maxAttempts = count * 10; // Prevent infinite loop
        int attempts = 0;

        while (profiles.Count < count && attempts < maxAttempts)
        {
            JA3Profile profile = GenerateRandomProfile();
            string hash = profile.ToJA3Hash();

            if (seenHashes.Add(hash))
            {
                profiles.Add(profile);
            }

            attempts++;
        }

        return profiles;
    }

    /// <summary>
    /// Fisher-Yates shuffle algorithm for randomizing list order.
    /// </summary>
    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
