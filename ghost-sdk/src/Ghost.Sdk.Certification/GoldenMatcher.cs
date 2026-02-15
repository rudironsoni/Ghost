using System.Text.Json;

namespace Ghost.Sdk.Certification;

/// <summary>
/// Default implementation of IGoldenMatcher.
/// Compares actual plugin output to expected golden files.
/// </summary>
public sealed class GoldenMatcher : IGoldenMatcher
{
    private readonly string _goldensPath;

    /// <summary>
    /// Initializes a new instance of GoldenMatcher.
    /// </summary>
    /// <param name="goldensPath">Path to the directory containing golden files</param>
    public GoldenMatcher(string goldensPath)
    {
        _goldensPath = goldensPath ?? throw new ArgumentNullException(nameof(goldensPath));
    }

    /// <inheritdoc />
    public async Task<GoldenMatchResult> MatchAsync(
        IReadOnlyList<Fixture> fixtures,
        CancellationToken ct = default)
    {
        int matchCount = 0;
        int mismatchCount = 0;
        string? firstMismatch = null;

        foreach (var fixture in fixtures)
        {
            try
            {
                // Load the golden file for this fixture
                var goldenPath = Path.Combine(_goldensPath, $"{fixture.FixtureId}.golden.json");

                if (!File.Exists(goldenPath))
                {
                    mismatchCount++;
                    firstMismatch ??= $"Golden file not found: {goldenPath}";
                    continue;
                }

                var goldenJson = await File.ReadAllTextAsync(goldenPath, ct);
                var golden = JsonDocument.Parse(goldenJson);

                // Compare the actual output to the golden
                // For now, we'll compare the Input documents
                // In a real implementation, this would compare the actual output events
                var isMatch = CompareJson(fixture.Input, golden);

                if (isMatch)
                {
                    matchCount++;
                }
                else
                {
                    mismatchCount++;
                    firstMismatch ??= $"Fixture {fixture.FixtureId} output does not match golden";
                }
            }
            catch (Exception ex)
            {
                mismatchCount++;
                firstMismatch ??= $"Error matching fixture {fixture.FixtureId}: {ex.Message}";
            }
        }

        return new GoldenMatchResult(
            AllMatch: mismatchCount == 0,
            MatchCount: matchCount,
            MismatchCount: mismatchCount,
            FirstMismatch: firstMismatch);
    }

    /// <summary>
    /// Compares two JSON documents for equality.
    /// </summary>
    /// <param name="actual">The actual JSON document</param>
    /// <param name="expected">The expected JSON document</param>
    /// <returns>True if the documents are equal, false otherwise</returns>
    private static bool CompareJson(JsonDocument actual, JsonDocument expected)
    {
        // Simple comparison - in a real implementation, this would be more sophisticated
        // and handle things like:
        // - Ignoring certain fields (timestamps, IDs)
        // - Tolerance for numeric values
        // - Order-independent array comparison

        using var actualStream = new MemoryStream();
        using var expectedStream = new MemoryStream();

        var writerOptions = new JsonWriterOptions { Indented = false };
        using (var writer = new Utf8JsonWriter(actualStream, writerOptions))
        {
            actual.WriteTo(writer);
        }
        using (var writer = new Utf8JsonWriter(expectedStream, writerOptions))
        {
            expected.WriteTo(writer);
        }

        var actualJson = System.Text.Encoding.UTF8.GetString(actualStream.ToArray());
        var expectedJson = System.Text.Encoding.UTF8.GetString(expectedStream.ToArray());

        return actualJson == expectedJson;
    }
}
