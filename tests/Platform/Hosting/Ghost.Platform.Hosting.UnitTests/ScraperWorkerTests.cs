using System.Reflection;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Ghost.Hosting.Tests;

public class ScraperWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ResolvesScopedJobClientWithinScopeLifetime()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var holder = new ScopedDependencyHolder();

        var services = new ServiceCollection();
        services.AddScoped(_ =>
        {
            var dependency = new ScopedDependency();
            holder.Latest = dependency;
            return dependency;
        });

        services.AddKeyedScoped<IJobClient>("linkedin", (sp, _) =>
            new FakeJobClient(sp.GetRequiredService<ScopedDependency>(), cts));

        ServiceProvider serviceProvider = services.BuildServiceProvider(validateScopes: true);

        var db = new Mock<IDatabase>();
        db.SetupSequence(x => x.ListRightPopAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"{\"jobId\":\"job-1\",\"platform\":\"linkedin\",\"searchQuery\":\"dotnet\",\"maxResults\":1}")
            .ReturnsAsync(RedisValue.Null);

        db.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var config = new Ghost.Worker.WorkerConfiguration
        {
            WorkerId = "worker-test",
            NodeName = "node-test",
            RedisQueueKey = "ghost:jobs:queue",
            MaxConcurrentJobs = 1,
            PollIntervalMs = 10,
            ResultsExpirationHours = 1
        };

        var worker = new Ghost.Worker.ScraperWorker(
            NullLogger<Ghost.Worker.ScraperWorker>.Instance,
            redis.Object,
            serviceProvider,
            config);

        MethodInfo? executeAsync = typeof(Ghost.Worker.ScraperWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        executeAsync.Should().NotBeNull();
        var runTask = (Task?)executeAsync!.Invoke(worker, new object[] { cts.Token });
        runTask.Should().NotBeNull();
        await runTask!;

        holder.Latest.Should().NotBeNull();
        holder.Latest!.Disposed.Should().BeTrue();

        await serviceProvider.DisposeAsync();
    }

    private sealed class ScopedDependency : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class ScopedDependencyHolder
    {
        public ScopedDependency? Latest { get; set; }
    }

    private sealed class FakeJobClient : IJobClient
    {
        private readonly ScopedDependency _dependency;
        private readonly CancellationTokenSource _completionSignal;

        public FakeJobClient(ScopedDependency dependency, CancellationTokenSource completionSignal)
        {
            _dependency = dependency;
            _completionSignal = completionSignal;
        }

        public static string PlatformName => "linkedin";

        public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_dependency.Disposed, nameof(ScopedDependency));

            _completionSignal.Cancel();
            return Task.FromResult<IReadOnlyList<JobListing>>(Array.Empty<JobListing>());
        }

        public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new JobListing { Id = jobId });
        }

        public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails application, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public static Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<JobApplication>>(Array.Empty<JobApplication>());
        }

        public static Task SaveJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public static Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<JobListing>>(Array.Empty<JobListing>());
        }
    }
}
