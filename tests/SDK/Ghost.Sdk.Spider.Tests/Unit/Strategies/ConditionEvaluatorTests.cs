using FluentAssertions;
using Ghost.Sdk.Spider.Strategies;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Strategies;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator;

    public ConditionEvaluatorTests()
    {
        _evaluator = new ConditionEvaluator();
    }

    [Fact]
    public void Evaluate_WithEmptyConditions_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>();
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithAlwaysCondition_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.Always }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithNegatedAlwaysCondition_ShouldReturnFalse()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.Always, Negate = true }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithTimeoutCondition_WhenTimeoutOccurred_ShouldReturnTrue()
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
                Exception = new TimeoutException("Request timed out")
            }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithStatusCodeCondition_WhenMatches_ShouldReturnTrue()
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
            StatusCode = 404
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithStatusCodeGreaterThan_ShouldWork()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.StatusCode,
                Operator = ConditionOperator.GreaterThanOrEqual,
                Value = 400
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            StatusCode = 500
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithElementNotFoundCondition_WhenFound_ShouldReturnTrue()
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
                ErrorMessage = "Element not found: .selector"
            }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithAnyFailedCondition_WhenAnyFailed_ShouldReturnTrue()
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
            new() { StrategyName = "Test2", Success = false, Duration = TimeSpan.FromSeconds(2) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithAllFailedCondition_WhenAllFailed_ShouldReturnTrue()
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
            new() { StrategyName = "Test2", Success = false, Duration = TimeSpan.FromSeconds(2) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithContentMatchCondition_WhenMatches_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.Contains,
                Value = "expected text"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "This contains expected text in the middle"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithPreviousSuccessCondition_WhenPreviousSucceeded_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.PreviousSuccess }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new() { StrategyName = "Test", Success = true, Duration = TimeSpan.FromSeconds(1) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithPreviousFailedCondition_WhenPreviousFailed_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new() { Type = ConditionType.PreviousFailed }
        };
        var context = new StrategyContext { Url = "https://example.com" };
        var attempts = new List<StrategyAttempt>
        {
            new() { StrategyName = "Test", Success = false, Duration = TimeSpan.FromSeconds(1) }
        };

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithRetryCountCondition_WhenMatches_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.RetryCount,
                Operator = ConditionOperator.GreaterThan,
                Value = 2
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
    public void Evaluate_WithCustomCondition_WhenStateMatches_ShouldReturnTrue()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.Custom,
                Field = "customField",
                Operator = ConditionOperator.Equals,
                Value = "expectedValue"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            State = new Dictionary<string, object>
            {
                ["customField"] = "expectedValue"
            }
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithAndOperator_ShouldCombineCorrectly()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.StatusCode,
                Operator = ConditionOperator.Equals,
                Value = 404,
                LogicalOperator = LogicalOperator.And
            },
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.Contains,
                Value = "error"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            StatusCode = 404,
            Content = "This is an error page"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithOrOperator_ShouldCombineCorrectly()
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
                Value = 500
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            StatusCode = 500
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithRegexOperator_ShouldMatch()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.Regex,
                Value = @"\d{3}-\d{3}-\d{4}"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "Call us at 555-123-4567 for more info"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithStartsWithOperator_ShouldMatch()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.StartsWith,
                Value = "Hello"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "Hello, World!"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithEndsWithOperator_ShouldMatch()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.EndsWith,
                Value = "World!"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "Hello, World!"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithNotContainsOperator_ShouldMatch()
    {
        // Arrange
        var conditions = new List<ConditionConfiguration>
        {
            new()
            {
                Type = ConditionType.ContentMatch,
                Operator = ConditionOperator.NotContains,
                Value = "missing"
            }
        };
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "Hello, World!"
        };
        var attempts = new List<StrategyAttempt>();

        // Act
        var result = _evaluator.Evaluate(conditions, context, attempts);

        // Assert
        result.Should().BeTrue();
    }
}
