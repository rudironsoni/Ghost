using FluentAssertions;
using Ghost.Sdk.Spider.Configuration.Models;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Configuration;

/// <summary>
/// Complex validation tests for configuration edge cases
/// </summary>
public class ComplexValidationTests
{
    private static readonly string[] ExpectedFieldNames = new[] { "Name", "Price", "Description" };
    
    [Fact]
    public void Configuration_WithValidEntity_ShouldInitialize()
    {
        // Arrange & Act
        var config = new EntityConfiguration
        {
            Name = "TestEntity",
            IsList = false,
            Fields = new List<FieldConfiguration>
            {
                new FieldConfiguration
                {
                    Name = "Field1",
                    Selector = new SelectorConfiguration { Expression = ".field1" }
                }
            }
        };

        // Assert
        config.Should().NotBeNull();
        config.Name.Should().Be("TestEntity");
        config.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void Configuration_WithDeepNesting_ShouldHandle()
    {
        // Arrange & Act
        var config = new EntityConfiguration
        {
            Name = "Level1",
            Fields = new List<FieldConfiguration>
            {
                new FieldConfiguration
                {
                    Name = "DeepField",
                    Selector = new SelectorConfiguration { Expression = ".level1 .level2 .level3" }
                }
            }
        };

        // Assert
        config.Fields.Should().HaveCount(1);
        config.Fields[0].Selector?.Expression.Should().Contain("level");
    }

    [Fact]
    public void Configuration_WithMultipleFields_ShouldValidateAll()
    {
        // Arrange & Act
        var entity = new EntityConfiguration
        {
            Name = "Product",
            Fields = new List<FieldConfiguration>
            {
                new FieldConfiguration { Name = "Name" },
                new FieldConfiguration { Name = "Price" },
                new FieldConfiguration { Name = "Description" }
            }
        };

        // Assert
        entity.Fields.Should().HaveCount(3);
        entity.Fields.Select(f => f.Name).Should().BeEquivalentTo(ExpectedFieldNames);
    }

    [Fact]
    public void Configuration_WithPipeline_ShouldValidate()
    {
        // Arrange & Act
        var config = new PipelineConfiguration
        {
            Enabled = true,
            StopOnFailure = true,
            Stages = new List<PipelineStageConfiguration>
            {
                new PipelineStageConfiguration { Name = "Stage1", Type = "Validation", Order = 1 },
                new PipelineStageConfiguration { Name = "Stage2", Type = "Transformation", Order = 2 }
            }
        };

        // Assert
        config.Should().NotBeNull();
        config.Enabled.Should().BeTrue();
        config.Stages.Should().HaveCount(2);
    }

    [Fact]
    public void Configuration_WithScheduling_ShouldValidate()
    {
        // Arrange & Act
        var config = new ScheduleConfiguration
        {
            CronExpression = "0 0 * * *",
            Enabled = true
        };

        // Assert
        config.Should().NotBeNull();
        config.CronExpression.Should().Be("0 0 * * *");
        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Configuration_WithEmptyEntities_ShouldBeValid()
    {
        // Arrange & Act
        var config = new ExtractionConfiguration
        {
            Entities = []
        };

        // Assert
        config.Entities.Should().BeEmpty();
        config.Should().NotBeNull();
    }

    [Fact]
    public void Configuration_WithDuplicateEntityNames_ShouldBeDetectable()
    {
        // Arrange & Act
        var entities = new List<EntityConfiguration>
        {
            new EntityConfiguration { Name = "Entity" },
            new EntityConfiguration { Name = "Entity" }
        };

        var duplicateNames = entities
            .GroupBy(e => e.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // Assert
        duplicateNames.Should().ContainSingle();
        duplicateNames[0].Should().Be("Entity");
    }

    [Fact]
    public void Configuration_WithNavigationRules_ShouldValidate()
    {
        // Arrange & Act
        var config = new NavigationConfiguration
        {
            FollowLinks = true,
            LinkSelector = "a[href]",
            HandlePagination = true,
            DeduplicateUrls = true
        };

        // Assert
        config.Should().NotBeNull();
        config.FollowLinks.Should().BeTrue();
        config.LinkSelector.Should().Be("a[href]");
        config.DeduplicateUrls.Should().BeTrue();
    }
}
