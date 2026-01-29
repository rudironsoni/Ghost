using Ghost.Contracts.Inference;

namespace Ghost.Platform.Google.Gemini;

/// <summary>
/// Thin wrapper around existing GoogleClient implementation.
/// </summary>
public sealed class GeminiClient : IInferenceClient
{
    private readonly GoogleClient _inner;

    public GeminiClient(GoogleClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string ProviderName => _inner.ProviderName;

    public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default)
        => _inner.CompleteAsync(request, ct);

    public IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, CancellationToken ct = default)
        => _inner.StreamAsync(request, ct);
}
