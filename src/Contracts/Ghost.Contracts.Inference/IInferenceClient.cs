using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Contracts.Inference;

/// <summary>
/// Abstraction for inference providers (LLM clients) used by Ghost.
/// </summary>
public interface IInferenceClient
{
    /// <summary>
    /// The provider name (eg. OpenAI, AzureOpenAI, Anthropic).
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Performs a single completion request and returns the final response.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams inference output as it becomes available.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, CancellationToken ct = default);
}
