using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Downloader;

public interface IDownloaderMiddleware
{
    public Task<GhostResponse> InvokeAsync(
        GhostRequest request,
        GhostEngineContext context,
        Func<GhostRequest, GhostEngineContext, CancellationToken, Task<GhostResponse>> nextStep,
        CancellationToken cancellationToken = default);
}
