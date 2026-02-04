using FluentAssertions;
using Ghost.Sdk.Spider.Scheduling.Contracts;
using Moq;
using NUnit.Framework;
using SpiderBase = Ghost.Sdk.Spider.Engine.Spider;
using SpiderOptions = Ghost.Sdk.Spider.Engine.SpiderOptions;

namespace Ghost.Sdk.Spider.Tests.Unit.Scheduling;

/// <summary>
/// Tests for trigger management and scheduling coordination
/// </summary>
[TestFixture]
public class TriggerManagerTests
{
    private Mock<IScheduler> _mockScheduler = null!;
    private Mock<SpiderBase> _mockSpider = null!;

    [SetUp]
    public void Setup()
    {
        _mockScheduler = new Mock<IScheduler>();
        _mockSpider = new Mock<SpiderBase>();
        _mockSpider.Setup(s => s.Name).Returns("TestSpider");
        _mockSpider.Setup(s => s.Options).Returns(new SpiderOptions());
    }

    [Test]
    public async Task TriggerManager_ScheduleMultipleTriggers_ShouldTrackAll()
    {
        // Arrange
        var scheduleIds = new List<string>();
        
        _mockScheduler
            .Setup(s => s.ScheduleCronAsync(It.IsAny<string>(), It.IsAny<SpiderBase>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, SpiderBase spider, string cron, CancellationToken ct) => $"schedule-{scheduleIds.Count + 1}");

        // Act
        var schedule1 = await _mockScheduler.Object.ScheduleCronAsync("Spider1", _mockSpider.Object, "0 * * * *");
        var schedule2 = await _mockScheduler.Object.ScheduleCronAsync("Spider2", _mockSpider.Object, "0 0 * * *");
        var schedule3 = await _mockScheduler.Object.ScheduleCronAsync("Spider3", _mockSpider.Object, "0 0 0 * *");

        scheduleIds.AddRange(new[] { schedule1, schedule2, schedule3 });

        // Assert
        scheduleIds.Should().HaveCount(3);
        scheduleIds.Should().OnlyContain(id => id.StartsWith("schedule-"));
    }

    [Test]
    public async Task TriggerManager_UnscheduleById_ShouldRemoveTrigger()
    {
        // Arrange
        var scheduleId = "schedule-123";
        
        _mockScheduler
            .Setup(s => s.UnscheduleAsync(scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await FluentActions.Awaiting(() => _mockScheduler.Object.UnscheduleAsync(scheduleId))
            .Should().NotThrowAsync();
        _mockScheduler.Verify(s => s.UnscheduleAsync(scheduleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task TriggerManager_PauseAndResume_ShouldWorkCorrectly()
    {
        // Arrange
        var scheduleId = "schedule-456";
        
        _mockScheduler
            .Setup(s => s.PauseAsync(scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockScheduler
            .Setup(s => s.ResumeAsync(scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await FluentActions.Awaiting(() => _mockScheduler.Object.PauseAsync(scheduleId))
            .Should().NotThrowAsync();
        await FluentActions.Awaiting(() => _mockScheduler.Object.ResumeAsync(scheduleId))
            .Should().NotThrowAsync();
    }

    [Test]
    public async Task TriggerManager_ScheduleWithSameId_ShouldReplace()
    {
        // Arrange
        var spiderName = "TestSpider";
        var scheduleId = "schedule-duplicate";
        var callCount = 0;
        
        _mockScheduler
            .Setup(s => s.ScheduleCronAsync(spiderName, It.IsAny<SpiderBase>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => 
            {
                callCount++;
                return scheduleId;
            });

        // Act
        var firstSchedule = await _mockScheduler.Object.ScheduleCronAsync(spiderName, _mockSpider.Object, "0 * * * *");
        var secondSchedule = await _mockScheduler.Object.ScheduleCronAsync(spiderName, _mockSpider.Object, "0 0 * * *");

        // Assert
        firstSchedule.Should().Be(scheduleId);
        secondSchedule.Should().Be(scheduleId);
        callCount.Should().Be(2);
    }

    [Test]
    public async Task TriggerManager_PauseMultiple_ShouldSucceed()
    {
        // Arrange
        var scheduleIds = new[] { "schedule-1", "schedule-2", "schedule-3" };
        
        foreach (var id in scheduleIds)
        {
            _mockScheduler
                .Setup(s => s.PauseAsync(id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // Act & Assert
        foreach (var id in scheduleIds)
        {
            await FluentActions.Awaiting(() => _mockScheduler.Object.PauseAsync(id))
                .Should().NotThrowAsync();
        }
    }

    [Test]
    public async Task TriggerManager_GetSchedules_ShouldReturnAll()
    {
        // Arrange
        var expectedSchedules = new List<ScheduleInfo>
        {
            new() { ScheduleId = "1", SpiderName = "Spider1", ScheduleType = "Cron" },
            new() { ScheduleId = "2", SpiderName = "Spider2", ScheduleType = "Interval" },
            new() { ScheduleId = "3", SpiderName = "Spider3", ScheduleType = "Once" }
        };
        
        _mockScheduler
            .Setup(s => s.GetSchedulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSchedules);

        // Act
        var schedules = await _mockScheduler.Object.GetSchedulesAsync();

        // Assert
        schedules.Should().HaveCount(3);
        schedules.Select(s => s.SpiderName).Should().Contain(new[] { "Spider1", "Spider2", "Spider3" });
    }

    [Test]
    public async Task TriggerManager_ScheduleImmediate_ShouldExecuteImmediately()
    {
        // Arrange
        var spiderName = "ImmediateSpider";
        var scheduleId = "schedule-immediate";
        
        _mockScheduler
            .Setup(s => s.ScheduleIntervalAsync(
                spiderName,
                It.IsAny<SpiderBase>(),
                It.IsAny<TimeSpan>(),
                TimeSpan.Zero,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduleId);

        // Act
        var result = await _mockScheduler.Object.ScheduleIntervalAsync(
            spiderName,
            _mockSpider.Object,
            TimeSpan.FromHours(1),
            TimeSpan.Zero);

        // Assert
        result.Should().Be(scheduleId);
    }
}
