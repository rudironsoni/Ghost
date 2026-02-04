namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Base abstract class for all formatters that transform extracted values.
/// </summary>
/// <remarks>
/// Formatters provide a modular way to transform data during extraction.
/// They can be applied in sequence, with each formatter processing the output
/// of the previous one. This allows for complex transformations through composition.
/// </remarks>
public abstract class Formatter
{
    /// <summary>
    /// Gets or sets the order in which this formatter should be applied.
    /// Lower values are applied first.
    /// </summary>
    /// <value>The execution order. Defaults to <see cref="int.MaxValue"/>.</value>
    public int Order { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the name of this formatter instance.
    /// </summary>
    /// <value>A descriptive name for logging and debugging purposes.</value>
    public string? Name { get; set; }

    /// <summary>
    /// Formats the input value according to the formatter's rules.
    /// </summary>
    /// <param name="value">The value to format. May be null.</param>
    /// <returns>
    /// The formatted value. May return null if the formatter determines the value
    /// should be removed, or the original value if no transformation is needed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Implementations should:
    /// <list type="bullet">
    /// <item>Handle null inputs gracefully</item>
    /// <item>Preserve type information when possible</item>
    /// <item>Return the original value if the formatter doesn't apply</item>
    /// <item>Throw meaningful exceptions for invalid configurations</item>
    /// </list>
    /// </para>
    /// <para>
    /// Thread Safety: Format method should be thread-safe as it may be called
    /// concurrently for different extraction operations.
    /// </para>
    /// </remarks>
    public abstract object? Format(object? value);

    /// <summary>
    /// Validates the formatter configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the formatter is misconfigured.
    /// </exception>
    /// <remarks>
    /// Override this method to validate formatter-specific configuration.
    /// This method is called during initialization to catch configuration
    /// errors early.
    /// </remarks>
    public virtual void Validate()
    {
        // Base implementation does nothing - derived classes can add validation
    }

    /// <summary>
    /// Returns a string representation of this formatter.
    /// </summary>
    /// <returns>A string describing this formatter.</returns>
    public override string ToString()
    {
        var name = Name ?? GetType().Name;
        return $"{name} (Order: {Order})";
    }
}
