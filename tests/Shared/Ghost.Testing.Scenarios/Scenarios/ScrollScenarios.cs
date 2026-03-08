using System.Text.Json;
using Ghost.Testing.Scenarios.Models;
using Ghost.Testing.Scenarios.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Logger messages for ScrollScenarios.
/// </summary>
public static partial class ScrollScenariosLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: scroll/auto-threshold")]
    public static partial void AutoThreshold(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: scroll/button-driven")]
    public static partial void ButtonDriven(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: scroll/virtualized")]
    public static partial void Virtualized(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "API: scroll/load-more offset={Offset} limit={Limit}")]
    public static partial void LoadMore(this ILogger logger, int offset, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: scroll/duplicate-chunk")]
    public static partial void DuplicateChunk(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "API: scroll/load-more-duplicates offset={Offset} limit={Limit}")]
    public static partial void LoadMoreDuplicates(this ILogger logger, int offset, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "API: Returning duplicate chunk at offset {Offset}")]
    public static partial void ReturningDuplicateChunk(this ILogger logger, int offset);
}

/// <summary>
/// Infinite scroll scenarios for testing various scroll-loading patterns.
/// </summary>
public static class ScrollScenarios
{
    public static IResult AutoThresholdHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.AutoThreshold();

        int offset = context.Items["ScrollOffset"] as int? ?? 0;
        int limit = context.Items["ScrollLimit"] as int? ?? 20;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, limit);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Auto Threshold Scroll</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .loading { text-align: center; padding: 20px; color: #666; }
    </style>
</head>
<body>
    <h1>Job Listings (Auto-Threshold Scroll)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
            <p>{j.Description}</p>
        </div>"))}}
    </div>
    <div id="loading" class="loading" style="display:none;">Loading more jobs...</div>

    <script>
        let offset = {{limit}};
        let loading = false;
        let hasMore = true;

        window.addEventListener('scroll', () => {
            if (loading || !hasMore) return;

            const scrollThreshold = document.documentElement.scrollHeight - window.innerHeight - 200;
            if (window.scrollY > scrollThreshold) {
                loadMore();
            }
        });

        async function loadMore() {
            if (loading) return;
            loading = true;
            document.getElementById('loading').style.display = 'block';

            console.log('[SCENARIO] Loading more at offset', offset);

            const response = await fetch(`/api/scroll/load-more?offset=${offset}&limit=20`);
            const data = await response.json();

            const jobList = document.getElementById('job-list');
            data.jobs.forEach(job => {
                const div = document.createElement('div');
                div.className = 'job';
                div.dataset.jobId = job.id;
                div.innerHTML = `
                    <h2>${job.title}</h2>
                    <div>${job.company} - ${job.location}</div>
                    <p>${job.description}</p>
                `;
                jobList.appendChild(div);
            });

            offset += data.jobs.length;
            hasMore = data.hasMore;
            loading = false;
            document.getElementById('loading').style.display = 'none';

            console.log('[SCENARIO] Loaded', data.jobs.length, 'jobs, hasMore:', hasMore);
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult ButtonDrivenHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.ButtonDriven();

        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 15);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Button-Driven Scroll</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        #load-more-btn {
            display: block;
            margin: 20px auto;
            padding: 12px 24px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-size: 16px;
        }
    </style>
</head>
<body>
    <h1>Job Listings (Button-Driven Load)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
        </div>"))}}
    </div>
    <button id="load-more-btn" onclick="loadMore()">Load More Jobs</button>

    <script>
        let offset = {{jobs.Count}};
        let hasMore = true;

        async function loadMore() {
            if (!hasMore) return;

            const btn = document.getElementById('load-more-btn');
            btn.disabled = true;
            btn.textContent = 'Loading...';

            console.log('[SCENARIO] Button load more at offset', offset);

            const response = await fetch(`/api/scroll/load-more?offset=${offset}&limit=15`);
            const data = await response.json();

            const jobList = document.getElementById('job-list');
            data.jobs.forEach(job => {
                const div = document.createElement('div');
                div.className = 'job';
                div.dataset.jobId = job.id;
                div.innerHTML = `<h2>${job.title}</h2><div>${job.company} - ${job.location}</div>`;
                jobList.appendChild(div);
            });

            offset += data.jobs.length;
            hasMore = data.hasMore;

            if (hasMore) {
                btn.disabled = false;
                btn.textContent = 'Load More Jobs';
            } else {
                btn.textContent = 'No More Jobs';
            }
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult VirtualizedHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.Virtualized();

        string html = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Virtualized Scroll</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        #viewport { height: 600px; overflow-y: auto; border: 1px solid #ddd; position: relative; }
        #content { position: relative; }
        .job { padding: 15px; border-bottom: 1px solid #eee; height: 80px; box-sizing: border-box; }
    </style>
</head>
<body>
    <h1>Job Listings (Virtualized Scroll)</h1>
    <div id="viewport">
        <div id="content"></div>
    </div>

    <script>
        const ITEM_HEIGHT = 80;
        const TOTAL_ITEMS = 1000;
        const BUFFER = 5;
        
        let startIndex = 0;
        let endIndex = 0;

        function render() {
            const viewport = document.getElementById('viewport');
            const content = document.getElementById('content');
            
            const scrollTop = viewport.scrollTop;
            const viewportHeight = viewport.clientHeight;
            
            startIndex = Math.max(0, Math.floor(scrollTop / ITEM_HEIGHT) - BUFFER);
            endIndex = Math.min(TOTAL_ITEMS, Math.ceil((scrollTop + viewportHeight) / ITEM_HEIGHT) + BUFFER);
            
            content.style.height = `${TOTAL_ITEMS * ITEM_HEIGHT}px`;
            content.innerHTML = '';
            
            for (let i = startIndex; i < endIndex; i++) {
                const div = document.createElement('div');
                div.className = 'job';
                div.style.position = 'absolute';
                div.style.top = `${i * ITEM_HEIGHT}px`;
                div.style.width = '100%';
                div.dataset.jobId = `job-${i}`;
                div.innerHTML = `<strong>Job ${i}</strong><br>Company ${i} - Location ${i}`;
                content.appendChild(div);
            }
            
            console.log('[SCENARIO] Virtualized render', startIndex, 'to', endIndex);
        }
        
        document.getElementById('viewport').addEventListener('scroll', render);
        render();
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static async Task<IResult> LoadMoreApiHandlerAsync(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        await Task.Delay(100).ConfigureAwait(false); // Simulate network delay

        int offset = int.TryParse(context.Request.Query["offset"], out int o) ? o : 0;
        int limit = int.TryParse(context.Request.Query["limit"], out int l) ? l : 20;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("API: scroll/load-more offset={Offset} limit={Limit}", offset, limit);
        }

        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(offset, limit);
        bool hasMore = offset + limit < TestData.TotalJobCount;

        var response = new
        {
            jobs = jobs.Select(j => new
            {
                id = j.Id,
                title = j.Title,
                company = j.Company,
                location = j.Location,
                description = j.Description
            }),
            hasMore,
            offset,
            limit,
            total = TestData.TotalJobCount
        };

        return Results.Json(response);
    }

    public static IResult DuplicateChunkReplayHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.DuplicateChunk();

        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 15);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Duplicate Chunk Replay</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .duplicate { background-color: #fff3cd; border-color: #ffc107; }
        #load-more-btn {
            display: block;
            margin: 20px auto;
            padding: 12px 24px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-size: 16px;
        }
        #stats { margin: 20px 0; padding: 10px; background: #f5f5f5; border-radius: 5px; }
    </style>
