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

        var page = context.Items["Page"] as int? ?? 1;
        var pageSize = context.Items["PageSize"] as int? ?? 10;

        var offset = (page - 1) * pageSize;
        var jobs = TestData.GetJobPostings(offset, pageSize);
        var totalPages = (int)Math.Ceiling((double)TestData.TotalJobCount / pageSize);

        var html = $$"""
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

        var cursor = context.Items["Cursor"] as string ?? "";
        var pageSize = context.Items["PageSize"] as int? ?? 10;

        var offset = string.IsNullOrEmpty(cursor) ? 0 : Convert.FromBase64String(cursor)[0] * pageSize;
        var jobs = TestData.GetJobPostings(offset, pageSize);

        var nextCursor = offset + pageSize < TestData.TotalJobCount
            ? Convert.ToBase64String(new byte[] { (byte)((offset / pageSize) + 1) })
            : null;

        var html = $$"""
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

        var page = context.Items["Page"] as int? ?? 1;
        var pageSize = 8;
        var offset = (page - 1) * pageSize;
        var jobs = TestData.GetJobPostings(offset, pageSize);

        var html = $$"""
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
}
