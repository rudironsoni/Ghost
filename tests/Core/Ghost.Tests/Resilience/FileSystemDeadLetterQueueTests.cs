using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Core.Tests.Resilience;

public class FileSystemDeadLetterQueueTests
{
    [Fact]
    public async Task EnqueueAsyncPersistsJobWithDefaults()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "LinkedIn", Query = "dev", Location = "remote", Error = "err" };

        await dlq.EnqueueAsync(job);
        var jobs = await dlq.GetFailedJobsAsync(TimeSpan.FromMinutes(5));

        jobs.Should().ContainSingle();
        jobs[0].Id.Should().NotBeNullOrWhiteSpace();
        jobs[0].FailedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetFailedJobsByPlatformAsyncFiltersByPlatform()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);

        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "LinkedIn", Query = "q1", Location = "r", Error = "e" });
        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "Indeed", Query = "q2", Location = "r", Error = "e" });

        var jobs = await dlq.GetFailedJobsByPlatformAsync("LinkedIn", TimeSpan.FromMinutes(5));

        jobs.Should().ContainSingle();
        jobs[0].Platform.Should().Be("LinkedIn");
    }

    [Fact]
    public async Task GetFailedJobsAsyncRespectsSinceWindow()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);
        var job = new FailedScrapeJob
        {
            Platform = "LinkedIn",
            Query = "dev",
            Location = "remote",
            Error = "err",
            FailedAt = DateTime.UtcNow.AddDays(-2)
        };

        await dlq.EnqueueAsync(job);
        var recentJobs = await dlq.GetFailedJobsAsync(TimeSpan.FromHours(12));
        var allJobs = await dlq.GetFailedJobsAsync(TimeSpan.FromDays(7));

        recentJobs.Should().BeEmpty();
        allJobs.Should().ContainSingle();
    }

    [Fact]
    public async Task GetJobAsyncReturnsJobWhenFound()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "Indeed", Query = "q", Location = "l", Error = "e" };

        await dlq.EnqueueAsync(job);
        var fetched = await dlq.GetJobAsync(job.Id);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task RetryAsyncIncrementsRetryCountAndTimestamp()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "Indeed", Query = "q", Location = "l", Error = "e" };

        await dlq.EnqueueAsync(job);
        await dlq.RetryAsync(job.Id);

        var fetched = await dlq.GetJobAsync(job.Id);
        fetched!.RetryCount.Should().Be(1);
        fetched.LastRetryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RetryAllAsyncIncrementsForMatchingWindow()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);

        var recent = new FailedScrapeJob { Platform = "LinkedIn", Query = "q", Location = "l", Error = "e" };
        var older = new FailedScrapeJob
        {
            Platform = "LinkedIn",
            Query = "q2",
            Location = "l",
            Error = "e",
            FailedAt = DateTime.UtcNow.AddDays(-10)
        };

        await dlq.EnqueueAsync(recent);
        await dlq.EnqueueAsync(older);

        await dlq.RetryAllAsync(TimeSpan.FromDays(2));

        var recentJob = await dlq.GetJobAsync(recent.Id);
        var olderJob = await dlq.GetJobAsync(older.Id);

        recentJob!.RetryCount.Should().Be(1);
        olderJob!.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task ArchiveAsyncMovesJobToArchive()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "LinkedIn", Query = "q", Location = "l", Error = "e" };

        await dlq.EnqueueAsync(job);
        await dlq.ArchiveAsync(job.Id);

        var fetched = await dlq.GetJobAsync(job.Id);
        fetched.Should().BeNull();

        var archiveRoot = Path.Combine(root, "archived");
        Directory.Exists(archiveRoot).Should().BeTrue();
        Directory.EnumerateFiles(archiveRoot, "*.json", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Fact]
    public async Task ArchiveAllAsyncMovesOldJobsOnly()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);

        var oldJob = new FailedScrapeJob
        {
            Platform = "Indeed",
            Query = "q",
            Location = "l",
            Error = "e",
            FailedAt = DateTime.UtcNow.AddDays(-10)
        };
        var newJob = new FailedScrapeJob
        {
            Platform = "Indeed",
            Query = "q2",
            Location = "l",
            Error = "e",
            FailedAt = DateTime.UtcNow
        };

        await dlq.EnqueueAsync(oldJob);
        await dlq.EnqueueAsync(newJob);

        await dlq.ArchiveAllAsync(TimeSpan.FromDays(3));

        var remaining = await dlq.GetFailedJobsAsync(TimeSpan.FromDays(30));
        remaining.Should().ContainSingle(job => job.Id == newJob.Id);
    }

    [Fact]
    public async Task GetQueueDepthAsyncReturnsActiveCount()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);

        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "LinkedIn", Query = "q", Location = "l", Error = "e" });
        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "Indeed", Query = "q", Location = "l", Error = "e" });

        var depth = await dlq.GetQueueDepthAsync();

        depth.Should().Be(2);
    }

    [Fact]
    public async Task EnqueueAsyncUsesDeterministicFileName()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);
        var job = new FailedScrapeJob
        {
            Id = "abc12345",
            Platform = "LinkedIn",
            Query = "q",
            Location = "l",
            Error = "e"
        };

        await dlq.EnqueueAsync(job);

        var activePath = Path.Combine(root, "active");
        Directory.EnumerateFiles(activePath, "*.json").Single()
            .Should().Contain("linkedin_abc12345.json");
    }

    [Fact]
    public async Task EnqueueAsyncThrowsWhenJobIsNull()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);

        await Assert.ThrowsAsync<ArgumentNullException>(() => dlq.EnqueueAsync(null!));
    }

    [Fact]
    public async Task GetFailedJobsAsyncThrowsOnNegativeSince()
    {
        var root = CreateTempRoot();
        var dlq = CreateQueue(root);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => dlq.GetFailedJobsAsync(TimeSpan.FromSeconds(-1)));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ghost-dlq-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static FileSystemDeadLetterQueue CreateQueue(string root)
    {
        return new FileSystemDeadLetterQueue(new DeadLetterQueueOptions
        {
            RootPath = root,
            AutoArchiveAfter = TimeSpan.Zero,
            ArchiveCheckInterval = TimeSpan.Zero
        }, NullLogger<FileSystemDeadLetterQueue>.Instance);
    }
}
