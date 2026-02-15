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
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "LinkedIn", Query = "dev", Location = "remote", Error = "err" };

        await dlq.EnqueueAsync(job).ConfigureAwait(false);
        IReadOnlyList<FailedScrapeJob> jobs = await dlq.GetFailedJobsAsync(TimeSpan.FromMinutes(5)).ConfigureAwait(false);

        jobs.Should().ContainSingle();
        jobs[0].Id.Should().NotBeNullOrWhiteSpace();
        jobs[0].FailedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetFailedJobsByPlatformAsyncFiltersByPlatform()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);

        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "LinkedIn", Query = "q1", Location = "r", Error = "e" }).ConfigureAwait(false);
        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "Indeed", Query = "q2", Location = "r", Error = "e" }).ConfigureAwait(false);

        IReadOnlyList<FailedScrapeJob> jobs = await dlq.GetFailedJobsByPlatformAsync("LinkedIn", TimeSpan.FromMinutes(5)).ConfigureAwait(false);

        jobs.Should().ContainSingle();
        jobs[0].Platform.Should().Be("LinkedIn");
    }

    [Fact]
    public async Task GetFailedJobsAsyncRespectsSinceWindow()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);
        var job = new FailedScrapeJob
        {
            Platform = "LinkedIn",
            Query = "dev",
            Location = "remote",
            Error = "err",
            FailedAt = DateTime.UtcNow.AddDays(-2)
        };

        await dlq.EnqueueAsync(job).ConfigureAwait(false);
        IReadOnlyList<FailedScrapeJob> recentJobs = await dlq.GetFailedJobsAsync(TimeSpan.FromHours(12)).ConfigureAwait(false);
        IReadOnlyList<FailedScrapeJob> allJobs = await dlq.GetFailedJobsAsync(TimeSpan.FromDays(7)).ConfigureAwait(false);

        recentJobs.Should().BeEmpty();
        allJobs.Should().ContainSingle();
    }

    [Fact]
    public async Task GetJobAsyncReturnsJobWhenFound()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "Indeed", Query = "q", Location = "l", Error = "e" };

        await dlq.EnqueueAsync(job).ConfigureAwait(false);
        FailedScrapeJob? fetched = await dlq.GetJobAsync(job.Id).ConfigureAwait(false);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task RetryAsyncIncrementsRetryCountAndTimestamp()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "Indeed", Query = "q", Location = "l", Error = "e" };

        await dlq.EnqueueAsync(job).ConfigureAwait(false);
        await dlq.RetryAsync(job.Id).ConfigureAwait(false);

        FailedScrapeJob? fetched = await dlq.GetJobAsync(job.Id).ConfigureAwait(false);
        fetched!.RetryCount.Should().Be(1);
        fetched.LastRetryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RetryAllAsyncIncrementsForMatchingWindow()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);

        var recent = new FailedScrapeJob { Platform = "LinkedIn", Query = "q", Location = "l", Error = "e" };
        var older = new FailedScrapeJob
        {
            Platform = "LinkedIn",
            Query = "q2",
            Location = "l",
            Error = "e",
            FailedAt = DateTime.UtcNow.AddDays(-10)
        };

        await dlq.EnqueueAsync(recent).ConfigureAwait(false);
        await dlq.EnqueueAsync(older).ConfigureAwait(false);

        await dlq.RetryAllAsync(TimeSpan.FromDays(2)).ConfigureAwait(false);

        FailedScrapeJob? recentJob = await dlq.GetJobAsync(recent.Id).ConfigureAwait(false);
        FailedScrapeJob? olderJob = await dlq.GetJobAsync(older.Id).ConfigureAwait(false);

        recentJob!.RetryCount.Should().Be(1);
        olderJob!.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task ArchiveAsyncMovesJobToArchive()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);
        var job = new FailedScrapeJob { Platform = "LinkedIn", Query = "q", Location = "l", Error = "e" };

        await dlq.EnqueueAsync(job).ConfigureAwait(false);
        await dlq.ArchiveAsync(job.Id).ConfigureAwait(false);

        FailedScrapeJob? fetched = await dlq.GetJobAsync(job.Id).ConfigureAwait(false);
        fetched.Should().BeNull();

        string archiveRoot = Path.Combine(root, "archived");
        Directory.Exists(archiveRoot).Should().BeTrue();
        Directory.EnumerateFiles(archiveRoot, "*.json", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Fact]
    public async Task ArchiveAllAsyncMovesOldJobsOnly()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);

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

        await dlq.EnqueueAsync(oldJob).ConfigureAwait(false);
        await dlq.EnqueueAsync(newJob).ConfigureAwait(false);

        await dlq.ArchiveAllAsync(TimeSpan.FromDays(3)).ConfigureAwait(false);

        IReadOnlyList<FailedScrapeJob> remaining = await dlq.GetFailedJobsAsync(TimeSpan.FromDays(30)).ConfigureAwait(false);
        remaining.Should().ContainSingle(job => job.Id == newJob.Id);
    }

    [Fact]
    public async Task GetQueueDepthAsyncReturnsActiveCount()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);

        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "LinkedIn", Query = "q", Location = "l", Error = "e" }).ConfigureAwait(false);
        await dlq.EnqueueAsync(new FailedScrapeJob { Platform = "Indeed", Query = "q", Location = "l", Error = "e" }).ConfigureAwait(false);

        int depth = await dlq.GetQueueDepthAsync().ConfigureAwait(false);

        depth.Should().Be(2);
    }

    [Fact]
    public async Task EnqueueAsyncUsesDeterministicFileName()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);
        var job = new FailedScrapeJob
        {
            Id = "abc12345",
            Platform = "LinkedIn",
            Query = "q",
            Location = "l",
            Error = "e"
        };

        await dlq.EnqueueAsync(job).ConfigureAwait(false);

        string activePath = Path.Combine(root, "active");
        Directory.EnumerateFiles(activePath, "*.json").Single()
            .Should().Contain("linkedin_abc12345.json");
    }

    [Fact]
    public async Task EnqueueAsyncThrowsWhenJobIsNull()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);

        await Assert.ThrowsAsync<ArgumentNullException>(() => dlq.EnqueueAsync(null!)).ConfigureAwait(false);
    }

    [Fact]
    public async Task GetFailedJobsAsyncThrowsOnNegativeSince()
    {
        string root = CreateTempRoot();
        FileSystemDeadLetterQueue dlq = CreateQueue(root);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => dlq.GetFailedJobsAsync(TimeSpan.FromSeconds(-1))).ConfigureAwait(false);
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "ghost-dlq-tests", Guid.NewGuid().ToString("N"));
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
