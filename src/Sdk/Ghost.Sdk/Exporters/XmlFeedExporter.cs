using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Ghost.Sdk.Exporters;

/// <summary>
/// Exports feed items as XML format with proper escaping.
/// Each item is wrapped in an element based on the item type name.
/// </summary>
public sealed class XmlFeedExporter : IFeedExporter
{
    private readonly Encoding _encoding;
    private readonly bool _indent;
    private readonly string _rootElementName;

    /// <inheritdoc />
    public string Format => "xml";

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlFeedExporter"/> class.
    /// </summary>
    /// <param name="encoding">The text encoding to use (default: UTF-8).</param>
    /// <param name="indent">Whether to indent the XML output (default: true).</param>
    /// <param name="rootElementName">The name of the root element (default: "items").</param>
    public XmlFeedExporter(Encoding? encoding = null, bool indent = true, string rootElementName = "items")
    {
        _encoding = encoding ?? Encoding.UTF8;
        _indent = indent;
        _rootElementName = rootElementName;
    }

    /// <inheritdoc />
    public async Task ExportAsync<T>(IEnumerable<T> items, Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(output);

        var itemsList = items.ToList();

        // Determine the element name for individual items
        string itemElementName = typeof(T).Name.ToLowerInvariant();
        if (itemElementName.EndsWith("item", StringComparison.OrdinalIgnoreCase))
        {
            itemElementName = itemElementName[..^4]; // Remove "item" suffix
        }
        if (string.IsNullOrEmpty(itemElementName))
        {
            itemElementName = "item";
        }

        // Create XML document
        var root = new XElement(_rootElementName);

        foreach (T? item in itemsList)
        {
            ct.ThrowIfCancellationRequested();

            XElement itemElement = CreateItemElement(item, itemElementName);
            root.Add(itemElement);
        }

        var document = new XDocument(
            new XDeclaration("1.0", _encoding.WebName, "yes"),
            root
        );

        // Write to stream
        var settings = new XmlWriterSettings
        {
            Encoding = _encoding,
            Indent = _indent,
            Async = true,
            OmitXmlDeclaration = false
        };

        XmlWriter writer = XmlWriter.Create(output, settings);
        try
        {
            await document.WriteToAsync(writer, ct).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates an XML element from an item object.
    /// </summary>
    private static XElement CreateItemElement<T>(T item, string elementName)
    {
        var element = new XElement(elementName);

        if (item == null)
        {
            return element;
        }

        IEnumerable<PropertyInfo> properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (PropertyInfo? property in properties)
        {
            object? value = property.GetValue(item);
            string propertyName = ToCamelCase(property.Name);

            if (value == null)
            {
                element.Add(new XElement(propertyName));
            }
            else if (IsSimpleType(property.PropertyType))
            {
                element.Add(new XElement(propertyName, FormatValue(value)));
            }
            else if (value is System.Collections.IEnumerable enumerable and not string)
            {
                var listElement = new XElement(propertyName);
                foreach (object? listItem in enumerable)
                {
                    if (listItem != null)
                    {
                        listElement.Add(new XElement("item", FormatValue(listItem)));
                    }
                }
                element.Add(listElement);
            }
            else
            {
                // Complex type - serialize as nested element
                element.Add(CreateItemElement(value, propertyName));
            }
        }

        return element;
    }

    /// <summary>
    /// Determines if a type is a simple type that can be directly serialized.
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               (Nullable.GetUnderlyingType(type) is Type underlyingType && IsSimpleType(underlyingType));
    }

    /// <summary>
    /// Formats a value for XML output.
    /// </summary>
    private static string FormatValue(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            DateTime dt => dt.ToString("o"),
            DateTimeOffset dto => dto.ToString("o"),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
        {
            return input;
        }

        return char.ToLowerInvariant(input[0]) + input[1..];
    }
}
