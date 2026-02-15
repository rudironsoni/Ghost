using Ghost.Testing.Scenarios.Models;
using Ghost.Testing.Scenarios.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Anti-bot scenarios for testing JavaScript challenges and bot detection.
/// </summary>
public static class AntiBotScenarios
{
    public static IResult SimpleChallengeHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: antibot/simple-challenge");

        bool verified = context.Request.Cookies.ContainsKey("ghost_verified");

        if (verified)
        {
            List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

            string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Verified</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
    </style>
</head>
<body>
    <h1>Job Listings (Verified User)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div>{j.Company} - {j.Location}</div>
        </div>"))}}
    </div>
</body>
</html>
""";
            return Results.Content(html, "text/html");
        }

        string challengeHtml = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Verification Required</title>
    <style>
        body { 
            font-family: Arial, sans-serif; 
            display: flex; 
            justify-content: center; 
            align-items: center; 
            height: 100vh; 
            margin: 0;
            background: #f5f5f5;
        }
        .challenge {
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            text-align: center;
        }
        .spinner {
            border: 4px solid #f3f3f3;
            border-top: 4px solid #2196F3;
            border-radius: 50%;
            width: 40px;
            height: 40px;
            animation: spin 1s linear infinite;
            margin: 20px auto;
        }
        @keyframes spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
        }
    </style>
</head>
<body>
    <div class="challenge">
        <h2>Verification in Progress</h2>
        <p>Please wait while we verify your browser...</p>
        <div class="spinner"></div>
        <p id="status">Running JavaScript challenge...</p>
    </div>

    <script>
        console.log('[SCENARIO] Anti-bot challenge started');
        
        // Simulate JavaScript challenge
        setTimeout(() => {
            document.getElementById('status').textContent = 'Computing proof of work...';
            
            // Simple computational challenge
            let result = 0;
            for (let i = 0; i < 1000000; i++) {
                result += Math.sqrt(i);
            }
            
            console.log('[SCENARIO] Challenge complete, result:', result);
            
            // Submit verification
            setTimeout(() => {
                fetch('/scenario/antibot/verify', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ challenge: result.toString() })
                }).then(() => {
                    console.log('[SCENARIO] Verification submitted, reloading...');
                    window.location.reload();
                });
            }, 500);
        }, 2000);
    </script>
</body>
</html>
""";

        return Results.Content(challengeHtml, "text/html");
    }

    public static IResult VerifyChallengeHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Anti-bot challenge verified");

        context.Response.Cookies.Append("ghost_verified", "true", new CookieOptions
        {
            Path = "/",
            MaxAge = TimeSpan.FromHours(1)
        });

        return Results.Ok(new { status = "verified" });
    }
}
