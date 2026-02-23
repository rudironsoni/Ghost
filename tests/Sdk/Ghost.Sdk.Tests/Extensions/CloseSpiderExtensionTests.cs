using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Spider.Contracts;
using Moq;
using Xunit;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class CloseSpiderExtensionTests
{
    [Fact]
    public async Task ShouldCloseAsync_WithNoConditions_ReturnsFalse()
    {
        // Arrange
        var extension = new CloseSpiderExtension(Enumerable.Empty<ICloseCondition>());
        var context = new SpiderContext();

        // Act
        var result = await extension.ShouldCloseAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldCloseAsync_WhenAnyConditionMet_ReturnsTrue()
    {
        // Arrange
        var condition1 = new Mock<ICloseCondition>();
        var condition2 = new Mock<ICloseCondition>();

        condition1.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        condition2.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var extension = new CloseSpiderExtension(new[] { condition1.Object, condition2.Object });
        var context = new SpiderContext();

        // Act
        var result = await extension.ShouldCloseAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldCloseAsync_WhenAllConditionsNotMet_ReturnsFalse()
    {
        // Arrange
        var condition1 = new Mock<ICloseCondition>();
        var condition2 = new Mock<ICloseCondition>();

        condition1.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        condition2.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var extension = new CloseSpiderExtension(new[] { condition1.Object, condition2.Object });
        var context = new SpiderContext();

        // Act
        var result = await extension.ShouldCloseAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldCloseAsync_ShortCircuitsOnFirstMetCondition()
    {
        // Arrange
        var condition1 = new Mock<ICloseCondition>();
        var condition2 = new Mock<ICloseCondition>();
        var condition3 = new Mock<ICloseCondition>();

        condition1.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        condition2.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        condition3.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var extension = new CloseSpiderExtension(new[] { condition1.Object, condition2.Object, condition3.Object });
        var context = new SpiderContext();

        // Act
        var result = await extension.ShouldCloseAsync(context);

        // Assert
        result.Should().BeTrue();
        condition1.Verify(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()), Times.Once);
        condition2.Verify(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()), Times.Once);
        condition3.Verify(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullConditions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CloseSpiderExtension(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("conditions");
    }

    [Fact]
    public async Task ShouldCloseAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var extension = new CloseSpiderExtension(Enumerable.Empty<ICloseCondition>());

        // Act
        var act = async () => await extension.ShouldCloseAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void ConditionCount_ReturnsCorrectCount()
    {
        // Arrange
        var conditions = new[]
        {
            new Mock<ICloseCondition>().Object,
            new Mock<ICloseCondition>().Object,
            new Mock<ICloseCondition>().Object
        };
        var extension = new CloseSpiderExtension(conditions);

        // Act
        var count = extension.ConditionCount;

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void Conditions_ReturnsReadOnlyCollection()
    {
        // Arrange
        var condition1 = new Mock<ICloseCondition>().Object;
        var condition2 = new Mock<ICloseCondition>().Object;
        var extension = new CloseSpiderExtension(new[] { condition1, condition2 });

        // Act
        var conditions = extension.Conditions;

        // Assert
        conditions.Should().HaveCount(2);
        conditions.Should().Contain(condition1);
        conditions.Should().Contain(condition2);
        conditions.Should().BeAssignableTo<IReadOnlyList<ICloseCondition>>();
    }

    [Fact]
    public async Task ShouldCloseAsync_WithCancellationToken_PassesTokenToConditions()
    {
        // Arrange
        var condition = new Mock<ICloseCondition>();
        condition.Setup(c => c.IsMetAsync(It.IsAny<SpiderContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var extension = new CloseSpiderExtension(new[] { condition.Object });
        var context = new SpiderContext();
        var cts = new CancellationTokenSource();

        // Act
        await extension.ShouldCloseAsync(context, cts.Token);

        // Assert
        condition.Verify(c => c.IsMetAsync(It.IsAny<SpiderContext>(), cts.Token), Times.Once);
    }
}
