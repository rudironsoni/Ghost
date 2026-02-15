using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Sdk.Exporters;

/// <summary>
/// Defines a contract for exporting feed items to various formats.
/// </summary>
public interface IFeedExporter
{
    /// <summary>
    /// Gets the format identifier for this exporter.
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// Exports items to the specified output stream asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of items to export.</typeparam>
    /// <param name="items">The collection of items to export.</param>
    /// <param name="output">The output stream to write to.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous export operation.</returns>
    public Task ExportAsync<T>(IEnumerable<T> items, Stream output, CancellationToken ct = default);
}
