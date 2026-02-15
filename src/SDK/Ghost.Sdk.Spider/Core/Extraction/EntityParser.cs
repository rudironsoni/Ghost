using System.Reflection;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;
using HtmlAgilityPack;

namespace Ghost.Sdk.Spider.Core.Extraction;

/// <summary>
/// Parses entities from content using attribute-based configuration.
/// Supports XPath, CSS, Regex, JSONPath, and JMESPath selectors.
/// </summary>
public class EntityParser
{
    /// <summary>
    /// Extracts entities from the provided content.
    /// </summary>
    /// <typeparam name="T">The entity type to extract.</typeparam>
    /// <param name="context">The extraction context containing the content and metadata.</param>
    /// <returns>A list of extracted entities.</returns>
    public static List<T> Parse<T>(ExtractionContext context) where T : EntityBase<T>, new()
    {
        EntityMetadata metadata = EntityBase<T>.GetMetadata();
        var entities = new List<T>();

        // If no entity selector is defined, treat the entire content as a single entity
        if (metadata.EntitySelector == null)
        {
            T? entity = ParseSingleEntity<T>(context.Content, metadata, context);
            if (entity != null)
                entities.Add(entity);
            return entities;
        }

        // Select entity nodes based on the entity selector
        List<string> entityNodes = SelectEntityNodes(context.Content, metadata.EntitySelector);

        foreach (string nodeContent in entityNodes)
        {
            T? entity = ParseSingleEntity<T>(nodeContent, metadata, context);
            if (entity != null)
                entities.Add(entity);
        }

        return entities;
    }

    /// <summary>
    /// Extracts a single entity from the provided content.
    /// </summary>
    /// <typeparam name="T">The entity type to extract.</typeparam>
    /// <param name="context">The extraction context containing the content and metadata.</param>
    /// <returns>The extracted entity, or null if extraction fails.</returns>
    public static T? ParseSingle<T>(ExtractionContext context) where T : EntityBase<T>, new()
    {
        EntityMetadata metadata = EntityBase<T>.GetMetadata();

        // If an entity selector is defined, use it to find the first matching entity
        if (metadata.EntitySelector != null)
        {
            List<string> entityNodes = SelectEntityNodes(context.Content, metadata.EntitySelector);
            if (entityNodes.Count == 0)
                return null;

            return ParseSingleEntity<T>(entityNodes[0], metadata, context);
        }

        // If no entity selector, treat the entire content as a single entity
        return ParseSingleEntity<T>(context.Content, metadata, context);
    }

    private static List<string> SelectEntityNodes(string content, EntitySelectorAttribute selector)
    {
        // For entity selection, we need to extract the HTML structure, not just text
        // This allows nested selectors to work properly
        if (selector.Type == SelectorType.Css)
        {
            return SelectEntityNodesWithCss(content, selector.Expression, selector.TakeFirst);
        }
        else if (selector.Type == SelectorType.XPath)
        {
            return SelectEntityNodesWithXPath(content, selector.Expression, selector.TakeFirst);
        }
        else if (selector.Type == SelectorType.JsonPath)
        {
            // For JSON, we can use the regular selector as it preserves structure
            ISelector selectorInstance = CreateSelector(selector.Expression, selector.Type, null);
            if (selector.TakeFirst)
            {
                string? firstNode = selectorInstance.SelectFirst(content);
                return firstNode != null ? new List<string> { firstNode } : new List<string>();
            }
            return selectorInstance.SelectValues(content);
        }

        return new List<string>();
    }

    private static List<string> SelectEntityNodesWithCss(string content, string expression, bool takeFirst)
    {
        var parser = new AngleSharp.Html.Parser.HtmlParser();
        AngleSharp.Html.Dom.IHtmlDocument document = parser.ParseDocument(content);
        IHtmlCollection<IElement> elements = document.QuerySelectorAll(expression);

        var results = new List<string>();
        foreach (IElement element in elements)
        {
            // Return the outer HTML to preserve structure for nested selectors
            results.Add(element.OuterHtml);
            if (takeFirst) break;
        }

        return results;
    }

    private static List<string> SelectEntityNodesWithXPath(string content, string expression, bool takeFirst)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(expression);
        if (nodes == null || nodes.Count == 0)
            return new List<string>();

        var results = new List<string>();
        foreach (HtmlNode? node in nodes)
        {
            // Return the outer HTML to preserve structure for nested selectors
            results.Add(node.OuterHtml);
            if (takeFirst) break;
        }

