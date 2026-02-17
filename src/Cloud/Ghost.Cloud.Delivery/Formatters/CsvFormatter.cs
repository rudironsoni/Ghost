using System.Text;
using System.Text.Json;

namespace Ghost.Cloud.Delivery.Formatters;

public sealed class CsvFormatter : IResultFormatter
{
    public string FormatType => "csv";
    public string Extension => "csv";
    public string ContentType => "text/csv";

    public byte[] FormatData(List<JsonElement> items)
    {
        if (items.Count == 0)
        {
            return Encoding.UTF8.GetBytes(string.Empty);
        }

        var sb = new StringBuilder();

        // Get headers from first item
        List<string> headers = GetHeaders(items[0]);

        // Write header
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

        // Write rows
        foreach (JsonElement item in items)
        {
            sb.AppendLine(string.Join(",", headers.Select(h => EscapeCsvField(GetFieldValue(item, h)))));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static List<string> GetHeaders(JsonElement element)
    {
        var headers = new List<string>();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                headers.Add(property.Name);
            }
        }
        return headers;
    }

    private static string GetFieldValue(JsonElement element, string fieldName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(fieldName, out JsonElement value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => value.ToString()
            };
        }
        return string.Empty;
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        // Check if we need to quote the field
        bool needsQuoting = field.Contains('"') ||
                           field.Contains(',') ||
                           field.Contains('\n') ||
                           field.Contains('\r');

        if (!needsQuoting)
        {
            return field;
        }

        // Escape quotes by doubling them and wrap in quotes
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
