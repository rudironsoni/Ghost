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

        IQueryCollection query = context.Request.Query;
        string sortedQuery = string.Join("&", query.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        string html = $$"""
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

        IQueryCollection query = context.Request.Query;
        string[] trackingParams = new[] { "utm_source", "utm_medium", "utm_campaign", "fbclid", "gclid", "ref" };

        string cleanQuery = string.Join("&", query
            .Where(kv => !trackingParams.Contains(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        string html = $$"""
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

    public static IResult RedirectChainHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/redirect-chain");

        string step = context.Request.Query["step"].FirstOrDefault() ?? "0";
        string finalUrl = "/scenario/dedupe/redirect-chain?step=final";

        string? html = step switch
        {
            "0" => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Redirect Chain</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Redirect Chain Test</h1>
    <div class="info">
        <strong>Current Step:</strong> Short URL<br>
        <strong>Redirects To:</strong> Tracking URL
    </div>

    <h2>Test Links (All Lead to Same Job):</h2>
    <a href="?step=1&tracking=abc123">Short URL (bit.ly style)</a>
    <a href="?step=2&source=email&utm_source=newsletter">Email tracking link</a>
    <a href="{{finalUrl}}">Direct link</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-redirect-001<br>
        <strong>Canonical URL:</strong> {{finalUrl}}<br>
        <strong>Fingerprint:</strong> job-dedupe-redirect-001
    </div>

    <script>
        console.log('[SCENARIO] Redirect chain test, step: 0');
        console.log('[SCENARIO] Canonical fingerprint: job-dedupe-redirect-001');
    </script>
</body>
</html>
""",
            "1" => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Redirect Chain</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Redirect Chain Test</h1>
    <div class="info">
        <strong>Current Step:</strong> Tracking URL<br>
        <strong>Redirects To:</strong> Final URL
    </div>

    <a href="{{finalUrl}}">Continue to final URL</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-redirect-001<br>
        <strong>Fingerprint:</strong> job-dedupe-redirect-001
    </div>

    <script>
        console.log('[SCENARIO] Redirect chain test, step: 1');
        console.log('[SCENARIO] Canonical fingerprint: job-dedupe-redirect-001');
    </script>
</body>
</html>
""",
            "2" => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Redirect Chain</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Redirect Chain Test</h1>
    <div class="info">
        <strong>Current Step:</strong> Email Tracking URL<br>
        <strong>Redirects To:</strong> Final URL
    </div>

    <a href="{{finalUrl}}">Continue to final URL</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-redirect-001<br>
        <strong>Fingerprint:</strong> job-dedupe-redirect-001
    </div>

    <script>
        console.log('[SCENARIO] Redirect chain test, step: 2');
        console.log('[SCENARIO] Canonical fingerprint: job-dedupe-redirect-001');
    </script>
</body>
</html>
""",
            "final" => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Redirect Chain</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #e8f5e9; padding: 15px; border-radius: 5px; margin: 10px 0; border: 2px solid #4CAF50; }
    </style>
</head>
<body>
    <h1>Final Destination</h1>
    <div class="info">
        <strong>Job ID:</strong> job-dedupe-redirect-001<br>
        <strong>Title:</strong> Product Manager<br>
        <strong>Company:</strong> TechCorp<br>
        <strong>Canonical Fingerprint:</strong> job-dedupe-redirect-001<br>
        <strong>All previous URLs should resolve to this same job</strong>
    </div>

    <script>
        console.log('[SCENARIO] Redirect chain test, step: final');
        console.log('[SCENARIO] Canonical fingerprint: job-dedupe-redirect-001');
        console.log('[SCENARIO] This is the canonical URL');
    </script>
</body>
</html>
""",
            _ => Results.NotFound().ToString()
        };

        return Results.Content(html, "text/html");
    }

    public static IResult MultipleAliasesHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/multiple-aliases");

        string alias = context.Request.Query["alias"].FirstOrDefault() ?? "default";
        string canonicalId = "job-dedupe-alias-001";

        string aliasInfo = alias switch
        {
            "slug1" => "senior-software-engineer-remote",
            "slug2" => "senior-software-engineer-remote-2024",
            "slug3" => "sse-remote-us",
            "mobile" => "m/senior-software-engineer-remote",
            "regional" => "us/ca/senior-software-engineer-remote",
            _ => "default"
        };

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Multiple Aliases</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        .canonical { background: #e8f5e9; padding: 15px; border-radius: 5px; margin: 10px 0; border: 2px solid #4CAF50; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Multiple Aliases Test</h1>
    <div class="info">
        <strong>Current Alias:</strong> {{aliasInfo}}<br>
        <strong>Canonical Job ID:</strong> {{canonicalId}}
    </div>

    <h2>Test Links (All Same Job, Different Aliases):</h2>
    <a href="?alias=slug1">SEO-friendly slug 1</a>
    <a href="?alias=slug2">SEO-friendly slug 2 (updated)</a>
    <a href="?alias=slug3">Short slug</a>
    <a href="?alias=mobile">Mobile URL</a>
    <a href="?alias=regional">Regional URL</a>
    <a href="?alias=default">Default URL</a>

    <div class="canonical">
        <strong>Job ID:</strong> {{canonicalId}}<br>
        <strong>Title:</strong> Senior Software Engineer<br>
        <strong>Company:</strong> GlobalTech<br>
        <strong>Canonical Fingerprint:</strong> {{canonicalId}}<br>
        <strong>All aliases resolve to this same job</strong>
    </div>

    <script>
        console.log('[SCENARIO] Multiple aliases test, alias:', '{{aliasInfo}}');
        console.log('[SCENARIO] Canonical fingerprint:', '{{canonicalId}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult TemporalChangesHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/temporal-changes");

        string version = context.Request.Query["version"].FirstOrDefault() ?? "v1";
        string canonicalId = "job-dedupe-temporal-001";

        (string, string, string) versionInfo = version switch
        {
            "v1" => ("Original Posting", "Software Engineer", "2024-01-15"),
            "v2" => ("Updated Title", "Senior Software Engineer", "2024-02-01"),
            "v3" => ("Reposted", "Software Engineer II", "2024-03-01"),
            _ => ("Unknown", "Unknown", "Unknown")
        };

        (string? status, string? title, string? date) = versionInfo;

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Temporal Changes</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        .canonical { background: #e8f5e9; padding: 15px; border-radius: 5px; margin: 10px 0; border: 2px solid #4CAF50; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Temporal Changes Test</h1>
    <div class="info">
        <strong>Current Version:</strong> {{version}}<br>
        <strong>Status:</strong> {{status}}<br>
        <strong>Posted Date:</strong> {{date}}
    </div>

    <h2>Test Links (Same Job, Different Versions):</h2>
    <a href="?version=v1">Original posting</a>
    <a href="?version=v2">Updated title</a>
    <a href="?version=v3">Reposted as new</a>

    <div class="canonical">
        <strong>Canonical Job ID:</strong> {{canonicalId}}<br>
        <strong>Current Title:</strong> {{title}}<br>
        <strong>Company:</strong> StartupXYZ<br>
        <strong>Canonical Fingerprint:</strong> {{canonicalId}}<br>
        <strong>All versions are the same logical job</strong>
    </div>

    <script>
        console.log('[SCENARIO] Temporal changes test, version:', '{{version}}');
        console.log('[SCENARIO] Canonical fingerprint:', '{{canonicalId}}');
        console.log('[SCENARIO] Current title:', '{{title}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult MixedCaseParamsHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/mixed-case-params");

        IQueryCollection query = context.Request.Query;
        string normalizedQuery = string.Join("&", query
            .OrderBy(kv => kv.Key.ToLowerInvariant())
            .Select(kv => $"{kv.Key.ToLowerInvariant()}={kv.Value}"));

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Mixed Case Parameters</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Mixed Case Parameters Test</h1>
    <div class="info">
        <strong>Original Query:</strong> {{context.Request.QueryString}}<br>
        <strong>Normalized Query:</strong> {{normalizedQuery}}<br>
        <strong>Should Be Same Page:</strong> Yes
    </div>

    <h2>Test Links (Same Content, Different Case):</h2>
    <a href="?JobID=123&Source=LinkedIn">Mixed case</a>
    <a href="?jobid=123&source=linkedin">Lowercase</a>
    <a href="?JOBID=123&SOURCE=LINKEDIN">Uppercase</a>
    <a href="?JoBiD=123&SoUrCe=LiNkEdIn">Random case</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-mixedcase-001<br>
        <strong>Title:</strong> Data Scientist<br>
        <strong>Fingerprint:</strong> {{normalizedQuery.GetHashCode():X8}}
    </div>

    <script>
        console.log('[SCENARIO] Mixed case params test');
        console.log('[SCENARIO] Original:', '{{context.Request.QueryString}}');
        console.log('[SCENARIO] Normalized:', '{{normalizedQuery}}');
        console.log('[SCENARIO] Fingerprint:', '{{normalizedQuery.GetHashCode():X8}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult ArrayParamsHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/array-params");

        IQueryCollection query = context.Request.Query;
        string?[] skills = query["skills"].ToArray();
        string?[] sortedSkills = skills.OrderBy(s => s).ToArray();
        string normalizedSkills = string.Join(",", sortedSkills);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Array Parameters</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        a { display: block; margin: 5px 0; color: #2196F3; }
    </style>
</head>
<body>
    <h1>Array Parameters Test</h1>
    <div class="info">
        <strong>Original Skills:</strong> {{string.Join(", ", skills)}}<br>
        <strong>Normalized Skills:</strong> {{normalizedSkills}}<br>
        <strong>Should Be Same Page:</strong> Yes
    </div>

    <h2>Test Links (Same Content, Different Array Order):</h2>
    <a href="?skills=python&skills=java&skills=javascript">Python, Java, JavaScript</a>
    <a href="?skills=java&skills=python&skills=javascript">Java, Python, JavaScript</a>
    <a href="?skills=javascript&skills=python&skills=java">JavaScript, Python, Java</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-array-001<br>
        <strong>Title:</strong> Full Stack Developer<br>
        <strong>Fingerprint:</strong> {{normalizedSkills.GetHashCode():X8}}
    </div>

    <script>
        console.log('[SCENARIO] Array params test');
        console.log('[SCENARIO] Original skills:', '{{string.Join(", ", skills)}}');
        console.log('[SCENARIO] Normalized skills:', '{{normalizedSkills}}');
        console.log('[SCENARIO] Fingerprint:', '{{normalizedSkills.GetHashCode():X8}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult SessionTrackingHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/session-tracking");

        IQueryCollection query = context.Request.Query;
        string[] sessionParams = new[] { "sessionid", "sid", "user_session", "click_id", "cid", "referral_id" };

        string cleanQuery = string.Join("&", query
            .Where(kv => !sessionParams.Contains(kv.Key.ToLowerInvariant()))
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - Session Tracking</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        .removed { color: #f44336; }
        .kept { color: #4CAF50; }
        a { display: block; margin: 5px 0; }
    </style>
</head>
<body>
    <h1>Session Tracking Parameters Test</h1>
    <div class="info">
        <strong>Original Query:</strong> {{context.Request.QueryString}}<br>
        <strong>Clean Query:</strong> {{cleanQuery}}<br>
        <strong>Removed Params:</strong> <span class="removed">{{string.Join(", ", query.Keys.Where(k => sessionParams.Contains(k.ToLowerInvariant())))}}</span>
    </div>

    <h2>Test Links (Same Job, Different Sessions):</h2>
    <a href="?jobId=456&sessionid=abc123">With session ID</a>
    <a href="?jobId=456&sid=xyz789">With SID</a>
    <a href="?jobId=456&click_id=click123">With click ID</a>
    <a href="?jobId=456&referral_id=ref456">With referral ID</a>
    <a href="?jobId=456">Clean link</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-session-001<br>
        <strong>Clean Fingerprint:</strong> {{cleanQuery.GetHashCode():X8}}
    </div>

    <script>
        console.log('[SCENARIO] Session tracking test');
        console.log('[SCENARIO] Original:', '{{context.Request.QueryString}}');
        console.log('[SCENARIO] Clean:', '{{cleanQuery}}');
        console.log('[SCENARIO] Fingerprint:', '{{cleanQuery.GetHashCode():X8}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult ABTestVariantsHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: dedupe/ab-test-variants");

        IQueryCollection query = context.Request.Query;
        string[] abTestParams = new[] { "ab_test", "variant", "experiment", "test_group", "bucket" };

        string cleanQuery = string.Join("&", query
            .Where(kv => !abTestParams.Contains(kv.Key.ToLowerInvariant()))
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Dedupe - A/B Test Variants</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .info { background: #f0f0f0; padding: 15px; border-radius: 5px; margin: 10px 0; }
        .removed { color: #f44336; }
        .kept { color: #4CAF50; }
        a { display: block; margin: 5px 0; }
    </style>
</head>
<body>
    <h1>A/B Test Variants Test</h1>
    <div class="info">
        <strong>Original Query:</strong> {{context.Request.QueryString}}<br>
        <strong>Clean Query:</strong> {{cleanQuery}}<br>
        <strong>Removed Params:</strong> <span class="removed">{{string.Join(", ", query.Keys.Where(k => abTestParams.Contains(k.ToLowerInvariant())))}}</span>
    </div>

    <h2>Test Links (Same Job, Different A/B Variants):</h2>
    <a href="?jobId=789&ab_test=A&variant=control">Variant A (control)</a>
    <a href="?jobId=789&ab_test=B&variant=treatment">Variant B (treatment)</a>
    <a href="?jobId=789&experiment=exp1&test_group=group1">Experiment 1</a>
    <a href="?jobId=789&bucket=42">Bucket 42</a>
    <a href="?jobId=789">Clean link</a>

    <div class="info">
        <strong>Job ID:</strong> job-dedupe-abtest-001<br>
        <strong>Clean Fingerprint:</strong> {{cleanQuery.GetHashCode():X8}}
    </div>

    <script>
        console.log('[SCENARIO] A/B test variants test');
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
