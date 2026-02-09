using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Sdk.Pipelines;

/// <summary>
/// Interface for processing media downloads.
/// </summary>
public interface IMediaPipeline
{
    /// <summary>
    /// Processes a media request and downloads the file.
    /// </summary>
    /// <param name="request">The media request containing download parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A media item with download results.</returns>
    Task<MediaItem> ProcessAsync(MediaRequest request, CancellationToken ct = default);
}
