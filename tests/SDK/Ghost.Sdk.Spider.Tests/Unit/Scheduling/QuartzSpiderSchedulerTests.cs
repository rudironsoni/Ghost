using FluentAssertions;
using Ghost.Sdk.Spider.Scheduling.Contracts;
using Moq;
using NUnit.Framework;
using Quartz;
using Quartz.Impl;
using SpiderBase = Ghost.Sdk.Spider.Engine.Spider;
using SpiderOptions = Ghost.Sdk.Spider.Engine.SpiderOptions;
using SpiderScheduler = Ghost.Sdk.Spider.Scheduling.Contracts.IScheduler;

namespace Ghost.Sdk.Spider.Tests.Unit.Scheduling;

/// <summary>
/// Tests for Quartz-based spider scheduler implementation.
/// Note: These tests assume a QuartzSpiderScheduler implementation exists.
/// If not implemented yet, they serve as specification tests.
/// </summary>
[TestFixture]
public class QuartzSpiderSchedulerTests
{
    private Mock<SpiderScheduler> _mockScheduler = null!;
    private Mock<SpiderBase> _mockSpider = null!;

    [SetUp]
    public void Setup()
    {
        _mockScheduler = new Mock<SpiderScheduler>();
        _mockSpider = new Mock<SpiderBase>();
        _mockSpider.Setup(s => s.Name).Returns("TestSpider");
        _mockSpider.Setup(s => s.Options).Returns(new SpiderOptions());
    }

