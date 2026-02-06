using System.Text.RegularExpressions;

namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Evaluates conditions to determine if a strategy should execute.
/// </summary>
public class ConditionEvaluator
{
    /// <summary>
    /// Evaluates a list of conditions against the provided context and attempts.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="context">The strategy context.</param>
    /// <param name="attempts">The list of previous strategy attempts.</param>
    /// <returns><c>true</c> if the conditions are met; otherwise, <c>false</c>.</returns>
    public static bool Evaluate(List<ConditionConfiguration> conditions, StrategyContext context, List<StrategyAttempt> attempts)
    {
        if (conditions.Count == 0)
        {
            return true;
        }

        bool result = true;
        LogicalOperator currentOperator = LogicalOperator.And;

        foreach (var condition in conditions)
        {
            var conditionResult = EvaluateCondition(condition, context, attempts);

            if (condition.Negate)
            {
                conditionResult = !conditionResult;
            }

            // Apply logical operator from previous condition
            if (currentOperator == LogicalOperator.And)
            {
                result = result && conditionResult;
            }
            else
            {
                result = result || conditionResult;
            }

            // Store operator for next iteration
            currentOperator = condition.LogicalOperator;
        }

        return result;
    }

    /// <summary>
    /// Evaluates a single condition.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="context">The strategy context.</param>
    /// <param name="attempts">The list of previous strategy attempts.</param>
    /// <returns><c>true</c> if the condition is met; otherwise, <c>false</c>.</returns>
    private static bool EvaluateCondition(ConditionConfiguration condition, StrategyContext context, List<StrategyAttempt> attempts)
    {
        return condition.Type switch
        {
            ConditionType.Always => true,
            ConditionType.Timeout => EvaluateTimeout(condition, attempts),
            ConditionType.StatusCode => EvaluateStatusCode(condition, context),
            ConditionType.ElementNotFound => EvaluateElementNotFound(condition, attempts),
            ConditionType.AnyFailed => EvaluateAnyFailed(attempts),
            ConditionType.AllFailed => EvaluateAllFailed(attempts),
            ConditionType.ContentMatch => EvaluateContentMatch(condition, context),
            ConditionType.PreviousSuccess => EvaluatePreviousSuccess(attempts),
            ConditionType.PreviousFailed => EvaluatePreviousFailed(attempts),
            ConditionType.RetryCount => EvaluateRetryCount(condition, context),
            ConditionType.ElapsedTime => EvaluateElapsedTime(condition, context),
            ConditionType.Custom => EvaluateCustom(condition, context),
            _ => false
        };
    }

    /// <summary>
    /// Evaluates a timeout condition.
    /// </summary>
    private static bool EvaluateTimeout(ConditionConfiguration condition, List<StrategyAttempt> attempts)
    {
        var lastAttempt = attempts.LastOrDefault();
        if (lastAttempt == null)
        {
            return false;
        }

        var timeoutOccurred = lastAttempt.Exception is TimeoutException ||
                             (lastAttempt.ErrorMessage?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);

        return timeoutOccurred;
    }

    /// <summary>
    /// Evaluates a status code condition.
    /// </summary>
    private static bool EvaluateStatusCode(ConditionConfiguration condition, StrategyContext context)
    {
        if (!context.StatusCode.HasValue || condition.Value == null)
        {
            return false;
        }

        return CompareValues(context.StatusCode.Value, condition.Value, condition.Operator);
    }

    /// <summary>
    /// Evaluates an element not found condition.
    /// </summary>
    private static bool EvaluateElementNotFound(ConditionConfiguration condition, List<StrategyAttempt> attempts)
    {
        var lastAttempt = attempts.LastOrDefault();
        if (lastAttempt == null)
        {
            return false;
        }

        var elementNotFound = lastAttempt.ErrorMessage?.Contains("element not found", StringComparison.OrdinalIgnoreCase) ?? false;
        elementNotFound |= lastAttempt.ErrorMessage?.Contains("selector not found", StringComparison.OrdinalIgnoreCase) ?? false;

        return elementNotFound;
    }

    /// <summary>
    /// Evaluates whether any previous strategy failed.
    /// </summary>
    private static bool EvaluateAnyFailed(List<StrategyAttempt> attempts)
    {
        return attempts.Any(a => !a.Success);
    }

    /// <summary>
    /// Evaluates whether all previous strategies failed.
    /// </summary>
    private static bool EvaluateAllFailed(List<StrategyAttempt> attempts)
    {
        return attempts.Count > 0 && attempts.All(a => !a.Success);
    }

    /// <summary>
    /// Evaluates a content match condition.
    /// </summary>
    private static bool EvaluateContentMatch(ConditionConfiguration condition, StrategyContext context)
    {
        if (string.IsNullOrEmpty(context.Content) || condition.Value == null)
        {
            return false;
        }

        var pattern = condition.Value.ToString() ?? string.Empty;
        return CompareStrings(context.Content, pattern, condition.Operator);
    }

