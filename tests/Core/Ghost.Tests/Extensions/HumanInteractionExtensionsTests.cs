using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost;
using Ghost.Extensions;
using NSubstitute;
using Xunit;

namespace Ghost.Tests.Extensions;

public class HumanInteractionExtensionsTests
{
    [Fact]
    public async Task HumanClickAsync_CallsMethodsInOrder()
    {
        // Arrange
        var element = Substitute.For<IElement>();
        var ct = CancellationToken.None;

        // Act
        await element.HumanClickAsync(ct);

        // Assert
        Received.InOrder(async () =>
        {
            await element.ScrollIntoViewAsync(ct);
            await element.HoverAsync(ct);
            await element.ClickAsync(ct: ct);
        });
    }

    [Fact]
    public async Task HumanClickAsync_ThrowsIfElementNull()
    {
        IElement? element = null;
        await Assert.ThrowsAsync<System.ArgumentNullException>(() => element!.HumanClickAsync());
    }
}
