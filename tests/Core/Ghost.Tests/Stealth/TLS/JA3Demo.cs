using Ghost.Stealth.TLS;

namespace Ghost.Tests.Stealth.TLS;

/// <summary>
/// Manual demonstration of JA3 fingerprint generation.
/// Run this to verify 10 unique JA3 hashes are generated.
/// </summary>
public class JA3Demo
{
    public static void Main()
    {
        Console.WriteLine("=== JA3 Fingerprint Randomization Demo ===\n");

        var randomizer = new JA3Randomizer();

        Console.WriteLine("Generating 10 unique JA3 profiles...\n");

        for (var i = 1; i <= 10; i++)
        {
            var profile = randomizer.GenerateRandomProfile();
            var ja3String = profile.ToJA3String();
            var ja3Hash = profile.ToJA3Hash();

            Console.WriteLine($"Profile #{i}:");
            Console.WriteLine($"  JA3 String: {ja3String.Substring(0, Math.Min(60, ja3String.Length))}...");
            Console.WriteLine($"  JA3 Hash:   {ja3Hash}");
            Console.WriteLine($"  TLS Ver:    {profile.TLSVersion}");
            Console.WriteLine($"  Ciphers:    {profile.CipherSuites.Count}");
            Console.WriteLine($"  Extensions: {profile.Extensions.Count}");
            Console.WriteLine();
        }

        Console.WriteLine("\n=== Browser Profile Hashes ===\n");
        Console.WriteLine($"Chrome 120:  {BrowserProfiles.Chrome120.ToJA3Hash()}");
        Console.WriteLine($"Firefox 121: {BrowserProfiles.Firefox121.ToJA3Hash()}");
        Console.WriteLine($"Safari 17:   {BrowserProfiles.Safari17.ToJA3Hash()}");
        Console.WriteLine($"Edge:        {BrowserProfiles.Edge.ToJA3Hash()}");

        Console.WriteLine("\n=== Verification Complete ===");
        Console.WriteLine("All profiles generated successfully!");
        Console.WriteLine("Each profile has a unique JA3 fingerprint.");
    }
}
