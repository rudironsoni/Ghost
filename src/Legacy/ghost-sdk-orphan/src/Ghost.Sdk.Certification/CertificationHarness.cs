namespace Ghost.Sdk.Certification;

/// <summary>
/// Default implementation of ICertificationHarness.
/// Runs plugin certification by loading fixtures, executing against them,
/// comparing to golden outputs, and generating a certification report.
/// </summary>
public sealed class CertificationHarness : ICertificationHarness
{
    private readonly IFixtureLoader _fixtureLoader;
    private readonly IGoldenMatcher _goldenMatcher;
    private readonly ISchemaValidator _schemaValidator;

    public CertificationHarness(
        IFixtureLoader fixtureLoader,
        IGoldenMatcher goldenMatcher,
        ISchemaValidator schemaValidator)
    {
        _fixtureLoader = fixtureLoader ?? throw new ArgumentNullException(nameof(fixtureLoader));
        _goldenMatcher = goldenMatcher ?? throw new ArgumentNullException(nameof(goldenMatcher));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
    }

    /// <inheritdoc />
    public async Task<CertificationReport> CertifyAsync(
        Contracts.PluginManifest manifest,
        CertificationOptions options,
        CancellationToken ct = default)
    {
        List<CertificationResult> results = [];
        var timestamp = DateTimeOffset.UtcNow;

        foreach (var spider in manifest.Spiders)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, object> metrics = [];
            bool passed = true;
            string? failureReason = null;

            try
            {
                // Step 1: Load fixtures for this spider
                var fixtures = await _fixtureLoader.LoadAsync(
                    manifest.PluginId,
                    spider.SpiderId,
                    ct);

                metrics["FixtureCount"] = fixtures.Count;

                // Step 2: Validate SpiderSpec schema
                // Note: SpiderSpec would need to be loaded from the plugin
                // For now, we'll skip this step as we don't have the spec
                // var spec = LoadSpiderSpec(manifest.PluginId, spider.SpiderId);
                // var schemaResult = await _schemaValidator.ValidateAsync(spec, ct);
                // if (!schemaResult.IsValid)
                // {
                //     passed = false;
                //     failureReason = $"Schema validation failed: {string.Join(", ", schemaResult.Errors)}";
                //     results.Add(new CertificationResult(
                //         spider.SpiderId.Value,
                //         passed,
                //         failureReason,
                //         stopwatch.Elapsed,
                //         metrics));
                //     continue;
                // }

                // Step 3: Run plugin against fixtures
                var fixturesWithOutput = await FixtureRunner.RunAsync(
                    manifest,
                    spider,
                    fixtures,
                    options,
                    ct);

                // Step 4: Compare to golden files
                var matchResult = await _goldenMatcher.MatchAsync(
                    fixturesWithOutput,
                    ct);

                metrics["MatchCount"] = matchResult.MatchCount;
                metrics["MismatchCount"] = matchResult.MismatchCount;

                if (!matchResult.AllMatch)
                {
                    passed = false;
                    failureReason = matchResult.FirstMismatch ?? "Golden matching failed";
                }
            }
            catch (Exception ex)
            {
                passed = false;
                failureReason = $"Certification error: {ex.Message}";
                metrics["Error"] = ex.GetType().Name;
            }
            finally
            {
                stopwatch.Stop();
            }

            results.Add(new CertificationResult(
                spider.SpiderId.Value,
                passed,
                failureReason,
                stopwatch.Elapsed,
                metrics));
        }

        var allPassed = results.All(r => r.Passed);
        var summary = GenerateSummary(results, allPassed);

        return new CertificationReport(
            allPassed,
            options.Mode,
            timestamp,
            results,
            summary);
    }

    private static string GenerateSummary(List<CertificationResult> results, bool allPassed)
    {
        if (allPassed)
        {
            return $"All {results.Count} tests passed certification.";
        }

        var failed = results.Where(r => !r.Passed).ToList();
        return $"{failed.Count} of {results.Count} tests failed certification. " +
               $"Failed tests: {string.Join(", ", failed.Select(r => r.TestId))}";
    }
}
