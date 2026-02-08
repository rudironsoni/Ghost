using FluentAssertions;
using Ghost.Sdk.Spider.Strategies;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Strategies;

/// <summary>
/// Additional comprehensive tests for ConditionEvaluator to increase coverage.
/// </summary>
public class ConditionEvaluatorAdditionalTests
{
    private readonly ConditionEvaluator _evaluator;

    public ConditionEvaluatorAdditionalTests()
    {
        _evaluator = new ConditionEvaluator();
    }

    [Fact]
    public void Evaluate_WithElapsedTimeCondition_WhenTimeElapsed_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ElapsedTime,
                Operator = ConditionOperator.GreaterThan,
                Value = TimeSpan.FromSeconds(2)
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Timestamp = DateTime.UtcNow.AddSeconds(-5)
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithElapsedTimeCondition_WithIntegerSeconds_ShouldWork()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ElapsedTime,
                Operator = ConditionOperator.GreaterThan,
                Value = 2 // Integer seconds
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Timestamp = DateTime.UtcNow.AddSeconds(-5)
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithElapsedTimeCondition_WithDoubleSeconds_ShouldWork()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ElapsedTime,
                Operator = ConditionOperator.LessThan,
                Value = 100.5 // Double seconds
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Timestamp = DateTime.UtcNow.AddSeconds(-5)
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithTimeoutCondition_WithErrorMessageContainingTimeout_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.Timeout }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new()
            {
                StrategyName = "Test",
                Success = false,
                Duration = TimeSpan.FromSeconds(30),
                ErrorMessage = "Request TIMEOUT occurred"
            }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithTimeoutCondition_WithNoAttempts_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.Timeout }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithStatusCodeCondition_WithNoStatusCode_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.StatusCode,
                Operator = ConditionOperator.Equals,
                Value = 404
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            StatusCode = null
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithStatusCodeCondition_WithNoValue_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.StatusCode,
                Operator = ConditionOperator.Equals,
                Value = null
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            StatusCode = 404
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithElementNotFoundCondition_WithSelectorNotFound_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.ElementNotFound }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new()
            {
                StrategyName = "Test",
                Success = false,
                Duration = TimeSpan.FromSeconds(5),
                ErrorMessage = "Selector not found: #my-selector"
            }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithAnyFailedCondition_WithNoAttempts_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.AnyFailed }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithAnyFailedCondition_WithAllSuccessful_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.AnyFailed }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new() { StrategyName = "Test1", Success = true, Duration = TimeSpan.FromSeconds(1) },
            new() { StrategyName = "Test2", Success = true, Duration = TimeSpan.FromSeconds(2) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithAllFailedCondition_WithNoAttempts_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.AllFailed }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithAllFailedCondition_WithSomeSuccessful_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.AllFailed }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new() { StrategyName = "Test1", Success = false, Duration = TimeSpan.FromSeconds(1) },
            new() { StrategyName = "Test2", Success = true, Duration = TimeSpan.FromSeconds(2) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithContentMatchCondition_WithEmptyContent_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.Contains,
                Value = "expected"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = ""
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithContentMatchCondition_WithNullValue_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.Contains,
                Value = null
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "some content"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithPreviousSuccessCondition_WithNoAttempts_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.PreviousSuccess }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithPreviousSuccessCondition_WithPreviousFailed_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.PreviousSuccess }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new() { StrategyName = "Test", Success = false, Duration = TimeSpan.FromSeconds(1) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithPreviousFailedCondition_WithNoAttempts_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.PreviousFailed }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithRetryCountCondition_WithNoValue_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.RetryCount,
                Operator = ConditionOperator.GreaterThan,
                Value = null
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            RetryCount = 3
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithRetryCountCondition_WithLessThanOperator_ShouldWork()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.RetryCount,
                Operator = ConditionOperator.LessThan,
                Value = 5
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            RetryCount = 3
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithCustomCondition_WithNoField_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = null,
                Value = "test"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithCustomCondition_WithFieldInParameters_ShouldWork()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "paramField",
                Operator = ConditionOperator.Equals,
                Value = "paramValue"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Parameters = new Dictionary<string, object>
            {
                ["paramField"] = "paramValue"
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithCustomCondition_WithNoValue_AndFieldExists_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "testField",
                Value = null
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            State = new Dictionary<string, object>
            {
                ["testField"] = "anyValue"
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithInOperator_WhenValueInCollection_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "status",
                Operator = ConditionOperator.In,
                Value = new List<object> { 200, 201, 202 }
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            State = new Dictionary<string, object>
            {
                ["status"] = 201
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithNotInOperator_WhenValueNotInCollection_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "status",
                Operator = ConditionOperator.NotIn,
                Value = new List<object> { 400, 401, 403 }
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            State = new Dictionary<string, object>
            {
                ["status"] = 200
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithNotEqualsOperator_WhenValuesDifferent_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "counter",
                Operator = ConditionOperator.NotEquals,
                Value = 5
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            State = new Dictionary<string, object>
            {
                ["counter"] = 10
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithLessThanOrEqualOperator_ShouldWork()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "counter",
                Operator = ConditionOperator.LessThanOrEqual,
                Value = 10
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            State = new Dictionary<string, object>
            {
                ["counter"] = 10
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithUnknownConditionType_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = (ConditionType)999 // Unknown type
            }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithMixedAndOrOperators_ShouldEvaluateCorrectly()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.StatusCode,
                Operator = ConditionOperator.Equals,
                Value = 404,
                LogicalOperator = LogicalOperator.Or
            },
            new()
            {
                Type = ConditionType.StatusCode,
                Operator = ConditionOperator.Equals,
                Value = 500,
                LogicalOperator = LogicalOperator.And
            },
            new()
            {
                Type = ConditionType.RetryCount,
                Operator = ConditionOperator.LessThan,
                Value = 5
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            StatusCode = 404,
            RetryCount = 3
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }
}
