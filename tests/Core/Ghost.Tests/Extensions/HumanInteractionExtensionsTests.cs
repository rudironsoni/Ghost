using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost;
using Ghost.Extensions;
using Moq;
using Xunit;

namespace Ghost.Tests.Extensions;

public class HumanInteractionExtensionsTests
{
    [Fact]
    public async Task HumanClickAsyncCallsMethodsInOrder()
    {
        // Arrange
        var mockElement = new Mock<IElement>();
        CancellationToken ct = CancellationToken.None;

        // Act
        await mockElement.Object.HumanClickAsync(ct).ConfigureAwait(false);

        // Assert
        mockElement.Verify(e => e.ScrollIntoViewAsync(ct), Times.Once);
        mockElement.Verify(e => e.HoverAsync(ct), Times.Once);
        mockElement.Verify(e => e.ClickAsync(null, ct), Times.Once);
    }

    [Fact]
    public async Task HumanClickAsyncThrowsIfElementNull()
    {
        IElement? element = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() => element!.HumanClickAsync()).ConfigureAwait(false);
    }
}
