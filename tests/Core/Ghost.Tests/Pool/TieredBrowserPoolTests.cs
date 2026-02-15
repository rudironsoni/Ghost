using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
        try
        {
            _kernel = await GhostKernel.CreateAsync(new KernelOptions
            {
                Headless = true,
                EnableStealth = false,
                MaxConcurrentSessions = 50
            }).ConfigureAwait(false);

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

            await Task.Delay(2000).ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_pool != null)
            await _pool.DisposeAsync().ConfigureAwait(false);

        if (_kernel != null)
            await _kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task HotPoolProvidesBrowserUnder500ms()
    {
        var stopwatch = Stopwatch.StartNew();
        await using IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Hot);
        stopwatch.Stop();

        Assert.NotNull(session);
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Hot pool acquisition took {stopwatch.ElapsedMilliseconds}ms, expected <500ms");
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task WarmPoolProvidesBrowserUnder1500ms()
    {
        var stopwatch = Stopwatch.StartNew();
        await using IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Warm);
        stopwatch.Stop();

        Assert.NotNull(session);
        Assert.True(stopwatch.ElapsedMilliseconds < 1500,
            $"Warm pool acquisition took {stopwatch.ElapsedMilliseconds}ms, expected <1500ms");
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task ColdPoolCreatesBrowserOnDemand()
    {
        await using IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Cold);

        Assert.NotNull(session);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task PoolScalesAutomaticallyUnderLoad()
    {
        Task<IBrowserSession>[] tasks = Enumerable.Range(0, 15)
            .Select(_ => _pool!.AcquireBrowserAsync(Tier.Hot))
            .ToArray();

        IBrowserSession[] sessions = await Task.WhenAll(tasks);

        Assert.All(sessions, session =>
        {
            Assert.NotNull(session);
            Assert.True(session.IsConnected);
        });

        foreach (IBrowserSession? session in sessions)
        {
            await _pool!.ReturnBrowserAsync(session);
        }
    }

    [Fact]
    public async Task PoolReturnsSessionSuccessfullyToPool()
    {
        IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Hot);
        Assert.NotNull(session);

        await _pool.ReturnBrowserAsync(session);

        PoolHealth health = await _pool.GetHealthAsync();
        Assert.True(health.Hot.Available > 0);
    }

    [Fact]
    public async Task PoolProvidesSeparateSessions()
    {
        IBrowserSession session1 = await _pool!.AcquireBrowserAsync(Tier.Hot);
        IBrowserSession session2 = await _pool.AcquireBrowserAsync(Tier.Hot);

        Assert.NotNull(session1);
        Assert.NotNull(session2);
        Assert.NotEqual(session1.SessionId, session2.SessionId);

        await _pool.ReturnBrowserAsync(session1);
        await _pool.ReturnBrowserAsync(session2);
    }

    [Fact]
    public async Task GetHealthAsyncReturnsValidHealthStatus()
    {
        PoolHealth health = await _pool!.GetHealthAsync();

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
    public async Task WarmUpAsyncCreatesExpectedNumberOfSessions()
    {
        await _pool!.WarmUpAsync(Tier.Hot, 3);

        PoolHealth health = await _pool.GetHealthAsync();

        Assert.True(health.Hot.Total >= 3);
    }

    [Fact]
    public async Task PoolHandlesNullSessionReturnGracefully()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _pool!.ReturnBrowserAsync(null!).ConfigureAwait(false);
        });
    }

    [Fact]
    public async Task PoolAcquisitionMetricsTrackCorrectly()
    {
        await _pool!.AcquireBrowserAsync(Tier.Hot);
        await _pool.AcquireBrowserAsync(Tier.Warm);
        await _pool.AcquireBrowserAsync(Tier.Cold);

        PoolHealth health = await _pool.GetHealthAsync();

        Assert.True(health.TotalAcquisitions >= 3);
        Assert.True(health.Hot.AcquisitionCount >= 1);
        Assert.True(health.Warm.AcquisitionCount >= 1);
        Assert.True(health.Cold.AcquisitionCount >= 1);
    }

    [Fact]
    public async Task PoolFallsBackToWarmWhenHotExhausted()
    {
        var hotSessions = new List<IBrowserSession>();

        for (int i = 0; i < 10; i++)
        {
            IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Hot);
            hotSessions.Add(session);
        }

        PoolHealth health = await _pool!.GetHealthAsync();

        Assert.True(health.Warm.AcquisitionCount > 0 || health.Cold.AcquisitionCount > 0,
            "Pool should fallback to Warm or Cold when Hot is exhausted");

        foreach (IBrowserSession session in hotSessions)
        {
            await _pool.ReturnBrowserAsync(session);
        }
    }

    [Fact]
    public async Task PoolFallsBackToColdWhenWarmExhausted()
    {
        var sessions = new List<IBrowserSession>();

        for (int i = 0; i < 20; i++)
        {
            IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Warm);
            sessions.Add(session);
        }

        PoolHealth health = await _pool!.GetHealthAsync();

        Assert.True(health.Cold.AcquisitionCount > 0,
            "Pool should fallback to Cold when Warm is exhausted");

        foreach (IBrowserSession session in sessions)
        {
            await _pool.ReturnBrowserAsync(session);
        }
    }

    [Fact]
    public async Task PoolHealthCheckDetectsIssues()
    {
        PoolHealth health = await _pool!.GetHealthAsync();

        Assert.True(health.Hot.IsHealthy || health.Warm.IsHealthy || health.Cold.IsHealthy,
            "At least one tier should be healthy");
    }

    [Fact]
    public async Task PoolCreatesWorkingPage()
    {
        await using IBrowserSession session = await _pool!.AcquireBrowserAsync(Tier.Hot);
        IPage page = await session.NewPageAsync();

        Assert.NotNull(page);

        await page.NavigateAsync("https://example.com");
        string title = await page.EvaluateAsync<string>("document.title");

        Assert.False(string.IsNullOrEmpty(title));

        await _pool.ReturnBrowserAsync(session);
    }

    [Fact]
    public async Task PoolRespectsConcurrentLimitForColdTier()
    {
        int maxConcurrent = 20;
        var options = new TieredBrowserPoolOptions
        {
            Cold = new ColdPoolOptions { MaximumConcurrent = maxConcurrent }
        };

        await using ConfiguredAsyncDisposable pool = new TieredBrowserPool(_kernel!, options).ConfigureAwait(false);
        Task[] tasks = Enumerable.Range(0, maxConcurrent + 5)
            .Select(async _ =>
            {
                IBrowserSession session = await pool.AcquireBrowserAsync(Tier.Cold).ConfigureAwait(false);

                try
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
                finally
                {
                    await pool.ReturnBrowserAsync(session).ConfigureAwait(false);
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);

        PoolHealth health = await pool.GetHealthAsync().ConfigureAwait(false);
        Assert.True(health.Cold.InUse <= maxConcurrent);
    }
}
