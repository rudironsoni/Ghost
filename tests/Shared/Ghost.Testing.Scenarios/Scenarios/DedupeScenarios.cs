using Ghost.Testing.Scenarios.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Deduplication scenarios for testing URL normalization and tracking parameter handling.
/// </summary>
public static class DedupeScenarios
{
    public static IResult QueryReorderHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/query-reorder");

        var query = context.Request.Query;
        var sortedQuery = string.Join("&", query.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Query Reorder</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Query Parameter Reordering Test</h1>
    <div class="info">
        <strong>Original Query:</strong> {{context.Request.QueryString}}<br>
        <strong>Normalized Query:</strong> {{sortedQuery}}<br>
        <strong>Should Be Same Page:</strong> Yes
    </div>

    <h2>Test Links (Same Content, Different Query Order):</h2>
    <a href="?z=3&y=2&x=1">?z=3&y=2&x=1</a>
    <a href="?x=1&y=2&z=3">?x=1&y=2&z=3</a>
    <a href="?y=2&x=1&z=3">?y=2&x=1&z=3</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-query-001<br>
        <strong>Title:</strong> Senior Software Engineer<br>
        <strong>Fingerprint:</strong> {{sortedQuery.GetHashCode():X8}}
    </div>

    <script>
        const normalized = '{{sortedQuery}}';
        console.log('[SCENARIO] Query reorder test, normalized:', normalized);
        console.log('[SCENARIO] Fingerprint:', '{{sortedQuery.GetHashCode():X8}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult TrackingParamsHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/tracking-params");

        var query = context.Request.Query;
        var trackingParams = new[] { "utm_source", "utm_medium", "utm_campaign", "fbclid", "gclid", "ref" };

        var cleanQuery = string.Join("&", query
            .Where(kv => !trackingParams.Contains(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Tracking Parameters</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        .removed { color: #f44336; }
        .kept { color: #4CAF50; }
        a { display: block; margin: 5px 0; }
    </style>
</head>
<body>
    <h1>Tracking Parameter Removal Test</h1>
    <div class="info">
        <strong>Original Query:</strong> {{context.Request.QueryString}}<br>
        <strong>Clean Query:</strong> {{cleanQuery}}<br>
        <strong>Removed Params:</strong> <span class="removed">{{string.Join(", ", query.Keys.Intersect(trackingParams))}}</span>
    </div>

    <h2>Test Links (Same Job, Different Tracking):</h2>
    <a href="?jobId=123&utm_source=google&utm_campaign=test">With Google tracking</a>
    <a href="?jobId=123&fbclid=abc123&ref=facebook">With Facebook tracking</a>
    <a href="?jobId=123">Clean link</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-tracking-001<br>
        <strong>Clean Fingerprint:</strong> {{cleanQuery.GetHashCode():X8}}
    </div>

    <script>
        console.log('[SCENARIO] Tracking params test');
        console.log('[SCENARIO] Original:', '{{context.Request.QueryString}}');
        console.log('[SCENARIO] Clean:', '{{cleanQuery}}');
        console.log('[SCENARIO] Fingerprint:', '{{cleanQuery.GetHashCode():X8}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }
}
