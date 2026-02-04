using FluentAssertions;
using Ghost.Sdk.Spider.Pipeline;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline;

[TestFixture]
public class SpiderStateBoxTests
{
    private SpiderStateBox _stateBox = null!;

    [SetUp]
    public void Setup()
    {
        _stateBox = new SpiderStateBox();
    }

    [Test]
    public void Constructor_InitializesAllCountersToZero()
    {
        // Arrange & Act
        var stateBox = new SpiderStateBox();

        // Assert
        stateBox.RequestCount.Should().Be(0);
        stateBox.SuccessCount.Should().Be(0);
        stateBox.ErrorCount.Should().Be(0);
        stateBox.RetryCount.Should().Be(0);
    }

    [Test]
    public void IncrementRequestCount_IncrementsCounter()
    {
        // Act
        var result = _stateBox.IncrementRequestCount();

        // Assert
        result.Should().Be(1);
        _stateBox.RequestCount.Should().Be(1);
    }

    [Test]
    public void IncrementRequestCount_MultipleIncrements_IncrementsCorrectly()
    {
        // Act
        _stateBox.IncrementRequestCount();
        _stateBox.IncrementRequestCount();
        var result = _stateBox.IncrementRequestCount();

        // Assert
        result.Should().Be(3);
        _stateBox.RequestCount.Should().Be(3);
    }

    [Test]
    public void IncrementSuccessCount_IncrementsCounter()
    {
        // Act
        var result = _stateBox.IncrementSuccessCount();

        // Assert
        result.Should().Be(1);
        _stateBox.SuccessCount.Should().Be(1);
    }

    [Test]
    public void IncrementErrorCount_IncrementsCounter()
    {
        // Act
        var result = _stateBox.IncrementErrorCount();

        // Assert
        result.Should().Be(1);
        _stateBox.ErrorCount.Should().Be(1);
    }

    [Test]
    public void IncrementRetryCount_IncrementsCounter()
    {
        // Act
        var result = _stateBox.IncrementRetryCount();

        // Assert
        result.Should().Be(1);
        _stateBox.RetryCount.Should().Be(1);
    }

    [Test]
    public void ResetCounters_ResetsAllCountersToZero()
    {
        // Arrange
        _stateBox.IncrementRequestCount();
        _stateBox.IncrementSuccessCount();
        _stateBox.IncrementErrorCount();
        _stateBox.IncrementRetryCount();

        // Act
        _stateBox.ResetCounters();

        // Assert
        _stateBox.RequestCount.Should().Be(0);
        _stateBox.SuccessCount.Should().Be(0);
        _stateBox.ErrorCount.Should().Be(0);
        _stateBox.RetryCount.Should().Be(0);
    }

    [Test]
    public void SetValue_StoresValueInProperties()
    {
        // Act
        _stateBox.SetValue("test_key", "test_value");

        // Assert
        _stateBox.Properties.Should().ContainKey("test_key");
        _stateBox.Properties["test_key"].Should().Be("test_value");
    }

    [Test]
    public void SetValue_WithDifferentTypes_StoresCorrectly()
    {
        // Act
        _stateBox.SetValue("string_key", "value");
        _stateBox.SetValue("int_key", 42);
        _stateBox.SetValue("bool_key", true);

        // Assert
        _stateBox.Properties["string_key"].Should().Be("value");
        _stateBox.Properties["int_key"].Should().Be(42);
        _stateBox.Properties["bool_key"].Should().Be(true);
    }

    [Test]
    public void TryGetValue_WithExistingKey_ReturnsTrue()
    {
        // Arrange
        _stateBox.SetValue("test_key", "test_value");

        // Act
        var result = _stateBox.TryGetValue<string>("test_key", out var value);

        // Assert
        result.Should().BeTrue();
        value.Should().Be("test_value");
    }

