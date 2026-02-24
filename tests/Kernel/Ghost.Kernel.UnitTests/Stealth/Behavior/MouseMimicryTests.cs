using Ghost.Stealth.Behavior;
using Microsoft.Playwright;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Stealth.Behavior;

public class MouseMimicryTests : ReliabilityTestBase
{
    public MouseMimicryTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task MoveHumanLikeAsync_MovesInMultipleSteps()
    {
        // Arrange
        var mouseMimicry = new MouseMimicry();
        var mockMouse = new Mock<IMouse>();
        int moveCallCount = 0;

        mockMouse.Setup(m => m.MoveAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<MouseMoveOptions>()))
            .Callback<float, float, MouseMoveOptions>((x, y, opts) => moveCallCount++)
            .Returns(Task.CompletedTask);

        // Act
        await mouseMimicry.MoveHumanLikeAsync(mockMouse.Object, 500, 500);

        // Assert
        // Should make 20-51 move calls (20-50 steps + final position)
        Assert.InRange(moveCallCount, 20, 52);
    }

    [Fact]
    public async Task MoveHumanLikeAsync_DoesNotMoveWhenAlreadyAtTarget()
    {
        // Arrange
        var mouseMimicry = new MouseMimicry();
        var mockMouse = new Mock<IMouse>();

        // Act
        await mouseMimicry.MoveHumanLikeAsync(mockMouse.Object, 0, 0);

        // Assert
        mockMouse.Verify(m => m.MoveAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<MouseMoveOptions>()), Times.Never);
    }

    [Fact]
    public async Task MoveHumanLikeAsync_RespectsPositions()
    {
        // Arrange
        var mouseMimicry = new MouseMimicry();
        var mockMouse = new Mock<IMouse>();
        float targetX = 500f;
        float targetY = 500f;
        float lastX = 0f;
        float lastY = 0f;

        mockMouse.Setup(m => m.MoveAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<MouseMoveOptions>()))
            .Callback<float, float, MouseMoveOptions>((x, y, opts) =>
            {
                lastX = x;
                lastY = y;
            })
            .Returns(Task.CompletedTask);

        // Act
        await mouseMimicry.MoveHumanLikeAsync(mockMouse.Object, targetX, targetY);

        // Assert - last position should be close to target
        Assert.InRange(lastX, targetX - 1, targetX + 1);
        Assert.InRange(lastY, targetY - 1, targetY + 1);
    }
}
