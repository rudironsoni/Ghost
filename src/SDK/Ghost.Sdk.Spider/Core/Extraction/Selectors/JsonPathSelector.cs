using Json.Path;
using System.Text.Json.Nodes;

namespace Ghost.Sdk.Spider.Core.Extraction.Selectors;

/// <summary>
/// Selects values from JSON content using JSONPath expressions.
/// </summary>
public class JsonPathSelector : ISelector
{
    private readonly JsonPath _jsonPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathSelector"/> class.
    /// </summary>
    /// <param name="expression">The JSONPath expression.</param>
    public JsonPathSelector(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentNullException(nameof(expression));

        Expression = expression;
        _jsonPath = JsonPath.Parse(expression);
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
            var jsonNode = JsonNode.Parse(content);
            if (jsonNode == null)
                return new List<string>();

            var result = _jsonPath.Evaluate(jsonNode);

            if (result.Matches == null || result.Matches.Count == 0)
                return new List<string>();

            var results = new List<string>();
            foreach (var match in result.Matches)
            {
                var value = ExtractValue(match.Value);
                if (value != null)
                    results.Add(value);
            }

            return results;
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
            var jsonNode = JsonNode.Parse(content);
            if (jsonNode == null)
                return null;

            var result = _jsonPath.Evaluate(jsonNode);

            if (result.Matches == null || result.Matches.Count == 0)
                return null;

            return ExtractValue(result.Matches[0].Value);
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
            var jsonNode = JsonNode.Parse("{}");
            if (jsonNode != null)
                _jsonPath.Evaluate(jsonNode);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractValue(JsonNode? node)
    {
        if (node == null)
            return null;

        return node switch
        {
            JsonValue value => value.ToJsonString(),
            JsonObject obj => obj.ToJsonString(),
            JsonArray arr => arr.ToJsonString(),
            _ => null
        };
    }
}