    [Test]
    public void TryGetValue_WithMissingKey_ReturnsFalse()
    {
        // Act
        var result = _stateBox.TryGetValue<string>("missing_key", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Test]
    public void TryGetValue_WithWrongType_ReturnsFalse()
    {
        // Arrange
        _stateBox.SetValue("test_key", "string_value");

        // Act
        var result = _stateBox.TryGetValue<int>("test_key", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().Be(0);
    }

    [Test]
    public void GetValueOrDefault_WithExistingKey_ReturnsValue()
    {
        // Arrange
        _stateBox.SetValue("test_key", "test_value");

        // Act
        var result = _stateBox.GetValueOrDefault("test_key", "default");

        // Assert
        result.Should().Be("test_value");
    }

    [Test]
    public void GetValueOrDefault_WithMissingKey_ReturnsDefault()
    {
        // Act
        var result = _stateBox.GetValueOrDefault("missing_key", "default");

        // Assert
        result.Should().Be("default");
    }

    [Test]
    public void GetValueOrDefault_WithNoDefaultSpecified_ReturnsTypeDefault()
    {
        // Act
        var result = _stateBox.GetValueOrDefault<int>("missing_key");

        // Assert
        result.Should().Be(0);
    }

    [Test]
    public void ClearProperties_RemovesAllProperties()
    {
        // Arrange
        _stateBox.SetValue("key1", "value1");
        _stateBox.SetValue("key2", "value2");
        _stateBox.SetValue("key3", "value3");

        // Act
        _stateBox.ClearProperties();

        // Assert
        _stateBox.Properties.Should().BeEmpty();
    }

    [Test]
    public void ClearProperties_DoesNotAffectCounters()
    {
        // Arrange
        _stateBox.IncrementRequestCount();
        _stateBox.IncrementSuccessCount();
        _stateBox.SetValue("key1", "value1");

        // Act
        _stateBox.ClearProperties();

        // Assert
        _stateBox.RequestCount.Should().Be(1);
        _stateBox.SuccessCount.Should().Be(1);
    }

    [Test]
    public void Reset_ClearsPropertiesAndCounters()
    {
        // Arrange
        _stateBox.IncrementRequestCount();
        _stateBox.IncrementSuccessCount();
        _stateBox.IncrementErrorCount();
        _stateBox.IncrementRetryCount();
        _stateBox.SetValue("key1", "value1");
        _stateBox.SetValue("key2", "value2");

        // Act
        _stateBox.Reset();

        // Assert
        _stateBox.RequestCount.Should().Be(0);
        _stateBox.SuccessCount.Should().Be(0);
        _stateBox.ErrorCount.Should().Be(0);
        _stateBox.RetryCount.Should().Be(0);
        _stateBox.Properties.Should().BeEmpty();
    }

    [Test]
    public void Properties_IsThreadSafe_ConcurrentAccess()
    {
        // Arrange
        var tasks = new List<Task>();
        const int iterations = 100;

        // Act - Multiple threads writing concurrently
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    _stateBox.SetValue($"key_{threadId}_{j}", $"value_{threadId}_{j}");
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All values should be present
        _stateBox.Properties.Count.Should().Be(1000);
    }

    [Test]
    public void Counters_AreThreadSafe_ConcurrentIncrements()
    {
        // Arrange
        var tasks = new List<Task>();
        const int iterations = 1000;

        // Act - Multiple threads incrementing concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    _stateBox.IncrementRequestCount();
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Count should be exactly 10 * 1000
        _stateBox.RequestCount.Should().Be(10000);
    }

    [Test]
    public void SetValue_WithComplexObject_StoresCorrectly()
    {
        // Arrange
        var complexObject = new List<string> { "item1", "item2", "item3" };

        // Act
        _stateBox.SetValue("complex_key", complexObject);

        // Assert
        var retrieved = _stateBox.GetValueOrDefault<List<string>>("complex_key");
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(complexObject);
    }

    [Test]
    public void SetValue_OverwritesExistingKey()
    {
        // Arrange
        _stateBox.SetValue("key", "original");

        // Act
        _stateBox.SetValue("key", "updated");

        // Assert
        var value = _stateBox.GetValueOrDefault<string>("key");
        value.Should().Be("updated");
    }
}
