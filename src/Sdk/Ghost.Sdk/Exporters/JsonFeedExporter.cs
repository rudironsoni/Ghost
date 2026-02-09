using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Sdk.Exporters;

/// <summary>
/// Exports feed items as JSON Lines (newline-delimited JSON) format.
/// Each item is written as a separate JSON object on its own line.
/// </summary>
public sealed class JsonFeedExporter : IFeedExporter
{
    private readonly JsonSerializerOptions _options;

    /// <inheritdoc />
    public string Format => "jsonl";

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFeedExporter"/> class.
    /// </summary>
    /// <param name="options">Optional JSON serialization options. If null, default options are used.</param>
    public JsonFeedExporter(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public async Task ExportAsync<T>(IEnumerable<T> items, Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(output);

        await using var writer = new StreamWriter(output, leaveOpen: true);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var json = JsonSerializer.Serialize(item, _options);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
    }
}
