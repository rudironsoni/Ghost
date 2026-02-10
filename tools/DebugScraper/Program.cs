using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Abstractions;

// Minimal console to exercise job platform clients and dump raw responses
var configuration = new ConfigurationBuilder().Build();

var services = new ServiceCollection();
services.AddHttpClient();

// Helper: ensure logs dir
System.IO.Directory.CreateDirectory("logs");

using var provider = services.BuildServiceProvider();
var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

// proxy provider instance
IProxyProvider proxyProvider = new LocalProxyProvider();

async Task DumpGoogle()
{
    try
    {
        Console.WriteLine("[Google] Testing Google jobs client...");
        var http = httpFactory.CreateClient();
        var options = new Ghost.Platform.Google.Jobs.GoogleJobsOptions();
        var api = new Ghost.Platform.Google.Jobs.Internal.GoogleJobsApiClient(http, options, NullLogger<Ghost.Platform.Google.Jobs.Internal.GoogleJobsApiClient>.Instance);
        var results = await api.SearchAsync("Software Engineer", "Madrid");
        Console.WriteLine($"[Google] Results: {results.Count}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Google] Error: {ex}");
    }
}

async Task DumpGlassdoor()
{
    try
    {
        Console.WriteLine("[Glassdoor] Testing Glassdoor client...");
        var http = httpFactory.CreateClient();
        var api = new Ghost.Platform.Glassdoor.Internal.GlassdoorApiClient(http);
        var json = await api.SearchAsync("Software Engineer", "Madrid");
        if (json != null)
        {
            await System.IO.File.WriteAllTextAsync("logs/glassdoor_response.json", json);
            Console.WriteLine($"[Glassdoor] Response saved ({json.Length} bytes)");
        }
        else
        {
            Console.WriteLine("[Glassdoor] No response");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Glassdoor] Error: {ex}");
    }
}

async Task DumpIndeed()
{
    try
    {
        Console.WriteLine("[Indeed] Testing Indeed client...");
        var http = httpFactory.CreateClient();
        var options = new Ghost.Platform.Indeed.IndeedOptions();
        var api = new Ghost.Platform.Indeed.Internal.IndeedApiClient(proxyProvider, options, NullLogger<Ghost.Platform.Indeed.Internal.IndeedApiClient>.Instance);

        using var writer = new System.IO.StreamWriter("logs/indeed_response.json");
        await foreach (var root in api.SearchAsync("Software Engineer", "Madrid", 50))
        {
            var json = JsonSerializer.Serialize(root);
            await writer.WriteLineAsync(json);
        }
        Console.WriteLine("[Indeed] Responses saved");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Indeed] Error: {ex}");
    }
}

async Task DumpInfoJobs()
{
    try
    {
        Console.WriteLine("[InfoJobs] Testing InfoJobs client...");
        var http = httpFactory.CreateClient();
        var options = new Ghost.Platform.InfoJobs.Jobs.InfoJobsOptions();
        var api = new Ghost.Platform.InfoJobs.Jobs.Internal.InfoJobsApiClient(http, options, NullLogger<Ghost.Platform.InfoJobs.Jobs.Internal.InfoJobsApiClient>.Instance);
        var jobs = await api.SearchAsync("Software Engineer", "Madrid");
        var raw = JsonSerializer.Serialize(jobs);
        await System.IO.File.WriteAllTextAsync("logs/infojobs_response.json", raw);
        Console.WriteLine($"[InfoJobs] Response saved ({jobs.Count} jobs)");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[InfoJobs] Error: {ex}");
    }
}

async Task DumpTecnoempleo()
{
    try
    {
        Console.WriteLine("[Tecnoempleo] Testing Tecnoempleo client...");
        var http = httpFactory.CreateClient();
        var options = new Ghost.Platform.Tecnoempleo.Jobs.TecnoempleoOptions();
        var api = new Ghost.Platform.Tecnoempleo.Jobs.Internal.TecnoempleoApiClient(http, options, NullLogger<Ghost.Platform.Tecnoempleo.Jobs.Internal.TecnoempleoApiClient>.Instance);
        var jobs = await api.SearchJobsAsync("Software Engineer", "Madrid");
        var raw = JsonSerializer.Serialize(jobs);
        await System.IO.File.WriteAllTextAsync("logs/tecnoempleo_response.json", raw);
        Console.WriteLine($"[Tecnoempleo] Response saved ({jobs.Count} jobs)");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Tecnoempleo] Error: {ex}");
    }
}

// Run sequentially
await DumpGoogle();
await DumpGlassdoor();
await DumpIndeed();
await DumpInfoJobs();
await DumpTecnoempleo();

Console.WriteLine("DebugScraper finished");

// Local proxy provider implementation used by DebugScraper
internal sealed class LocalProxyProvider : IProxyProvider
{
    public Task<ProxyInfo?> GetProxyAsync(string countryCode, System.Threading.CancellationToken token = default)
        => Task.FromResult<ProxyInfo?>(null);
}
