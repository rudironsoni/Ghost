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
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in StreamAsync(request, ct)) sb.Append(chunk.Delta);
        return new Ghost.Contracts.Inference.InferenceResponse
        {
            Model = request.Model ?? _options.DefaultModel,
            Content = sb.ToString()
        };
    }

    public async IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync(_options.BaseUrl, ct: ct);
            try { await page.WaitForSelectorAsync("textarea", ct: ct); } catch { }

            var prompt = string.Join("\n", request.Messages.Select(m => m.Content));
            await page.TypeAsync("textarea", prompt, ct: ct);
            await page.PressAsync("textarea", "Enter", ct);

            var last = string.Empty;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!ct.IsCancellationRequested && sw.Elapsed < _options.ResponseTimeout)
            {
                string content = string.Empty;
                try
                {
                    content = await page.EvaluateAsync<string>("() => { const el = document.querySelector('[data-testid=assistant-response]') || document.querySelector('.assistant'); return el ? el.innerText : ''; }", ct: ct);
                }
                catch (Exception ex)
                {
                    OpenAILog.LogFailedToEvaluateOpenAI(_logger, ex);
                }

                if (!string.IsNullOrEmpty(content) && content.Length > last.Length)
                {
                    var delta = content.Substring(last.Length);
                    last = content;
                    yield return new InferenceChunk { Delta = delta };
                }

                await Task.Delay(200, ct);
            }
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
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
