using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ghost.Core;
using Ghost.Pool;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Tests.Pool;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Async disposal handled by IAsyncLifetime")]
public class TieredBrowserPoolTests : IAsyncLifetime
{
    private GhostKernel? _kernel;
    private TieredBrowserPool? _pool;

    public async Task InitializeAsync()
    {
        _kernel = await GhostKernel.CreateAsync(new KernelOptions
        {
            Headless = true,
            EnableStealth = false,
            MaxConcurrentSessions = 50
        });

        var options = new TieredBrowserPoolOptions
        {
            Hot = new HotPoolOptions
            {
                MinimumSize = 2,
                MaximumSize = 5,
                MaxAge = TimeSpan.FromMinutes(5)
            },
            Warm = new WarmPoolOptions
            {
                MinimumSize = 3,
                MaximumSize = 10,
                MaxAge = TimeSpan.FromMinutes(10)
            },
            Cold = new ColdPoolOptions
            {
                MaximumConcurrent = 20
            },
            SessionTtl = TimeSpan.FromMinutes(5),
            HealthCheckInterval = TimeSpan.FromSeconds(5)
        };

        _pool = new TieredBrowserPool(_kernel, options, NullLogger<TieredBrowserPool>.Instance);

        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        if (_pool != null)
            await _pool.DisposeAsync();

        if (_kernel != null)
            await _kernel.DisposeAsync();
    }

    [Fact]
    public async Task HotPool_ProvidesBrowser_Under500ms()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var session = await _pool!.AcquireBrowserAsync(Tier.Hot);
        stopwatch.Stop();

        Assert.NotNull(session);
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Hot pool acquisition took {stopwatch.ElapsedMilliseconds}ms, expected <500ms");
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task WarmPool_ProvidesBrowser_Under1500ms()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var session = await _pool!.AcquireBrowserAsync(Tier.Warm);
        stopwatch.Stop();

