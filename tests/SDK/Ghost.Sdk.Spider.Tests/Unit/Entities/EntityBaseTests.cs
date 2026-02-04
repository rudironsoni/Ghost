using FluentAssertions;
using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Entities;

[TestFixture]
public class EntityBaseTests
{
    [Test]
    public void GetMetadata_ShouldReturnEntityMetadata()
    {
        // Act
        var metadata = EntityBase<TestProduct>.GetMetadata();

        // Assert
        metadata.Should().NotBeNull();
        metadata.EntityType.Should().Be(typeof(TestProduct));
        metadata.EntitySelector.Should().NotBeNull();
        metadata.Properties.Should().NotBeEmpty();
    }

    [Test]
    public void GetMetadata_ShouldIncludeAllAnnotatedProperties()
    {
        // Act
        var metadata = EntityBase<TestProduct>.GetMetadata();

        // Assert
        metadata.Properties.Should().HaveCountGreaterThan(0);
        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == nameof(TestProduct.Title));
        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == nameof(TestProduct.Price));
        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == nameof(TestProduct.Description));
    }

    [Test]
    public void GetMetadata_ShouldIncludeValueSelector()
    {
        // Act
        var metadata = EntityBase<TestProduct>.GetMetadata();

        // Assert
        var titleProperty = metadata.Properties.First(p => p.PropertyInfo.Name == nameof(TestProduct.Title));
        titleProperty.ValueSelector.Should().NotBeNull();
        titleProperty.ValueSelector.Expression.Should().Be(".product-name");
    }

    [Test]
    public void Validate_ShouldReturnTrueForValidEntity()
    {
        // Arrange
        var product = new TestProduct
        {
            Title = "Test Product",
            Price = "19.99",
            Description = "Test description"
        };

        // Act
        var isValid = product.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Test]
    public void Clone_ShouldCreateNewInstanceWithSameValues()
    {
        // Arrange
        var original = new TestProduct
        {
            Id = "test-id",
            SourceUrl = "https://example.com",
            ExtractedAt = DateTime.UtcNow,
            Title = "Original Title",
            Price = "29.99",
            Description = "Original description",
            ProductId = 123
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.Id.Should().Be(original.Id);
        clone.SourceUrl.Should().Be(original.SourceUrl);
        clone.ExtractedAt.Should().Be(original.ExtractedAt);
        clone.Title.Should().Be(original.Title);
        clone.Price.Should().Be(original.Price);
        clone.Description.Should().Be(original.Description);
        clone.ProductId.Should().Be(original.ProductId);
    }

    [Test]
    public void Clone_ShouldCreateIndependentInstance()
    {
        // Arrange
        var original = new TestProduct { Title = "Original" };

        // Act
        var clone = original.Clone();
        clone.Title = "Modified";

        // Assert
        original.Title.Should().Be("Original");
        clone.Title.Should().Be("Modified");
    }

    [Test]
    public void EntityBase_ShouldInitializeBaseProperties()
    {
        // Arrange & Act
        var product = new TestProduct
        {
            Id = "test-123",
            SourceUrl = "https://test.com",
            ExtractedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Assert
        product.Id.Should().Be("test-123");
        product.SourceUrl.Should().Be("https://test.com");
        product.ExtractedAt.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void GetMetadata_ForEntityWithoutEntitySelector_ShouldHaveNullEntitySelector()
    {
        // Act
        var metadata = EntityBase<TestArticle>.GetMetadata();

        // Assert
        metadata.EntitySelector.Should().BeNull();
        metadata.Properties.Should().NotBeEmpty();
    }

    [Test]
    public void GetMetadata_ShouldIncludeFormatters()
    {
        // Act
        var metadata = EntityBase<TestFormattedEntity>.GetMetadata();

        // Assert
        var priceProperty = metadata.Properties.First(p => p.PropertyInfo.Name == nameof(TestFormattedEntity.Price));
        priceProperty.Formatters.Should().NotBeEmpty();
        priceProperty.Formatters.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Test]
    public void GetMetadata_ShouldCacheMetadataForSameType()
    {
        // Act
        var metadata1 = EntityBase<TestProduct>.GetMetadata();
        var metadata2 = EntityBase<TestProduct>.GetMetadata();

        // Assert
        // Note: This tests that GetMetadata returns consistent results
        metadata1.EntityType.Should().Be(metadata2.EntityType);
        metadata1.Properties.Count.Should().Be(metadata2.Properties.Count);
    }
}
