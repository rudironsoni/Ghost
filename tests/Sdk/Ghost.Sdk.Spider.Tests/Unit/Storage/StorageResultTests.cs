using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for StorageResult class and its factory methods.
/// </summary>
public class StorageResultTests : ReliabilityTestBase
{
    public StorageResultTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void CreateSuccess_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var itemsStored = 5;
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var result = StorageResult.CreateSuccess(itemsStored, duration);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(itemsStored);
        result.Duration.Should().Be(duration);
        result.Error.Should().BeNull();
        result.Exception.Should().BeNull();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void CreateFailure_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Test error";
        var exception = new InvalidOperationException("Test exception");
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = StorageResult.CreateFailure(errorMessage, exception, duration);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ItemsStored.Should().Be(0);
        result.Error.Should().Be(errorMessage);
        result.Exception.Should().Be(exception);
        result.Duration.Should().Be(duration);
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void CreateFailure_WithNullException_ShouldSucceed()
    {
        // Arrange
        var errorMessage = "Test error without exception";
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        var result = StorageResult.CreateFailure(errorMessage, null, duration);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Be(errorMessage);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void StorageResult_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["key3"] = true
        };

        // Act
        var result = new StorageResult
        {
            Success = true,
            ItemsStored = 10,
            Duration = TimeSpan.FromSeconds(1),
            Metadata = metadata
        };

        // Assert
        result.Metadata.Should().HaveCount(3);
        result.Metadata["key1"].Should().Be("value1");
        result.Metadata["key2"].Should().Be(42);
        result.Metadata["key3"].Should().Be(true);
    }

    [Fact]
    public void StorageResult_WithZeroDuration_ShouldBeValid()
    {
        // Act
        var result = StorageResult.CreateSuccess(1, TimeSpan.Zero);

        // Assert
        result.Success.Should().BeTrue();
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void StorageResult_WithLargeDuration_ShouldBeValid()
    {
        // Arrange
        var duration = TimeSpan.FromHours(1);

        // Act
        var result = StorageResult.CreateSuccess(1000, duration);

        // Assert
        result.Duration.Should().Be(duration);
        result.ItemsStored.Should().Be(1000);
    }
}