        return results;
    }

    private static T? ParseSingleEntity<T>(string content, EntityMetadata metadata, ExtractionContext context) where T : EntityBase<T>, new()
    {
        var entity = new T
        {
            SourceUrl = context.SourceUrl,
            ExtractedAt = context.Timestamp,
            Id = Guid.NewGuid().ToString()
        };

        // Order properties by their Order attribute (if specified)
        var orderedProperties = metadata.Properties
            .OrderBy(p => p.FieldAttribute?.Order ?? int.MaxValue)
            .ToList();

        foreach (PropertyMetadata? propertyMeta in orderedProperties)
        {
            try
            {
                object? value = ExtractPropertyValue(content, propertyMeta, context);
                if (value != null || !propertyMeta.FieldAttribute?.IgnoreNull == true)
                {
                    SetPropertyValue(entity, propertyMeta, value);
                }
            }
            catch (Exception ex)
            {
                // Log or handle extraction errors
                if (propertyMeta.ValueSelector.Required || propertyMeta.FieldAttribute?.Required == true)
                {
                    throw new InvalidOperationException(
                        $"Failed to extract required property '{propertyMeta.PropertyInfo.Name}': {ex.Message}", ex);
                }
            }
        }

        return entity.Validate() ? entity : null;
    }

    private static object? ExtractPropertyValue(string content, PropertyMetadata propertyMeta, ExtractionContext context)
    {
        ValueSelectorAttribute selector = propertyMeta.ValueSelector;
        ISelector selectorInstance = CreateSelector(selector.Expression, selector.Type, selector.Attribute);

        // Extract raw value(s)
        object? rawValue;
        if (selector.TakeFirst)
        {
            rawValue = selectorInstance.SelectFirst(content);
        }
        else
        {
            List<string> values = selectorInstance.SelectValues(content);
            rawValue = values.Count > 0 ? values : null;
        }

        // Use default value if nothing was extracted
        if (rawValue == null)
        {
            rawValue = selector.DefaultValue;
        }

        // Apply formatters
        if (rawValue != null && propertyMeta.Formatters.Count > 0)
        {
            var orderedFormatters = propertyMeta.Formatters
                .OrderBy(f => f.Order)
                .ToList();

            foreach (FormatterAttribute? formatter in orderedFormatters)
            {
                rawValue = formatter.Format(rawValue);
            }
        }

        // Apply field-level transformations
        if (rawValue is string strValue && propertyMeta.FieldAttribute != null)
        {
            rawValue = ApplyFieldTransformations(strValue, propertyMeta.FieldAttribute);
        }

        // Convert to target property type
        return ConvertValue(rawValue, propertyMeta.PropertyInfo.PropertyType);
    }

    private static string ApplyFieldTransformations(string value, FieldAttribute field)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Remove HTML tags if requested
        if (field.RemoveHtml)
        {
            value = System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", string.Empty);
        }

        // Apply max length
        if (value.Length > field.MaxLength)
        {
            value = value.Substring(0, field.MaxLength);
        }

        // Validate pattern if specified
        if (!string.IsNullOrEmpty(field.Pattern))
        {
            var regex = new System.Text.RegularExpressions.Regex(field.Pattern);
            if (!regex.IsMatch(value))
            {
                if (field.Required)
                    throw new InvalidOperationException($"Value '{value}' does not match required pattern '{field.Pattern}'");
                return string.Empty;
            }
        }

        // Validate min length
        if (value.Length < field.MinLength && field.Required)
        {
            throw new InvalidOperationException($"Value length {value.Length} is less than minimum required length {field.MinLength}");
        }

        return value;
    }

    private static void SetPropertyValue<T>(T entity, PropertyMetadata propertyMeta, object? value) where T : EntityBase<T>, new()
    {
        if (value == null)
        {
            propertyMeta.PropertyInfo.SetValue(entity, null);
            return;
        }

        propertyMeta.PropertyInfo.SetValue(entity, value);
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
            return null;

        // Handle nullable types
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // If already the correct type
        if (value.GetType() == underlyingType)
            return value;

        // Handle List<string> for multi-value selectors
        if (value is List<string> list)
        {
            if (targetType == typeof(List<string>) || targetType == typeof(IList<string>) || targetType == typeof(IEnumerable<string>))
                return list;

            if (targetType == typeof(string[]))
                return list.ToArray();

            // If target is a single string, join the list
            if (underlyingType == typeof(string))
                return string.Join(", ", list);

            // Try to convert first element
            if (list.Count > 0)
                return ConvertValue(list[0], targetType);

            return null;
        }

        // Handle string to other type conversions
        if (value is string strValue)
        {
            if (underlyingType == typeof(string))
                return strValue;

            if (string.IsNullOrWhiteSpace(strValue))
                return null;

            // Numeric types
            if (underlyingType == typeof(int))
                return int.TryParse(strValue, out int intVal) ? intVal : null;

            if (underlyingType == typeof(long))
                return long.TryParse(strValue, out long longVal) ? longVal : null;

            if (underlyingType == typeof(double))
                return double.TryParse(strValue, out double doubleVal) ? doubleVal : null;

            if (underlyingType == typeof(decimal))
                return decimal.TryParse(strValue, out decimal decimalVal) ? decimalVal : null;

            if (underlyingType == typeof(float))
                return float.TryParse(strValue, out float floatVal) ? floatVal : null;

            // Boolean
            if (underlyingType == typeof(bool))
                return bool.TryParse(strValue, out bool boolVal) ? boolVal : null;

            // DateTime
            if (underlyingType == typeof(DateTime))
                return DateTime.TryParse(strValue, out DateTime dateVal) ? dateVal : null;

            if (underlyingType == typeof(DateTimeOffset))
                return DateTimeOffset.TryParse(strValue, out DateTimeOffset dateOffsetVal) ? dateOffsetVal : null;

            // Guid
            if (underlyingType == typeof(Guid))
                return Guid.TryParse(strValue, out Guid guidVal) ? guidVal : null;

            // Enum
            if (underlyingType.IsEnum)
            {
                try
                {
                    return Enum.Parse(underlyingType, strValue, ignoreCase: true);
                }
                catch
                {
                    return null;
                }
            }
        }

        // Try Convert.ChangeType as a fallback
        try
        {
            return Convert.ChangeType(value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static ISelector CreateSelector(string expression, SelectorType type, string? attribute)
    {
        return type switch
        {
            SelectorType.XPath => new XPathSelector(expression, attribute),
            SelectorType.Css => new CssSelector(expression, attribute),
            SelectorType.Regex => new RegexSelector(expression),
            SelectorType.JsonPath => new JsonPathSelector(expression),
            SelectorType.JmesPath => new JmesPathSelector(expression),
            SelectorType.Environment => throw new NotSupportedException("Environment selector is not supported in EntityParser"),
            _ => throw new ArgumentException($"Unsupported selector type: {type}", nameof(type))
        };
    }
}
