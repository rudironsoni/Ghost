using Ghost.Stealth.Behavior;
using Xunit;

namespace Ghost.Tests.Stealth.Behavior;

public class TimingMimicryTests
{
    [Fact]
    public async Task NavigationDelayAsync_TakesExpectedTime()
    {
        // Arrange
        var timing = new TimingMimicry();
        DateTime startTime = DateTime.UtcNow;

        // Act
        await timing.NavigationDelayAsync();

        // Assert
        TimeSpan elapsed = DateTime.UtcNow - startTime;
        Assert.InRange(elapsed.TotalMilliseconds, 1900, 5200); // 2000-5000ms + buffer
    }

    [Fact]
    public async Task PreClickDelayAsync_TakesExpectedTime()
    {
        // Arrange
        var timing = new TimingMimicry();
        DateTime startTime = DateTime.UtcNow;

        // Act
        await timing.PreClickDelayAsync();

        // Assert
        TimeSpan elapsed = DateTime.UtcNow - startTime;
        Assert.InRange(elapsed.TotalMilliseconds, 400, 1700); // 500-1500ms + buffer
    }

    [Fact]
    public async Task PostClickDelayAsync_TakesExpectedTime()
    {
        // Arrange
        var timing = new TimingMimicry();
        DateTime startTime = DateTime.UtcNow;

        // Act
        await timing.PostClickDelayAsync();

        // Assert
        TimeSpan elapsed = DateTime.UtcNow - startTime;
        Assert.InRange(elapsed.TotalMilliseconds, 900, 3500); // 1000-3000ms + buffer for CI scheduler jitter
    }

    [Fact]
    public async Task CustomDelayAsync_TakesExpectedTime()
    {
        // Arrange
        var timing = new TimingMimicry();
        DateTime startTime = DateTime.UtcNow;

        // Act
        await timing.CustomDelayAsync(100, 200);

        // Assert
        TimeSpan elapsed = DateTime.UtcNow - startTime;
        Assert.InRange(elapsed.TotalMilliseconds, 90, 800); // 100-200ms + buffer for CI scheduler jitter
    }

    [Fact]
    public async Task CustomDelayAsync_ThrowsOnInvalidRange()
    {
        // Arrange
        var timing = new TimingMimicry();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await timing.CustomDelayAsync(100, 50));
    }

    [Fact]
    public async Task CustomDelayAsync_ThrowsOnNegativeMin()
    {
        // Arrange
        var timing = new TimingMimicry();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await timing.CustomDelayAsync(-1, 100));
    }
}
