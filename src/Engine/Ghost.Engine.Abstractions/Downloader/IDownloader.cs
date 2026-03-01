using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Downloader;

public interface IDownloader
{
    public Task<GhostResponse> DownloadAsync(GhostRequest request, GhostEngineContext context, CancellationToken cancellationToken = default);
}
