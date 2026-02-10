using Ghost.Testing.Scenarios.Models;
using Ghost.Testing.Scenarios.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Consent-related scenarios for testing cookie banners, modals, and CMP integrations.
/// </summary>
public static class ConsentScenarios
{
    public static IResult ModalBlockingHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: consent/modal-blocking");

        var hasConsent = context.Items["HasConsent"] as bool? ?? false;
        var jobs = TestData.GetJobPostings(0, 10);

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - Consent Modal Blocking</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .job h2 { margin: 0 0 10px 0; color: #333; }
        .job .company { color: #666; font-size: 14px; }
        .job .location { color: #888; font-size: 12px; }
        #consent-modal { 
            display: {{(hasConsent ? "none" : "block")}}; 
            position: fixed; 
            top: 0; 
            left: 0; 
            width: 100%; 
            height: 100%; 
            background: rgba(0,0,0,0.8); 
            z-index: 9999;
        }
        .modal-content {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: white;
            padding: 30px;
            border-radius: 10px;
            max-width: 500px;
            text-align: center;
        }
        .modal-content button {
            margin: 10px;
            padding: 10px 20px;
            font-size: 16px;
            cursor: pointer;
            border: none;
            border-radius: 5px;
        }
        .accept-btn { background: #4CAF50; color: white; }
        .reject-btn { background: #f44336; color: white; }
    </style>
</head>
<body>
    <div id="consent-modal">
        <div class="modal-content">
            <h2>Cookie Consent Required</h2>
            <p>We need your consent to show you job postings. This is a synthetic test scenario.</p>
            <button class="accept-btn" onclick="acceptConsent()">Accept All Cookies</button>
            <button class="reject-btn" onclick="rejectConsent()">Reject All</button>
        </div>
    </div>

    <h1>Job Listings (Modal Blocking Consent)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div class='company'>{j.Company}</div>
            <div class='location'>{j.Location}</div>
            <p>{j.Description}</p>
        </div>"))}}
    </div>

    <script>
        function acceptConsent() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=accepted; path=/';
                    document.getElementById('consent-modal').style.display = 'none';
                    console.log('[SCENARIO] Consent accepted');
                });
        }

        function rejectConsent() {
            document.cookie = 'ghost_consent=rejected; path=/';
            document.getElementById('consent-modal').style.display = 'none';
            console.log('[SCENARIO] Consent rejected');
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult BannerSoftHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: consent/banner-soft");

        var hasConsent = context.Items["HasConsent"] as bool? ?? false;
        var jobs = TestData.GetJobPostings(0, 10);

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - Consent Banner Soft</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; padding-bottom: 80px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .job h2 { margin: 0 0 10px 0; color: #333; }
        #consent-banner { 
            display: {{(hasConsent ? "none" : "block")}}; 
            position: fixed; 
            bottom: 0; 
            left: 0; 
            width: 100%; 
            background: #333; 
            color: white; 
            padding: 15px;
            z-index: 1000;
        }
        #consent-banner button {
            margin-left: 10px;
            padding: 8px 15px;
            background: #4CAF50;
            color: white;
            border: none;
            border-radius: 3px;
            cursor: pointer;
        }
    </style>
</head>
<body>
    <div id="consent-banner">
        <span>We use cookies to improve your experience. Non-blocking banner.</span>
        <button onclick="acceptConsent()">Accept</button>
    </div>

    <h1>Job Listings (Soft Consent Banner)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div class='company'>{j.Company}</div>
            <div class='location'>{j.Location}</div>
            <p>{j.Description}</p>
        </div>"))}}
    </div>

    <script>
        function acceptConsent() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=accepted; path=/';
                    document.getElementById('consent-banner').style.display = 'none';
                    console.log('[SCENARIO] Consent accepted via banner');
                });
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult IframeCmpHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Scenario: consent/iframe-cmp");

        var hasConsent = context.Items["HasConsent"] as bool? ?? false;
        var jobs = TestData.GetJobPostings(0, 10);

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - CMP Iframe Consent</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        #cmp-iframe { 
            display: {{(hasConsent ? "none" : "block")}}; 
            position: fixed; 
            bottom: 20px; 
            right: 20px; 
            width: 400px; 
            height: 300px;
            border: 2px solid #333;
            z-index: 9999;
            background: white;
        }
    </style>
</head>
<body>
    <iframe id="cmp-iframe" srcdoc="<html><body style='padding:20px;font-family:Arial;'><h3>Consent Management Platform</h3><p>Synthetic CMP in iframe</p><button onclick='parent.postMessage({type:\"consent\",action:\"accept\"}, \"*\")' style='padding:10px;background:#4CAF50;color:white;border:none;cursor:pointer;'>Accept</button></body></html>"></iframe>

    <h1>Job Listings (CMP Iframe)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div class='company'>{j.Company}</div>
        </div>"))}}
    </div>

    <script>
        window.addEventListener('message', (event) => {
            if (event.data.type === 'consent' && event.data.action === 'accept') {
                fetch('/scenario/consent/accept', { method: 'POST' })
                    .then(() => {
                        document.cookie = 'ghost_consent=accepted; path=/';
                        document.getElementById('cmp-iframe').style.display = 'none';
                        console.log('[SCENARIO] CMP consent accepted');
                    });
            }
        });
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult AcceptConsentHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.LogInformation("Consent accepted via POST");

        context.Response.Cookies.Append("ghost_consent", "accepted", new CookieOptions
        {
            Path = "/",
            MaxAge = TimeSpan.FromHours(24)
        });

        return Results.Ok(new { status = "accepted" });
    }
}
