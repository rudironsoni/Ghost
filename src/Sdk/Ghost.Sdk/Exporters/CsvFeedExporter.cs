using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Sdk.Exporters;

/// <summary>
/// Exports feed items as CSV (Comma-Separated Values) format with headers.
/// Handles proper escaping and quoting per RFC 4180.
/// </summary>
public sealed class CsvFeedExporter : IFeedExporter
{
    private readonly string _delimiter;
    private readonly Encoding _encoding;
    private readonly bool _includeHeaders;

    /// <inheritdoc />
    public string Format => "csv";

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFeedExporter"/> class.
    /// </summary>
    /// <param name="delimiter">The delimiter to use between fields (default: comma).</param>
    /// <param name="encoding">The text encoding to use (default: UTF-8).</param>
    /// <param name="includeHeaders">Whether to include column headers (default: true).</param>
    public CsvFeedExporter(string delimiter = ",", Encoding? encoding = null, bool includeHeaders = true)
    {
        _delimiter = delimiter;
        _encoding = encoding ?? Encoding.UTF8;
        _includeHeaders = includeHeaders;
    }

    /// <inheritdoc />
    public async Task ExportAsync<T>(IEnumerable<T> items, Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(output);

        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return;
        }

        StreamWriter writer = new StreamWriter(output, _encoding, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            // Get properties from the first item
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToArray();

            // Write headers if requested
            if (_includeHeaders)
            {
                string headers = string.Join(_delimiter, properties.Select(p => EscapeField(p.Name)));
                await writer.WriteLineAsync(headers).ConfigureAwait(false);
            }

            // Write data rows
            foreach (T? item in itemsList)
            {
                ct.ThrowIfCancellationRequested();

                IEnumerable<string> values = properties.Select(p =>
                {
                    object? value = p.GetValue(item);
                    return EscapeField(FormatValue(value));
                });

                string row = string.Join(_delimiter, values);
                await writer.WriteLineAsync(row).ConfigureAwait(false);
            }

            await writer.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Escapes a field value according to CSV rules.
    /// Fields containing delimiter, quotes, or newlines are wrapped in quotes.
    /// Internal quotes are escaped by doubling them.
    /// </summary>
    private string EscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Check if the field needs quoting
        bool needsQuoting = value.Contains(_delimiter) ||
                          value.Contains('"') ||
                          value.Contains('\n') ||
                          value.Contains('\r');

        if (!needsQuoting)
        {
            return value;
        }

        // Escape internal quotes by doubling them
        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Formats a value for CSV output.
    /// </summary>
    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