</head>
<body>
    <h1>Job Listings (Duplicate Chunk Replay)</h1>
    <div id="stats">
        <strong>Stats:</strong> Total: <span id="total-count">0</span> | Unique: <span id="unique-count">0</span> | Duplicates: <span id="duplicate-count">0</span>
    </div>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
        </div>"))}}
    </div>
    <button id="load-more-btn" onclick="loadMore()">Load More Jobs</button>

    <script>
        let offset = {{jobs.Count}};
        let hasMore = true;
        let seenIds = new Set();
        let duplicateCount = 0;

        // Initialize with initial jobs
        document.querySelectorAll('.job[data-job-id]').forEach(el => {
            const id = el.dataset.jobId;
            if (seenIds.has(id)) {
                el.classList.add('duplicate');
                duplicateCount++;
            } else {
                seenIds.add(id);
            }
        });
        updateStats();

        async function loadMore() {
            if (!hasMore) return;

            const btn = document.getElementById('load-more-btn');
            btn.disabled = true;
            btn.textContent = 'Loading...';

            console.log('[SCENARIO] Loading chunk at offset', offset);

            const response = await fetch(`/api/scroll/load-more-duplicates?offset=${offset}&limit=15`);
            const data = await response.json();

            const jobList = document.getElementById('job-list');
            data.jobs.forEach(job => {
                const div = document.createElement('div');
                div.className = 'job';
                div.dataset.jobId = job.id;

                if (seenIds.has(job.id)) {
                    div.classList.add('duplicate');
                    duplicateCount++;
                    console.log('[SCENARIO] Duplicate detected:', job.id);
                } else {
                    seenIds.add(job.id);
                }

                div.innerHTML = `<h2>${job.title}</h2><div>${job.company} - ${job.location}</div>`;
                jobList.appendChild(div);
            });

            offset += data.jobs.length;
            hasMore = data.hasMore;
            updateStats();

            if (hasMore) {
                btn.disabled = false;
                btn.textContent = 'Load More Jobs';
            } else {
                btn.textContent = 'No More Jobs';
            }

            console.log('[SCENARIO] Chunk loaded. Unique:', seenIds.size, 'Duplicates:', duplicateCount);
        }

        function updateStats() {
            const total = document.querySelectorAll('.job').length;
            document.getElementById('total-count').textContent = total;
            document.getElementById('unique-count').textContent = seenIds.size;
            document.getElementById('duplicate-count').textContent = duplicateCount;
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static async Task<IResult> LoadMoreDuplicatesApiHandlerAsync(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        await Task.Delay(100).ConfigureAwait(false); // Simulate network delay

        int offset = int.TryParse(context.Request.Query["offset"], out int o) ? o : 0;
        int limit = int.TryParse(context.Request.Query["limit"], out int l) ? l : 15;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("API: scroll/load-more-duplicates offset={Offset} limit={Limit}", offset, limit);
        }

        // Simulate duplicate chunks: return overlapping items on certain offsets
        List<SyntheticJobPosting> jobs;
        if (offset == 15 || offset == 45 || offset == 75)
        {
            // Return duplicate chunk (overlap with previous)
            jobs = TestData.GetJobPostings(offset - 5, limit);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("API: Returning duplicate chunk at offset {Offset}", offset);
            }
        }
        else
        {
            jobs = TestData.GetJobPostings(offset, limit);
        }

        bool hasMore = offset + limit < TestData.TotalJobCount;

        var response = new
        {
            jobs = jobs.Select(j => new
            {
                id = j.Id,
                title = j.Title,
                company = j.Company,
                location = j.Location,
                description = j.Description
            }),
            hasMore,
            offset,
            limit,
            total = TestData.TotalJobCount
        };

        return Results.Json(response);
    }
}
