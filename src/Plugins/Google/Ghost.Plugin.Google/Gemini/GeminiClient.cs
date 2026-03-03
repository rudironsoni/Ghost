using Ghost.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Google.Gemini;

/// <summary>
/// Browser-driven client for gemini.google.com using the Ghost kernel.
/// </summary>
public sealed partial class GeminiClient : Ghost.Contracts.Inference.IInferenceClient
{
    private readonly Ghost.IBrowserSession _session;
    private readonly Gemini.GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(Ghost.IBrowserSession session, IOptions<Gemini.GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new Gemini.GeminiOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiClient>.Instance;
    }

    public string ProviderName => "Google";

    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default)
    {
        var stringBuilder = new System.Text.StringBuilder();
        await foreach (InferenceChunk chunk in StreamAsync(request, ct).ConfigureAwait(false)) stringBuilder.Append(chunk.Delta);
        return new Ghost.Contracts.Inference.InferenceResponse
        {
            Model = request.Model ?? _options.DefaultModel,
            Content = stringBuilder.ToString()
        };
    }

    public async IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IPage page = await _session.NewPageAsync(ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync(_options.BaseUrl, ct: ct).ConfigureAwait(false);
            try { await page.WaitForSelectorAsync("textarea", ct: ct).ConfigureAwait(false); } catch (Exception ex) { _logger.LogError(ex, "Wait for textarea selector failed"); }

            string prompt = string.Join("\n", request.Messages.Select(m => m.Content));
            await page.TypeAsync("textarea", prompt, ct: ct).ConfigureAwait(false);
            await page.PressAsync("textarea", "Enter", ct).ConfigureAwait(false);

            string last = string.Empty;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!ct.IsCancellationRequested && stopwatch.Elapsed < _options.ResponseTimeout)
            {
                string content = string.Empty;
                try
                {
                    content = await page.EvaluateAsync<string>("() => { const el = document.querySelector('[data-testid=assistant]') || document.querySelector('.assistant'); return el ? el.innerText : ''; }", ct: ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    GeminiClientLog.LogFailedToEvaluateGemini(_logger, ex);
                }

                if (!string.IsNullOrEmpty(content) && content.Length > last.Length)
                {
                    string delta = content.Substring(last.Length);
                    last = content;
                    yield return new InferenceChunk { Delta = delta };
                }

                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { _logger.LogError(ex, "Failed to dispose page"); }
        }
    }
}

internal static partial class GeminiClientLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to evaluate gemini page")]
    public static partial void LogFailedToEvaluateGemini(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigating to {Url}")]
    public static partial void NavigatingTo(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Response element not found")]
    public static partial void ResponseNotFound(ILogger logger);
}
