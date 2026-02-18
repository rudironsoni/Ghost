using System.Security.Cryptography;
using System.Text;

namespace Ghost.Plugin.Glassdoor.Internal;

/// <summary>
/// Generates deterministic IDs for Glassdoor job listings based on job content.
/// This ensures idempotent test behavior - the same job content always produces the same ID.
/// </summary>
internal static class GlassdoorIdGenerator
{
    /// <summary>
    /// Generates a deterministic ID based on job content (title, company, location, url).
    /// Uses SHA256 hash of the combined content to produce a consistent 16-character hex ID.
    /// </summary>
    /// <param name="title">The job title.</param>
    /// <param name="company">The company name.</param>
    /// <param name="location">The job location.</param>
    /// <param name="url">The job URL.</param>
    /// <returns>A deterministic 16-character lowercase hex ID.</returns>
    public static string GenerateDeterministicId(string? title, string? company, string? location, string? url)
    {
        // Normalize inputs to handle nulls and trim whitespace
        string normalizedTitle = (title ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedCompany = (company ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedLocation = (location ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedUrl = (url ?? string.Empty).Trim().ToLowerInvariant();

        // Combine all fields with a delimiter to create a unique signature
        string combined = $"{normalizedTitle}|{normalizedCompany}|{normalizedLocation}|{normalizedUrl}";

        // Use SHA256 to generate a hash (static method for better performance)
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));

        // Take first 8 bytes (16 hex characters) for the ID
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
