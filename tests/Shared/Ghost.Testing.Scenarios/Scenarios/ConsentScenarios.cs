using Ghost.Testing.Scenarios.Models;
using Ghost.Testing.Scenarios.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Logger messages for ConsentScenarios.
/// </summary>
public static partial class ConsentScenariosLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/modal-blocking")]
    public static partial void ModalBlocking(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/banner-soft")]
    public static partial void BannerSoft(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/iframe-cmp")]
    public static partial void IframeCmp(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consent accepted via POST")]
    public static partial void ConsentAcceptedPost(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/banner-dismiss")]
    public static partial void BannerDismiss(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/iframe-cmp-advanced")]
    public static partial void IframeCmpAdvanced(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/region-gdpr")]
    public static partial void RegionGdpr(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/region-ccpa")]
    public static partial void RegionCcpa(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/region-lgpd")]
    public static partial void RegionLgpd(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/stateful-persistence")]
    public static partial void StatefulPersistence(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario: consent/reconsent-policy-change")]
    public static partial void ReconsentPolicyChange(this ILogger logger);
}

/// <summary>
/// Consent-related scenarios for testing cookie banners, modals, and CMP integrations.
/// </summary>
public static class ConsentScenarios
{
    public static IResult ModalBlockingHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.ModalBlocking();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
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
        logger.BannerSoft();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
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
        logger.IframeCmp();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
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
        logger.ConsentAcceptedPost();

        context.Response.Cookies.Append("ghost_consent", "accepted", new CookieOptions
        {
            Path = "/",
            MaxAge = TimeSpan.FromHours(24)
        });

        return Results.Ok(new { status = "accepted" });
    }

    public static IResult BannerDismissHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.BannerDismiss();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - Consent Banner Dismiss</title>
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
            border: none;
            border-radius: 3px;
            cursor: pointer;
        }
        .accept-btn { background: #4CAF50; color: white; }
        .dismiss { background: #666; color: white; }
    </style>
</head>
<body>
    <div id="consent-banner">
        <span>We use cookies to improve your experience. You can dismiss without deciding.</span>
        <button class="accept-btn" onclick="acceptConsent()">Accept</button>
        <button class="dismiss" onclick="dismissBanner()">Dismiss</button>
    </div>

    <h1>Job Listings (Banner with Dismiss)</h1>
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

        function dismissBanner() {
            document.cookie = 'ghost_consent_dismissed=true; path=/';
            document.getElementById('consent-banner').style.display = 'none';
            console.log('[SCENARIO] Banner dismissed without decision');
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult IframeCmpAdvancedHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.IframeCmpAdvanced();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Jobs - Advanced CMP Iframe</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        #cmp-iframe {
            display: {{(hasConsent ? "none" : "block")}};
            position: fixed;
            bottom: 20px;
            right: 20px;
            width: 450px;
            height: 400px;
            border: 2px solid #333;
            z-index: 9999;
            background: white;
        }
    </style>
</head>
<body>
    <iframe id="cmp-iframe" srcdoc="<html><head><style>body{padding:20px;font-family:Arial;background:#f5f5f5;}.option{margin:10px 0;padding:10px;background:white;border:1px solid #ddd;border-radius:5px;}button{padding:8px 15px;margin:5px;cursor:pointer;border:none;border-radius:3px;}.accept-all{background:#4CAF50;color:white;}.reject-all{background:#f44336;color:white;}.save{background:#2196F3;color:white;}</style></head><body><h3>Advanced CMP</h3><p>Customize your consent preferences</p><div class='option'><label><input type='checkbox' checked> Essential cookies</label></div><div class='option'><label><input type='checkbox' checked> Analytics</label></div><div class='option'><label><input type='checkbox'> Marketing</label></div><div class='option'><label><input type='checkbox'> Third-party</label></div><div style='margin-top:20px;'><button class='accept-all' onclick='parent.postMessage({type:\"consent\",action:\"accept-all\"}, \"*\")'>Accept All</button><button class='reject-all' onclick='parent.postMessage({type:\"consent\",action:\"reject-all\"}, \"*\")'>Reject All</button><button class='save' onclick='parent.postMessage({type:\"consent\",action:\"save\"}, \"*\")'>Save Preferences</button></div></body></html>"></iframe>

    <h1>Job Listings (Advanced CMP Iframe)</h1>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div class='company'>{j.Company}</div>
        </div>"))}}
    </div>

    <script>
        window.addEventListener('message', (event) => {
            if (event.data.type === 'consent') {
                const action = event.data.action;
                fetch('/scenario/consent/accept', { method: 'POST' })
                    .then(() => {
                        document.cookie = 'ghost_consent=' + action + '; path=/';
                        document.getElementById('cmp-iframe').style.display = 'none';
                        console.log('[SCENARIO] CMP consent action:', action);
                    });
            }
        });
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult RegionGdprHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.RegionGdpr();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - GDPR Consent</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .job h2 { margin: 0 0 10px 0; color: #333; }
        #gdpr-modal {
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
            max-width: 600px;
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
        .accept-all { background: #4CAF50; color: white; }
        .customize { background: #2196F3; color: white; }
        .reject { background: #f44336; color: white; }
        .region-badge {
            background: #003399;
            color: white;
            padding: 5px 10px;
            border-radius: 3px;
            font-size: 12px;
            margin-bottom: 15px;
            display: inline-block;
        }
    </style>
</head>
<body>
    <div id="gdpr-modal">
        <div class="modal-content">
            <span class="region-badge">🇪🇺 EU Region - GDPR</span>
            <h2>GDPR Cookie Consent</h2>
            <p>Under the General Data Protection Regulation, we require your explicit consent before processing personal data.</p>
            <p>We use cookies for essential functionality, analytics, and personalized content.</p>
            <button class="accept-all" onclick="acceptAll()">Accept All</button>
            <button class="customize" onclick="customize()">Customize</button>
            <button class="reject" onclick="reject()">Reject Non-Essential</button>
        </div>
    </div>

    <h1>Job Listings (GDPR Region)</h1>
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
        function acceptAll() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=gdpr-accepted; path=/';
                    document.getElementById('gdpr-modal').style.display = 'none';
                    console.log('[SCENARIO] GDPR: All cookies accepted');
                });
        }

        function customize() {
            alert('Customize options would open here');
            document.cookie = 'ghost_consent=gdpr-customized; path=/';
            document.getElementById('gdpr-modal').style.display = 'none';
            console.log('[SCENARIO] GDPR: Customized consent');
        }

        function reject() {
            document.cookie = 'ghost_consent=gdpr-rejected; path=/';
            document.getElementById('gdpr-modal').style.display = 'none';
            console.log('[SCENARIO] GDPR: Non-essential rejected');
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult RegionCcpaHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.RegionCcpa();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - CCPA Consent</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; padding-bottom: 80px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .job h2 { margin: 0 0 10px 0; color: #333; }
        #ccpa-banner {
            display: {{(hasConsent ? "none" : "block")}};
            position: fixed;
            bottom: 0;
            left: 0;
            width: 100%;
            background: #1a1a1a;
            color: white;
            padding: 20px;
            z-index: 1000;
        }
        #ccpa-banner button {
            margin-left: 10px;
            padding: 10px 20px;
            border: none;
            border-radius: 3px;
            cursor: pointer;
        }
        .opt-out { background: #f44336; color: white; }
        .keep-default { background: #4CAF50; color: white; }
        .region-badge {
            background: #0066cc;
            color: white;
            padding: 3px 8px;
            border-radius: 3px;
            font-size: 11px;
            margin-right: 10px;
        }
    </style>
</head>
<body>
    <div id="ccpa-banner">
        <span><span class="region-badge">🇺🇸 California - CCPA</span>We use cookies and similar technologies. By default, we assume you consent. You can opt out of the sale of personal data.</span>
        <button class="keep-default" onclick="keepDefault()">Keep Default</button>
        <button class="opt-out" onclick="optOut()">Opt Out of Sale</button>
    </div>

    <h1>Job Listings (CCPA Region)</h1>
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
        function keepDefault() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=ccpa-default; path=/';
                    document.getElementById('ccpa-banner').style.display = 'none';
                    console.log('[SCENARIO] CCPA: Default consent kept');
                });
        }

        function optOut() {
            document.cookie = 'ghost_consent=ccpa-opted-out; path=/';
            document.getElementById('ccpa-banner').style.display = 'none';
            console.log('[SCENARIO] CCPA: Opted out of sale');
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult RegionLgpdHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.RegionLgpd();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - LGPD Consent</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .job h2 { margin: 0 0 10px 0; color: #333; }
        #lgpd-modal {
            display: {{(hasConsent ? "none" : "block")}};
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,100,0,0.85);
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
            max-width: 550px;
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
        .accept { background: #009739; color: white; }
        .reject { background: #ffdf00; color: #003c8f; }
        .region-badge {
            background: #009c3b;
            color: #ffdf00;
            padding: 5px 10px;
            border-radius: 3px;
            font-size: 12px;
            margin-bottom: 15px;
            display: inline-block;
        }
    </style>
</head>
<body>
    <div id="lgpd-modal">
        <div class="modal-content">
            <span class="region-badge">🇧🇷 Brasil - LGPD</span>
            <h2>LGPD - Lei Geral de Proteção de Dados</h2>
            <p>Under Brazil's General Personal Data Protection Law, we require your explicit consent before processing personal data.</p>
            <p>Proteção de dados pessoais é um direito fundamental.</p>
            <button class="accept" onclick="accept()">Aceitar / Accept</button>
            <button class="reject" onclick="reject()">Rejeitar / Reject</button>
        </div>
    </div>

    <h1>Job Listings (LGPD Region)</h1>
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
        function accept() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=lgpd-accepted; path=/';
                    document.getElementById('lgpd-modal').style.display = 'none';
                    console.log('[SCENARIO] LGPD: Consent accepted');
                });
        }

        function reject() {
            document.cookie = 'ghost_consent=lgpd-rejected; path=/';
            document.getElementById('lgpd-modal').style.display = 'none';
            console.log('[SCENARIO] LGPD: Consent rejected');
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult StatefulPersistenceHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.StatefulPersistence();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        int page = context.Request.Query.TryGetValue("page", out StringValues pageValue) ? int.Parse(pageValue!, System.Globalization.CultureInfo.InvariantCulture) : 1;
        List<SyntheticJobPosting> jobs = TestData.GetJobPostings((page - 1) * 10, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - Stateful Consent Persistence</title>
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
        .pagination { margin: 20px 0; }
        .pagination a {
            margin: 0 5px;
            padding: 8px 12px;
            background: #ddd;
            text-decoration: none;
            border-radius: 3px;
        }
        .pagination a.active {
            background: #4CAF50;
            color: white;
        }
    </style>
</head>
<body>
    <div id="consent-banner">
        <span>We use cookies. Your consent will be remembered across pages.</span>
        <button onclick="acceptConsent()">Accept</button>
    </div>

    <h1>Job Listings (Stateful Persistence - Page {{page}})</h1>
    <div id="session-id" style="display:none;">session-{{DateTime.UtcNow.Ticks}}</div>
    <div id="job-list">
        {{string.Join("\n", jobs.Select(j => $@"
        <div class='job' data-job-id='{j.Id}'>
            <h2>{j.Title}</h2>
            <div class='company'>{j.Company}</div>
            <div class='location'>{j.Location}</div>
            <p>{j.Description}</p>
        </div>"))}}
    </div>

    <div class="pagination">
        @if (page > 1) { <a href="?page={{page - 1}}">Previous</a> }
        <a href="?page={{page}}" class="active">{{page}}</a>
        <a href="?page={{page + 1}}">Next</a>
    </div>

    <script>
        function acceptConsent() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=accepted; path=/; max-age=86400';
                    document.getElementById('consent-banner').style.display = 'none';
                    console.log('[SCENARIO] Consent accepted - will persist');
                });
        }

        // Log consent state on page load
        window.addEventListener('load', () => {
            const consentCookie = document.cookie.split('; ').find(row => row.startsWith('ghost_consent='));
            console.log('[SCENARIO] Consent state on load:', consentCookie ? consentCookie.split('=')[1] : 'none');
        });
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }

    public static IResult ReconsentPolicyChangeHandler(HttpContext context, ILogger<ScenarioRegistry> logger)
    {
        logger.ReconsentPolicyChange();

        bool hasConsent = context.Items["HasConsent"] as bool? ?? false;
        string? consentCookie = context.Request.Cookies.TryGetValue("ghost_consent", out string? consentValue) ? consentValue : null;
        int policyCookie = context.Request.Cookies.TryGetValue("ghost_policy_version", out string? policyValue) ? int.Parse(policyValue!, System.Globalization.CultureInfo.InvariantCulture) : 0;
        int currentPolicyVersion = 2;

        // Trigger re-consent if policy version changed
        bool needsReconsent = hasConsent && policyCookie < currentPolicyVersion;

        List<SyntheticJobPosting> jobs = TestData.GetJobPostings(0, 10);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Jobs - Re-consent on Policy Change</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }
        .job { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .job h2 { margin: 0 0 10px 0; color: #333; }
        #reconsent-modal {
            display: {{(needsReconsent ? "block" : "none")}};
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(255,165,0,0.9);
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
        .accept-new { background: #FF9800; color: white; }
        .policy-info {
            background: #fff3cd;
            padding: 10px;
            border-radius: 5px;
            margin: 15px 0;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div id="reconsent-modal">
        <div class="modal-content">
            <h2>🔄 Policy Update</h2>
            <p>Our privacy policy has been updated. We need your consent again.</p>
            <div class="policy-info">
                <strong>Previous version:</strong> {{policyCookie}}<br>
                <strong>New version:</strong> {{currentPolicyVersion}}
            </div>
            <button class="accept-new" onclick="acceptNewPolicy()">Accept New Policy</button>
        </div>
    </div>

    <h1>Job Listings (Re-consent on Policy Change)</h1>
    <div id="consent-status">
        <p>Current consent: {{consentCookie ?? "none"}}</p>
        <p>Policy version: {{policyCookie}}</p>
    </div>
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
        function acceptNewPolicy() {
            fetch('/scenario/consent/accept', { method: 'POST' })
                .then(() => {
                    document.cookie = 'ghost_consent=accepted; path=/';
                    document.cookie = 'ghost_policy_version={{currentPolicyVersion}}; path=/';
                    document.getElementById('reconsent-modal').style.display = 'none';
                    document.getElementById('consent-status').innerHTML = '<p>Current consent: accepted</p><p>Policy version: {{currentPolicyVersion}}</p>';
                    console.log('[SCENARIO] New policy accepted');
                });
        }

        // Set old policy version on first load to simulate policy change
        if (!document.cookie.includes('ghost_policy_version')) {
            document.cookie = 'ghost_policy_version=1; path=/';
            console.log('[SCENARIO] Set old policy version (1) to simulate change');
            location.reload();
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
    }
}