        Assert.NotNull(session);
        Assert.True(stopwatch.ElapsedMilliseconds < 1500,
            $"Warm pool acquisition took {stopwatch.ElapsedMilliseconds}ms, expected <1500ms");
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task ColdPool_CreatesBrowserOnDemand()
    {
        await using var session = await _pool!.AcquireBrowserAsync(Tier.Cold);

        Assert.NotNull(session);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task Pool_ScalesAutomatically_UnderLoad()
    {
        var tasks = Enumerable.Range(0, 15)
            .Select(_ => _pool!.AcquireBrowserAsync(Tier.Hot))
            .ToArray();

        var sessions = await Task.WhenAll(tasks);

        Assert.All(sessions, session =>
        {
            Assert.NotNull(session);
            Assert.True(session.IsConnected);
        });

        foreach (var session in sessions)
        {
            await _pool!.ReturnBrowserAsync(session);
        }
    }

    [Fact]
    public async Task Pool_ReturnsSession_SuccessfullyToPool()
    {
        var session = await _pool!.AcquireBrowserAsync(Tier.Hot);
        Assert.NotNull(session);

        await _pool.ReturnBrowserAsync(session);

        var health = await _pool.GetHealthAsync();
        Assert.True(health.Hot.Available > 0);
    }

    [Fact]
    public async Task Pool_ProvidesSeparateSessions()
    {
        var session1 = await _pool!.AcquireBrowserAsync(Tier.Hot);
        var session2 = await _pool.AcquireBrowserAsync(Tier.Hot);

        Assert.NotNull(session1);
        Assert.NotNull(session2);
        Assert.NotEqual(session1.SessionId, session2.SessionId);

        await _pool.ReturnBrowserAsync(session1);
        await _pool.ReturnBrowserAsync(session2);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsValidHealthStatus()
    {
        var health = await _pool!.GetHealthAsync();

        Assert.NotNull(health);
        Assert.NotNull(health.Hot);
        Assert.NotNull(health.Warm);
        Assert.NotNull(health.Cold);

        Assert.True(health.Hot.Total >= 0);
        Assert.True(health.Warm.Total >= 0);
        Assert.True(health.Cold.Total >= 0);

        Assert.True(health.MemoryPressure >= 0 && health.MemoryPressure <= 1);
    }

    [Fact]
    public async Task WarmUpAsync_CreatesExpectedNumberOfSessions()
    {
        await _pool!.WarmUpAsync(Tier.Hot, 3);

        var health = await _pool.GetHealthAsync();

        Assert.True(health.Hot.Total >= 3);
    }

    [Fact]
    public async Task Pool_HandlesNullSessionReturn_Gracefully()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _pool!.ReturnBrowserAsync(null!);
        });
    }

    [Fact]
    public async Task Pool_AcquisitionMetrics_TrackCorrectly()
    {
        await _pool!.AcquireBrowserAsync(Tier.Hot);
        await _pool.AcquireBrowserAsync(Tier.Warm);
        await _pool.AcquireBrowserAsync(Tier.Cold);

        var health = await _pool.GetHealthAsync();

        Assert.True(health.TotalAcquisitions >= 3);
        Assert.True(health.Hot.AcquisitionCount >= 1);
        Assert.True(health.Warm.AcquisitionCount >= 1);
        Assert.True(health.Cold.AcquisitionCount >= 1);
    }

    [Fact]
    public async Task Pool_FallsBackToWarm_WhenHotExhausted()
    {
        var hotSessions = new List<IBrowserSession>();

        for (int i = 0; i < 10; i++)
        {
            var session = await _pool!.AcquireBrowserAsync(Tier.Hot);
            hotSessions.Add(session);
        }

        var health = await _pool!.GetHealthAsync();

        Assert.True(health.Warm.AcquisitionCount > 0 || health.Cold.AcquisitionCount > 0,
            "Pool should fallback to Warm or Cold when Hot is exhausted");

        foreach (var session in hotSessions)
        {
            await _pool.ReturnBrowserAsync(session);
        }
    }

    [Fact]
    public async Task Pool_FallsBackToCold_WhenWarmExhausted()
    {
        var sessions = new List<IBrowserSession>();

        for (int i = 0; i < 20; i++)
        {
            var session = await _pool!.AcquireBrowserAsync(Tier.Warm);
            sessions.Add(session);
        }

        var health = await _pool!.GetHealthAsync();

        Assert.True(health.Cold.AcquisitionCount > 0,
            "Pool should fallback to Cold when Warm is exhausted");

        foreach (var session in sessions)
        {
            await _pool.ReturnBrowserAsync(session);
        }
    }

    [Fact]
    public async Task Pool_HealthCheck_DetectsIssues()
    {
        var health = await _pool!.GetHealthAsync();

        Assert.True(health.Hot.IsHealthy || health.Warm.IsHealthy || health.Cold.IsHealthy,
            "At least one tier should be healthy");
    }

    [Fact]
    public async Task Pool_CreatesWorkingPage()
    {
        await using var session = await _pool!.AcquireBrowserAsync(Tier.Hot);
        var page = await session.NewPageAsync();

        Assert.NotNull(page);

        await page.NavigateAsync("https://example.com");
        var title = await page.EvaluateAsync<string>("document.title");

        Assert.False(string.IsNullOrEmpty(title));

        await _pool.ReturnBrowserAsync(session);
    }

    [Fact]
    public async Task Pool_RespectsConcurrentLimit_ForColdTier()
    {
        var maxConcurrent = 20;
        var options = new TieredBrowserPoolOptions
        {
            Cold = new ColdPoolOptions { MaximumConcurrent = maxConcurrent }
        };

        await using var pool = new TieredBrowserPool(_kernel!, options);

        var sessions = new List<IBrowserSession>();
        var tasks = Enumerable.Range(0, maxConcurrent + 5)
            .Select(async _ =>
            {
                var session = await pool.AcquireBrowserAsync(Tier.Cold);
                lock (sessions)
                {
                    sessions.Add(session);
                }
                await Task.Delay(100);
                return session;
            })
            .ToArray();

        await Task.WhenAll(tasks);

        var health = await pool.GetHealthAsync();
        Assert.True(health.Cold.InUse <= maxConcurrent);

        foreach (var session in sessions)
        {
            await pool.ReturnBrowserAsync(session);
        }
    }
}