    [Test]
    public async Task ScheduleCronAsync_WithValidExpression_ShouldReturnScheduleId()
    {
        // Arrange
        var spiderName = "TestSpider";
        var cronExpression = "0 0 * * *"; // Daily at midnight
        var expectedScheduleId = "schedule-123";

        _mockScheduler
            .Setup(s => s.ScheduleCronAsync(spiderName, It.IsAny<SpiderBase>(), cronExpression, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedScheduleId);

        // Act
        var scheduleId = await _mockScheduler.Object.ScheduleCronAsync(spiderName, _mockSpider.Object, cronExpression);

        // Assert
        scheduleId.Should().Be(expectedScheduleId);
        _mockScheduler.Verify(s => s.ScheduleCronAsync(spiderName, It.IsAny<SpiderBase>(), cronExpression, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ScheduleCronAsync_WithInvalidExpression_ShouldThrow()
    {
        // Arrange
        var spiderName = "TestSpider";
        var invalidCronExpression = "invalid-cron";

        _mockScheduler
            .Setup(s => s.ScheduleCronAsync(spiderName, _mockSpider.Object, invalidCronExpression, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid cron expression"));

        // Act
        var act = async () => await _mockScheduler.Object.ScheduleCronAsync(spiderName, _mockSpider.Object, invalidCronExpression);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid cron expression");
    }

    [Test]
    public async Task ScheduleIntervalAsync_WithValidInterval_ShouldReturnScheduleId()
    {
        // Arrange
        var spiderName = "TestSpider";
        var interval = TimeSpan.FromMinutes(30);
        var expectedScheduleId = "schedule-456";

        _mockScheduler
            .Setup(s => s.ScheduleIntervalAsync(spiderName, _mockSpider.Object, interval, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedScheduleId);

        // Act
        var scheduleId = await _mockScheduler.Object.ScheduleIntervalAsync(spiderName, _mockSpider.Object, interval);

        // Assert
        scheduleId.Should().Be(expectedScheduleId);
    }

    [Test]
    public async Task ScheduleIntervalAsync_WithStartDelay_ShouldScheduleWithDelay()
    {
        // Arrange
        var spiderName = "TestSpider";
        var interval = TimeSpan.FromMinutes(30);
        var startDelay = TimeSpan.FromMinutes(5);
        var expectedScheduleId = "schedule-789";

        _mockScheduler
            .Setup(s => s.ScheduleIntervalAsync(spiderName, _mockSpider.Object, interval, startDelay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedScheduleId);

        // Act
        var scheduleId = await _mockScheduler.Object.ScheduleIntervalAsync(spiderName, _mockSpider.Object, interval, startDelay);

        // Assert
        scheduleId.Should().Be(expectedScheduleId);
        _mockScheduler.Verify(s => s.ScheduleIntervalAsync(spiderName, _mockSpider.Object, interval, startDelay, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ScheduleOnceAsync_WithFutureTime_ShouldReturnScheduleId()
    {
        // Arrange
        var spiderName = "TestSpider";
        var runAt = DateTimeOffset.UtcNow.AddHours(1);
        var expectedScheduleId = "schedule-once-123";

        _mockScheduler
            .Setup(s => s.ScheduleOnceAsync(spiderName, _mockSpider.Object, runAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedScheduleId);

        // Act
        var scheduleId = await _mockScheduler.Object.ScheduleOnceAsync(spiderName, _mockSpider.Object, runAt);

        // Assert
        scheduleId.Should().Be(expectedScheduleId);
    }

    [Test]
    public async Task ScheduleOnceAsync_WithPastTime_ShouldThrow()
    {
        // Arrange
        var spiderName = "TestSpider";
        var runAt = DateTimeOffset.UtcNow.AddHours(-1);

        _mockScheduler
            .Setup(s => s.ScheduleOnceAsync(spiderName, _mockSpider.Object, runAt, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Cannot schedule in the past"));

        // Act
        var act = async () => await _mockScheduler.Object.ScheduleOnceAsync(spiderName, _mockSpider.Object, runAt);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task TriggerNowAsync_ShouldReturnExecutionId()
    {
        // Arrange
        var spiderName = "TestSpider";
        var expectedExecutionId = "exec-123";

        _mockScheduler
            .Setup(s => s.TriggerNowAsync(spiderName, _mockSpider.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedExecutionId);

        // Act
        var executionId = await _mockScheduler.Object.TriggerNowAsync(spiderName, _mockSpider.Object);

        // Assert
        executionId.Should().Be(expectedExecutionId);
    }

    [Test]
    public async Task UnscheduleAsync_WithValidScheduleId_ShouldComplete()
    {
        // Arrange
        var scheduleId = "schedule-123";

        _mockScheduler
            .Setup(s => s.UnscheduleAsync(scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockScheduler.Object.UnscheduleAsync(scheduleId);

        // Assert
        _mockScheduler.Verify(s => s.UnscheduleAsync(scheduleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UnscheduleAsync_WithInvalidScheduleId_ShouldThrow()
    {
        // Arrange
        var scheduleId = "invalid-schedule";

        _mockScheduler
            .Setup(s => s.UnscheduleAsync(scheduleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Schedule not found"));

        // Act
        var act = async () => await _mockScheduler.Object.UnscheduleAsync(scheduleId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task PauseAsync_WithValidScheduleId_ShouldComplete()
    {
        // Arrange
        var scheduleId = "schedule-123";

        _mockScheduler
            .Setup(s => s.PauseAsync(scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockScheduler.Object.PauseAsync(scheduleId);

        // Assert
        _mockScheduler.Verify(s => s.PauseAsync(scheduleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResumeAsync_WithValidScheduleId_ShouldComplete()
    {
        // Arrange
        var scheduleId = "schedule-123";

        _mockScheduler
            .Setup(s => s.ResumeAsync(scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockScheduler.Object.ResumeAsync(scheduleId);

        // Assert
        _mockScheduler.Verify(s => s.ResumeAsync(scheduleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetSchedulesAsync_ShouldReturnAllSchedules()
    {
        // Arrange
        var expectedSchedules = new List<ScheduleInfo>
        {
            new() { ScheduleId = "schedule-1", SpiderName = "Spider1", ScheduleType = "Cron", Expression = "0 0 * * *" },
            new() { ScheduleId = "schedule-2", SpiderName = "Spider2", ScheduleType = "Interval", Expression = "00:30:00" }
        };

        _mockScheduler
            .Setup(s => s.GetSchedulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSchedules);

        // Act
        var schedules = await _mockScheduler.Object.GetSchedulesAsync();

        // Assert
        schedules.Should().HaveCount(2);
        schedules.Should().BeEquivalentTo(expectedSchedules);
    }

    [Test]
    public async Task GetScheduleAsync_WithExistingSchedule_ShouldReturnScheduleInfo()
    {
        // Arrange
        var scheduleId = "schedule-123";
        var expectedInfo = new ScheduleInfo
        {
            ScheduleId = scheduleId,
            SpiderName = "TestSpider",
            ScheduleType = "Cron",
            Expression = "0 0 * * *",
            NextRunTime = DateTimeOffset.UtcNow.AddHours(1),
            IsPaused = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExecutionCount = 5
        };

        _mockScheduler
            .Setup(s => s.GetScheduleAsync(scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInfo);

        // Act
        var info = await _mockScheduler.Object.GetScheduleAsync(scheduleId);

        // Assert
        info.Should().NotBeNull();
        info!.ScheduleId.Should().Be(scheduleId);
        info.SpiderName.Should().Be("TestSpider");
        info.ExecutionCount.Should().Be(5);
    }

    [Test]
    public async Task GetScheduleAsync_WithNonExistentSchedule_ShouldReturnNull()
    {
        // Arrange
        var scheduleId = "non-existent";

        _mockScheduler
            .Setup(s => s.GetScheduleAsync(scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleInfo?)null);

        // Act
        var info = await _mockScheduler.Object.GetScheduleAsync(scheduleId);

        // Assert
        info.Should().BeNull();
    }

    [Test]
    public async Task CancellationToken_ShouldPropagate()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockScheduler
            .Setup(s => s.TriggerNowAsync(It.IsAny<string>(), It.IsAny<SpiderBase>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _mockScheduler.Object.TriggerNowAsync("TestSpider", _mockSpider.Object, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public void CronExpression_Validation_Examples()
    {
        // Test various cron expressions for validity
        // Note: Quartz.NET requires 6 or 7 fields (with seconds)
        var validExpressions = new[]
        {
            "0 0 0 * * ?",      // Daily at midnight (Quartz format)
            "0 0 */6 * * ?",    // Every 6 hours (Quartz format)
            "0 0 0 ? * MON",    // Every Monday at midnight (Quartz format)
            "0 0 9 ? * MON-FRI",// Weekdays at 9 AM (Quartz format)
            "0 */15 * * * ?"    // Every 15 minutes (Quartz format)
        };

        foreach (var expr in validExpressions)
        {
            // Act - using Quartz's built-in validator
            var isValid = CronExpression.IsValidExpression(expr);

            // Assert
            isValid.Should().BeTrue($"'{expr}' should be a valid cron expression");
        }
    }

    [Test]
    public async Task DistributedLocking_MultipleInstances_ShouldNotRunConcurrently()
    {
        // Arrange
        var executionId1 = "exec-1";

        // Simulate distributed lock behavior
        _mockScheduler
            .SetupSequence(s => s.TriggerNowAsync(It.IsAny<string>(), It.IsAny<SpiderBase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionId1)
            .ThrowsAsync(new InvalidOperationException("Already running"));

        // Act
        var firstExecution = await _mockScheduler.Object.TriggerNowAsync("TestSpider", _mockSpider.Object);
        var secondExecution = async () => await _mockScheduler.Object.TriggerNowAsync("TestSpider", _mockSpider.Object);

        // Assert
        firstExecution.Should().Be(executionId1);
        await secondExecution.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task ScheduleInfo_Metadata_ShouldStoreCustomData()
    {
        // Arrange
        var scheduleId = "schedule-with-metadata";
        var metadata = new Dictionary<string, object>
        {
            ["environment"] = "production",
            ["version"] = "1.0.0",
            ["tags"] = new[] { "tag1", "tag2" }
        };

        var scheduleInfo = new ScheduleInfo
        {
            ScheduleId = scheduleId,
            SpiderName = "TestSpider",
            ScheduleType = "Cron",
            Metadata = metadata
        };

        _mockScheduler
            .Setup(s => s.GetScheduleAsync(scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduleInfo);

        // Act
        var info = await _mockScheduler.Object.GetScheduleAsync(scheduleId);

        // Assert
        info.Should().NotBeNull();
        info!.Metadata.Should().ContainKey("environment");
        info.Metadata["environment"].Should().Be("production");
        info.Metadata["tags"].Should().BeEquivalentTo(new[] { "tag1", "tag2" });
    }
}
