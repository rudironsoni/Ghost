using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Downloader;

/// <summary>
/// Fake downloader for offline testing that returns deterministic responses.
/// </summary>
public sealed class FakeDownloader : IDownloader
{
    private readonly Func<GhostRequest, GhostResponse>? _responseFactory;

    public FakeDownloader(Func<GhostRequest, GhostResponse>? responseFactory = null)
    {
        _responseFactory = responseFactory ?? CreateDefaultResponse;
    }

    public Task<GhostResponse> DownloadAsync(GhostRequest request, GhostEngineContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GhostResponse response = _responseFactory!(request);
        return Task.FromResult(response);
    }

    private static GhostResponse CreateDefaultResponse(GhostRequest request)
    {
        return new GhostResponse(
            Url: request.Url,
            StatusCode: 200,
            Headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "text/html"
            },
            Content: $"<html><body>Response for {request.Url}</body></html>",
            ReceivedAtUtc: DateTimeOffset.UtcNow);
    }
}
