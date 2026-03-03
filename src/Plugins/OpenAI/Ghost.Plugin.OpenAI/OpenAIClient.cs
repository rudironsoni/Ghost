using Ghost.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.OpenAI;

/// <summary>
/// Browser-driven client that automates chatgpt.com via the Ghost browser kernel.
/// </summary>
public sealed partial class OpenAIClient : Ghost.Contracts.Inference.IInferenceClient
{
    private readonly Ghost.IBrowserSession _session;
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIClient> _logger;

    public OpenAIClient(Ghost.IBrowserSession session, IOptions<OpenAIOptions> options, ILogger<OpenAIClient> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new OpenAIOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAIClient>.Instance;
    }

    public string ProviderName => "OpenAI";

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
            try { await page.WaitForSelectorAsync("textarea", ct: ct).ConfigureAwait(false); } catch (Exception ex) { _logger.LogError(ex, "Error"); }

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
                    content = await page.EvaluateAsync<string>("() => { const el = document.querySelector('[data-testid=assistant-response]') || document.querySelector('.assistant'); return el ? el.innerText : ''; }", ct: ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OpenAILog.LogFailedToEvaluateOpenAI(_logger, ex);
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
            try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { _logger.LogError(ex, "Error"); }
        }
    }
}

internal static partial class OpenAILog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to evaluate chatgpt page")]
    public static partial void LogFailedToEvaluateOpenAI(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigating to {Url}")]
    public static partial void NavigatingTo(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Response element not found")]
    public static partial void ResponseNotFound(ILogger logger);
}
