using Ghost.Testing.Scenarios.Models;
using Ghost.Testing.Scenarios.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Pagination scenarios for testing different pagination patterns.
/// </summary>
public static class PaginationScenarios
{
    public static IResult NumberedHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/numbered");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = context.Items["PageSize"] as int? ?? 10;

        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);
        int totalPages = (int)Math.Ceiling((double)TestData.TotalJobCount / pageSize);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Numbered Pagination</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a { 
            display: inline-block;
            padding: 8px 12px;
            margin: 0 2px;
            border: 1px solid #ddd;
            text-decoration: none;
            color: #333;
            border-radius: 3px;
        }
        .pagination a.active { background: #2196F3; color: white; border-color: #2196F3; }
        .pagination a:hover:not(.active) { background: #f5f5f5; }
    </style>
</head>
<body>
    <h1>Job Listings (Numbered Pagination)</h1>
    <p>Showing page {{page}} of {{totalPages}} ({{TestData.TotalJobCount}} total jobs)</p>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
            <p>{j.Description}</p>
        </div>"))}}
    </div>

    <div class="pagination">
        {{(page > 1 ? $"<a href='?page={page - 1}&pageSize={pageSize}'>Previous</a>" : "")}}
        
        {{string.Join("", Enumerable.Range(1, Math.Min(totalPages, 10)).Select(p =>
            p == page
                ? $"<a class='active' href='?page={p}&pageSize={pageSize}'>{p}</a>"
                : $"<a href='?page={p}&pageSize={pageSize}'>{p}</a>"))}}
        
        {{(totalPages > 10 ? "<span>...</span>" : "")}}
        {{(page < totalPages ? $"<a href='?page={page + 1}&pageSize={pageSize}'>Next</a>" : "")}}
    </div>

    <script>
        console.log('[SCENARIO] Numbered pagination page={{page}} of {{totalPages}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult CursorHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/cursor");

        string cursor = context.Items["Cursor"] as string ?? "";
        int pageSize = context.Items["PageSize"] as int? ?? 10;

        int offset = string.IsNullOrEmpty(cursor) ? 0 : Convert.FromBase64String(cursor)[0] * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        string? nextCursor = offset + pageSize < TestData.TotalJobCount
            ? Convert.ToBase64String(new byte[] { (byte)((offset / pageSize) + 1) })
            : null;

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Cursor Pagination</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 0 5px;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Cursor Pagination)</h1>
    <p>Showing {{jobs.Count}} jobs (cursor-based)</p>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
        </div>"))}}
    </div>

    <div class="pagination">
        {{(nextCursor != null ? $"<a href='?cursor={nextCursor}&pageSize={pageSize}'>Load Next Page</a>" : "<span>No more results</span>")}}
    </div>

    <script>
        console.log('[SCENARIO] Cursor pagination, cursor={{cursor ?? "start"}}, nextCursor={{nextCursor ?? "none"}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult MixedHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/mixed");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 8;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Mixed Pagination</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .controls { text-align: center; margin: 20px 0; }
        button { padding: 10px 20px; margin: 0 5px; cursor: pointer; }
    </style>
</head>
<body>
    <h1>Job Listings (Mixed: Numbered + Infinite Scroll)</h1>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    <div class="controls">
        <button onclick="loadPage({{page - 1}})">Previous Page</button>
        <span>Page {{page}}</span>
        <button onclick="loadPage({{page + 1}})">Next Page</button>
        <button onclick="toggleAutoScroll()">Toggle Auto-Scroll</button>
    </div>

    <script>
        let currentPage = {{page}};
        let autoScroll = false;

        function loadPage(page) {
            window.location.href = `?page=${page}`;
        }

        function toggleAutoScroll() {
            autoScroll = !autoScroll;
            console.log('[SCENARIO] Auto-scroll:', autoScroll);
        }

        window.addEventListener('scroll', () => {
            if (!autoScroll) return;
            
            const threshold = document.documentElement.scrollHeight - window.innerHeight - 100;
            if (window.scrollY > threshold) {
                autoScroll = false;
                loadPage(currentPage + 1);
            }
        });
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult JumpToPageHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/jump-to-page");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Jump to Page</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .controls { text-align: center; margin: 20px 0; }
        input { padding: 8px; width: 60px; }
        button { padding: 8px 16px; cursor: pointer; }
    </style>
</head>
<body>
    <h1>Job Listings (Jump to Page)</h1>
    
    <div class="controls">
        <label>Jump to page: </label>
        <input type="number" id="jumpPage" value="{{page}}" min="1" max="50">
        <button onclick="jumpToPage()">Go</button>
    </div>

    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
        </div>"))}}
    </div>

    <script>
        function jumpToPage() {
            const page = document.getElementById('jumpPage').value;
            window.location.href = `?page=${page}`;
        }
        console.log('[SCENARIO] Jump to page, current={{page}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult LastPageDetectionHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/last-page-detection");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);
        int totalPages = (int)Math.Ceiling((double)TestData.TotalJobCount / pageSize);
        bool isLastPage = page >= totalPages;

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Last Page Detection</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 8px 16px;
            margin: 0 5px;
            border: 1px solid #ddd;
            text-decoration: none;
            color: #333;
            border-radius: 3px;
        }
        .pagination a.disabled {
            background: #f5f5f5;
            color: #999;
            cursor: not-allowed;
        }
        .end-marker {
            text-align: center;
            padding: 20px;
            background: #e8f5e9;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Last Page Detection)</h1>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    {{(isLastPage ? $"<div class='end-marker' data-end-marker>You have reached the last page (page {page} of {totalPages})</div>" : "")}}

    <div class="pagination">
        {{(page > 1 ? $"<a href='?page={page - 1}'>Previous</a>" : "")}}
        <span>Page {{page}} of {{totalPages}}</span>
        {{(isLastPage ? "<a class='disabled'>Next</a>" : $"<a href='?page={page + 1}'>Next</a>")}}
    </div>

    <script>
        console.log('[SCENARIO] Last page detection, page={{page}}, isLast={{isLastPage.ToString().ToLowerInvariant()}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult TokenExpirationHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/token-expiration");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;

        // Simulate token expiration after page 3
        bool tokenExpired = page > 3;
        List<SyntheticJobPosting> jobs = tokenExpired ? new List<SyntheticJobPosting>() : TestData.GetJobPostings(offset, pageSize);

        string html = tokenExpired ? $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Token Expired</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .error {
            background: #ffebee;
            border: 1px solid #ef9a9a;
            padding: 20px;
            border-radius: 5px;
            margin: 20px 0;
        }
        button {
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 5px;
            cursor: pointer;
        }
    </style>
</head>
<body>
    <h1>Job Listings</h1>
    
    <div class="error" data-error-type="token-expired">
        <h2>Token Expired</h2>
        <p>Your pagination token has expired. Please start from the beginning.</p>
        <button onclick="window.location.href='?page=1'">Start from beginning</button>
    </div>

    <script>
        console.log('[SCENARIO] Token expired at page {{page}}');
    </script>
</body>
</html>
""" : $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Cursor Pagination</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            text-decoration: none;
            border-radius: 5px;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Cursor Pagination)</h1>
    <p>Showing {{jobs.Count}} jobs (token-based)</p>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    <div class="pagination">
        <a href='?page={{page + 1}}'>Load Next Page</a>
    </div>

    <script>
        console.log('[SCENARIO] Token pagination, page={{page}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult EmptyPageHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/empty-page");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);
        bool isEmpty = jobs.Count == 0;

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Empty Page</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            text-decoration: none;
            border-radius: 5px;
        }
        .empty-state {
            text-align: center;
            padding: 40px;
            background: #f5f5f5;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Empty Page)</h1>
    
    {{(isEmpty ? $"<div class='empty-state' data-empty-state><h2>No more results</h2><p>You have reached the end of the job listings.</p></div>" : $"<div id='job-list'>{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}</div>")}}

    <div class="pagination">
        {{(isEmpty ? "" : $"<a href='?page={page + 1}'>Load Next Page</a>")}}
    </div>

    <script>
        console.log('[SCENARIO] Empty page, page={{page}}, isEmpty={{isEmpty.ToString().ToLowerInvariant()}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult DynamicUrlHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/dynamic-url");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Dynamic URL</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .controls { text-align: center; margin: 20px 0; }
        .url-display {
            background: #f5f5f5;
            padding: 10px;
            border-radius: 5px;
            margin: 10px 0;
            font-family: monospace;
        }
        button {
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 5px;
            cursor: pointer;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Dynamic URL)</h1>
    
    <div class="controls">
        <div class="url-display" id="currentUrl">Current URL: ?page={{page}}</div>
        <button id="nextBtn" onclick="nextPage()">Next Page</button>
    </div>

    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    <script>
        function nextPage() {
            const newPage = {{page}} + 1;
            const newUrl = `?page=${newPage}`;
            window.history.pushState({page: newPage}, '', newUrl);
            document.getElementById('currentUrl').textContent = `Current URL: ${newUrl}`;
            window.location.href = newUrl;
        }
        console.log('[SCENARIO] Dynamic URL, page={{page}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult CircularHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/circular");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;

        // Simulate circular pagination: after page 5, go back to page 1
        int actualPage = page > 5 ? 1 : page;
        int offset = (actualPage - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Circular Pagination</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            text-decoration: none;
            border-radius: 5px;
        }
        .warning {
            background: #fff3e0;
            border: 1px solid #ffb74d;
            padding: 15px;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Circular Pagination)</h1>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    {{(page > 5 ? $"<div class='warning' data-warning-type='circular-pagination'><h2>⚠️ Circular Pagination Detected</h2><p>You have been redirected back to page 1. This is a test scenario for loop detection.</p></div>" : "")}}

    <div class="pagination">
        <a href='?page={{page + 1}}'>Next Page</a>
    </div>

    <script>
        console.log('[SCENARIO] Circular pagination, page={{page}}, actualPage={{actualPage}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult MissingNextLinkHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/missing-next-link");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        // Simulate missing next link after page 3
        bool hasNext = page < 3;

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Missing Next Link</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            text-decoration: none;
            border-radius: 5px;
        }
        .warning {
            background: #fff3e0;
            border: 1px solid #ffb74d;
            padding: 15px;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Missing Next Link)</h1>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    {{(!hasNext ? $"<div class='warning' data-warning-type='missing-next-link'><h2>⚠️ Next Link Missing</h2><p>The next page link is not available. This is a test scenario for dead-end detection.</p></div>" : "")}}

    <div class="pagination">
        {{(hasNext ? $"<a href='?page={page + 1}'>Next</a>" : "<span>No more pages</span>")}}
    </div>

    <script>
        console.log('[SCENARIO] Missing next link, page={{page}}, hasNext={{hasNext.ToString().ToLowerInvariant()}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult InfiniteRedirectHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/infinite-redirect");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;

        // Simulate infinite redirect loop: redirect to next page up to 10 times
        int redirectCount = int.TryParse(context.Request.Query["redirect"], out int rc) ? rc : 0;
        bool shouldRedirect = redirectCount < 10;

        if (shouldRedirect)
        {
            context.Response.Headers.Append("Location", $"?page={page}&redirect={redirectCount + 1}");
            context.Response.StatusCode = 302;
            return Results.Empty;
        }

        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Infinite Redirect</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .warning {
            background: #ffebee;
            border: 1px solid #ef9a9a;
            padding: 15px;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Infinite Redirect)</h1>
    
    <div class="warning" data-warning-type="infinite-redirect">
        <h2>⚠️ Infinite Redirect Detected</h2>
        <p>The server stopped after {{redirectCount}} redirects to prevent an infinite loop.</p>
    </div>

    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    <script>
        console.log('[SCENARIO] Infinite redirect, stopped after {{redirectCount}} redirects');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult SafeTerminationHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: pagination/safe-termination");

        int page = context.Items["Page"] as int? ?? 1;
        int pageSize = 10;
        int offset = (page - 1) * pageSize;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, pageSize);
        int totalPages = (int)Math.Ceiling((double)TestData.TotalJobCount / pageSize);
        bool isLastPage = page >= totalPages;

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Safe Termination</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .pagination { text-align: center; margin: 20px 0; }
        .pagination a {
            display: inline-block;
            padding: 8px 16px;
            margin: 0 5px;
            border: 1px solid #ddd;
            text-decoration: none;
            color: #333;
            border-radius: 3px;
        }
        .termination-marker {
            text-align: center;
            padding: 20px;
            background: #e8f5e9;
            border: 1px solid #c8e6c9;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Safe Termination)</h1>
    
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company}</div>
        </div>"))}}
    </div>

    {{(isLastPage ? $"<div class='termination-marker' data-termination-marker><h2>✓ Safe Termination</h2><p>Successfully extracted all {TestData.TotalJobCount} jobs across {totalPages} pages.</p></div>" : "")}}

    <div class="pagination">
        {{(page > 1 ? $"<a href='?page={page - 1}'>Previous</a>" : "")}}
        <span>Page {{page}} of {{totalPages}}</span>
        {{(isLastPage ? "" : $"<a href='?page={page + 1}'>Next</a>")}}
    </div>

    <script>
        console.log('[SCENARIO] Safe termination, page={{page}}, isLast={{isLastPage.ToString().ToLowerInvariant()}}');
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }
}
