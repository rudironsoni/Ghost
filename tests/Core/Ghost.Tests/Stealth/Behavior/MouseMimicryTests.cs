using Ghost.Stealth.Behavior;
using Microsoft.Playwright;
using NSubstitute;

namespace Ghost.Tests.Stealth.Behavior;

public class MouseMimicryTests
{
    [Fact]
    public async Task MoveHumanLikeAsync_MovesInMultipleSteps()
    {
        // Arrange
        var mouseMimicry = new MouseMimicry();
        var mockMouse = Substitute.For<IMouse>();
        var moveCallCount = 0;

        mockMouse.When(x => x.MoveAsync(Arg.Any<float>(), Arg.Any<float>()))
            .Do(_ => moveCallCount++);

        // Act
        await mouseMimicry.MoveHumanLikeAsync(mockMouse, 500, 500);

        // Assert
        // Should make 20-51 move calls (20-50 steps + final position)
        Assert.InRange(moveCallCount, 20, 52);
    }

    [Fact]
    public async Task MoveHumanLikeAsync_DoesNotMoveWhenAlreadyAtTarget()
    {
        // Arrange
        var mouseMimicry = new MouseMimicry();
        var mockMouse = Substitute.For<IMouse>();

        // Act
        await mouseMimicry.MoveHumanLikeAsync(mockMouse, 0, 0);

        // Assert
        await mockMouse.DidNotReceive().MoveAsync(Arg.Any<float>(), Arg.Any<float>());
    }

    [Fact]
    public async Task MoveHumanLikeAsync_RespectsPositions()
    {
        // Arrange
        var mouseMimicry = new MouseMimicry();
        var mockMouse = Substitute.For<IMouse>();
        var targetX = 500f;
        var targetY = 500f;
        var lastX = 0f;
        var lastY = 0f;

        mockMouse.When(x => x.MoveAsync(Arg.Any<float>(), Arg.Any<float>()))
            .Do(x =>
            {
                lastX = (float)x[0];
                lastY = (float)x[1];
            });

        // Act
        await mouseMimicry.MoveHumanLikeAsync(mockMouse, targetX, targetY);

        // Assert - last position should be close to target
        Assert.InRange(lastX, targetX - 1, targetX + 1);
        Assert.InRange(lastY, targetY - 1, targetY + 1);
    }
}
