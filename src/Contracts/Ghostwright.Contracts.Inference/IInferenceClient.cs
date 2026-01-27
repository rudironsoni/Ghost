using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghostwright.Contracts.Inference;

/// <summary>
/// Abstraction for inference providers (LLM clients) used by Ghostwright.
/// </summary>
public interface IInferenceClient
{
    /// <summary>
    /// The provider name (eg. OpenAI, AzureOpenAI, Anthropic).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Performs a single completion request and returns the final response.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams inference output as it becomes available.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, CancellationToken ct = default);
}
