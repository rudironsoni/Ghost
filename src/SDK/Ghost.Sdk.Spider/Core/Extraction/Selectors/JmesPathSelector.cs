using DevLab.JmesPath;
using Newtonsoft.Json.Linq;

namespace Ghost.Sdk.Spider.Core.Extraction.Selectors;

/// <summary>
/// Selects values from JSON content using JMESPath expressions.
/// JMESPath is more powerful than JSONPath and supports transformations.
/// </summary>
public class JmesPathSelector : ISelector
{
    private readonly JmesPath _jmesPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JmesPathSelector"/> class.
    /// </summary>
    /// <param name="expression">The JMESPath expression.</param>
    public JmesPathSelector(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentNullException(nameof(expression));

        Expression = expression;
        _jmesPath = new JmesPath();
    }

    /// <inheritdoc/>
    public string Expression { get; }

    /// <inheritdoc/>
    public List<string> Select(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<string>();

        try
        {
            var result = _jmesPath.Transform(content, Expression);

            if (string.IsNullOrEmpty(result))
                return new List<string>();

            var parsedResult = JToken.Parse(result);

            if (parsedResult is JArray array)
            {
                var results = new List<string>();
                foreach (var item in array)
                {
                    var value = ExtractValue(item);
                    if (value != null)
                        results.Add(value);
                }
                return results;
            }

            var singleValue = ExtractValue(parsedResult);
            return singleValue != null ? new List<string> { singleValue } : new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <inheritdoc/>
    public string? SelectFirst(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            var result = _jmesPath.Transform(content, Expression);

            if (string.IsNullOrEmpty(result))
                return null;

            var parsedResult = JToken.Parse(result);

            if (parsedResult is JArray array && array.Count > 0)
            {
                return ExtractValue(array[0]);
            }

            return ExtractValue(parsedResult);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public bool Validate()
    {
        try
        {
            _jmesPath.Transform("{}", Expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractValue(JToken token)
    {
        return token.Type switch
        {
            JTokenType.String => token.Value<string>(),
            JTokenType.Integer => token.Value<long>().ToString(),
            JTokenType.Float => token.Value<double>().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JTokenType.Boolean => token.Value<bool>().ToString().ToLowerInvariant(),
            JTokenType.Null => null,
            JTokenType.Object => token.ToString(Newtonsoft.Json.Formatting.None),
            JTokenType.Array => token.ToString(Newtonsoft.Json.Formatting.None),
            _ => null
        };
    }
}