    /// <summary>
    /// Evaluates whether the previous strategy succeeded.
    /// </summary>
    private static bool EvaluatePreviousSuccess(List<StrategyAttempt> attempts)
    {
        var lastAttempt = attempts.LastOrDefault();
        return lastAttempt?.Success ?? false;
    }

    /// <summary>
    /// Evaluates whether the previous strategy failed.
    /// </summary>
    private static bool EvaluatePreviousFailed(List<StrategyAttempt> attempts)
    {
        var lastAttempt = attempts.LastOrDefault();
        return lastAttempt != null && !lastAttempt.Success;
    }

    /// <summary>
    /// Evaluates a retry count condition.
    /// </summary>
    private static bool EvaluateRetryCount(ConditionConfiguration condition, StrategyContext context)
    {
        if (condition.Value == null)
        {
            return false;
        }

        return CompareValues(context.RetryCount, condition.Value, condition.Operator);
    }

    /// <summary>
    /// Evaluates an elapsed time condition.
    /// </summary>
    private static bool EvaluateElapsedTime(ConditionConfiguration condition, StrategyContext context)
    {
        if (condition.Value == null)
        {
            return false;
        }

        var elapsed = DateTime.UtcNow - context.Timestamp;
        var threshold = condition.Value switch
        {
            TimeSpan ts => ts,
            int seconds => TimeSpan.FromSeconds(seconds),
            double seconds => TimeSpan.FromSeconds(seconds),
            _ => TimeSpan.Zero
        };

        return CompareValues(elapsed.TotalSeconds, threshold.TotalSeconds, condition.Operator);
    }

    /// <summary>
    /// Evaluates a custom condition.
    /// </summary>
    private static bool EvaluateCustom(ConditionConfiguration condition, StrategyContext context)
    {
        // Custom conditions can be extended by checking context.State or Parameters
        if (string.IsNullOrEmpty(condition.Field))
        {
            return false;
        }

        // Check if the field exists in context state
        if (context.State.TryGetValue(condition.Field, out var value))
        {
            return condition.Value == null || CompareValues(value, condition.Value, condition.Operator);
        }

        // Check if the field exists in context parameters
        if (context.Parameters.TryGetValue(condition.Field, out value))
        {
            return condition.Value == null || CompareValues(value, condition.Value, condition.Operator);
        }

        return false;
    }

    /// <summary>
    /// Compares two values using the specified operator.
    /// </summary>
    private static bool CompareValues(object actual, object expected, ConditionOperator op)
    {
        // Convert to comparable types
        if (actual is IComparable actualComparable && TryConvertToComparable(expected, actual.GetType(), out var expectedComparable))
        {
            var comparison = actualComparable.CompareTo(expectedComparable);

            return op switch
            {
                ConditionOperator.Equals => comparison == 0,
                ConditionOperator.NotEquals => comparison != 0,
                ConditionOperator.GreaterThan => comparison > 0,
                ConditionOperator.GreaterThanOrEqual => comparison >= 0,
                ConditionOperator.LessThan => comparison < 0,
                ConditionOperator.LessThanOrEqual => comparison <= 0,
                _ => false
            };
        }

        // String comparisons
        if (actual is string actualStr && expected is string expectedStr)
        {
            return CompareStrings(actualStr, expectedStr, op);
        }

        // Collection comparisons
        if (op == ConditionOperator.In && expected is IEnumerable<object> collection)
        {
            return collection.Contains(actual);
        }

        if (op == ConditionOperator.NotIn && expected is IEnumerable<object> notCollection)
        {
            return !notCollection.Contains(actual);
        }

        return false;
    }

    /// <summary>
    /// Compares two strings using the specified operator.
    /// </summary>
    private static bool CompareStrings(string actual, string expected, ConditionOperator op)
    {
        return op switch
        {
            ConditionOperator.Equals => actual.Equals(expected, StringComparison.Ordinal),
            ConditionOperator.NotEquals => !actual.Equals(expected, StringComparison.Ordinal),
            ConditionOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotContains => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.StartsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.EndsWith => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Regex => Regex.IsMatch(actual, expected),
            _ => false
        };
    }

    /// <summary>
    /// Attempts to convert a value to a comparable type.
    /// </summary>
    private static bool TryConvertToComparable(object value, Type targetType, out IComparable? result)
    {
        result = null;

        try
        {
            if (value.GetType() == targetType)
            {
                result = value as IComparable;
                return result != null;
            }

            var converted = Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
            result = converted as IComparable;
            return result != null;
        }
        catch
        {
            return false;
        }
    }
}
