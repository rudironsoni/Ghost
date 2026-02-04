using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for StorageContext class and its factory methods.
/// </summary>
[TestFixture]
public class StorageContextTests
{
    [Test]
    public void Create_WithSpiderName_ShouldCreateContext()
    {
        // Arrange
        var spiderName = "TestSpider";

        // Act
        var context = StorageContext.Create(spiderName);

        // Assert
        context.Should().NotBeNull();
        context.SpiderName.Should().Be(spiderName);
        context.SourceUrl.Should().BeNull();
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        context.Metadata.Should().NotBeNull();
        context.Metadata.Should().BeEmpty();
        context.Tags.Should().NotBeNull();
        context.Tags.Should().BeEmpty();
        context.UniqueKeys.Should().NotBeNull();
        context.UniqueKeys.Should().BeEmpty();
    }

    [Test]
    public void Create_WithSpiderNameAndSourceUrl_ShouldCreateContext()
    {
        // Arrange
        var spiderName = "TestSpider";
        var sourceUrl = "https://example.com";

        // Act
        var context = StorageContext.Create(spiderName, sourceUrl);

        // Assert
        context.Should().NotBeNull();
        context.SpiderName.Should().Be(spiderName);
        context.SourceUrl.Should().Be(sourceUrl);
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Context_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Metadata = new Dictionary<string, object>
            {
                ["extractionType"] = "html",
                ["retryCount"] = 3,
                ["cached"] = false
            }
        };

        // Assert
        context.Metadata.Should().HaveCount(3);
        context.Metadata["extractionType"].Should().Be("html");
        context.Metadata["retryCount"].Should().Be(3);
        context.Metadata["cached"].Should().Be(false);
    }

    [Test]
    public void Context_WithTags_ShouldStoreTags()
    {
        // Arrange
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Tags = new List<string> { "ecommerce", "product", "active" }
        };

        // Assert
        context.Tags.Should().HaveCount(3);
        context.Tags.Should().Contain("ecommerce");
        context.Tags.Should().Contain("product");
        context.Tags.Should().Contain("active");
    }

    [Test]
    public void Context_WithTableName_ShouldStoreTableName()
    {
        // Arrange
        var tableName = "products";
        var context = new StorageContext
        {
            SpiderName = "ProductSpider",
            TableName = tableName
        };

        // Assert
        context.TableName.Should().Be(tableName);
    }

    [Test]
    public void Context_WithBatchId_ShouldStoreBatchId()
    {
        // Arrange
        var batchId = "batch-12345";
        var context = new StorageContext
        {
            SpiderName = "BatchSpider",
            BatchId = batchId
        };

        // Assert
        context.BatchId.Should().Be(batchId);
    }

    [Test]
    public void Context_UpdateOnConflict_ShouldDefaultToFalse()
    {
        // Arrange
        var context = new StorageContext
        {
            SpiderName = "TestSpider"
        };

        // Assert
        context.UpdateOnConflict.Should().BeFalse();
    }

    [Test]
    public void Context_WithUpdateOnConflict_ShouldStoreValue()
    {
        // Arrange
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            UpdateOnConflict = true,
            UniqueKeys = new List<string> { "id", "url" }
        };

        // Assert
        context.UpdateOnConflict.Should().BeTrue();
        context.UniqueKeys.Should().HaveCount(2);
        context.UniqueKeys.Should().Contain("id");
        context.UniqueKeys.Should().Contain("url");
    }

    [Test]
    public void Context_Timestamp_ShouldBeUtc()
    {
        // Act
        var context = StorageContext.Create("TestSpider");

        // Assert
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        context.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void Context_WithAllProperties_ShouldStoreAllValues()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var context = new StorageContext
        {
            SpiderName = "CompleteSpider",
            SourceUrl = "https://example.com/page",
            Timestamp = timestamp,
            TableName = "spider_data",
            BatchId = "batch-001",
            UpdateOnConflict = true,
            UniqueKeys = new List<string> { "url" },
            Metadata = new Dictionary<string, object> { ["version"] = "1.0" },
            Tags = new List<string> { "production" }
        };

        // Assert
        context.SpiderName.Should().Be("CompleteSpider");
        context.SourceUrl.Should().Be("https://example.com/page");
        context.Timestamp.Should().Be(timestamp);
        context.TableName.Should().Be("spider_data");
        context.BatchId.Should().Be("batch-001");
        context.UpdateOnConflict.Should().BeTrue();
        context.UniqueKeys.Should().ContainSingle().Which.Should().Be("url");
        context.Metadata.Should().ContainKey("version");
        context.Tags.Should().ContainSingle().Which.Should().Be("production");
    }
}
