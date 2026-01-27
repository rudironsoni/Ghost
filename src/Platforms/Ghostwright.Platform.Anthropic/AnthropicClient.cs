using Ghostwright.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghostwright.Platform.Anthropic;

/// <summary>
/// Browser-driven inference client that automates claude.ai via the Ghostwright browser kernel.
/// </summary>
public sealed partial class AnthropicClient : Ghostwright.Contracts.Inference.IInferenceClient
{
    private readonly Ghostwright.IBrowserSession _session;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicClient> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AnthropicClient"/>.
    /// </summary>
    public AnthropicClient(Ghostwright.IBrowserSession session, IOptions<AnthropicOptions> options, ILogger<AnthropicClient> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new AnthropicOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AnthropicClient>.Instance;
    }

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default)
    {
        await foreach (var chunk in StreamAsync(request, ct))
        {
            // accumulate
        }

        // As a simple implementation, replay stream and build final message
        var buffer = new System.Text.StringBuilder();
        await foreach (var c in StreamAsync(request, ct))
        {
            buffer.Append(c.Delta);
        }

        // Contracts expect InferenceResponse with Content property
        return new Ghostwright.Contracts.Inference.InferenceResponse
        {
            Model = request.Model ?? _options.DefaultModel,
            Content = buffer.ToString()
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.DefaultModel : request.Model;

        using var pageCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pageCt.CancelAfter(_options.ResponseTimeout);

        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            AnthropicLog.NavigatingTo(_logger, _options.BaseUrl);
            await page.NavigateAsync(_options.BaseUrl, ct: ct);

            // Very small, robust automation flow using simple selectors.
            // Wait for prompt box
            try
            {
                await page.WaitForSelectorAsync("textarea", options: null, ct: ct);
            }
                catch (Exception ex)
                {
                    AnthropicLog.PromptTextboxNotFound(_logger, ex);
                }

            // Type prompt
            var prompt = string.Join("\n", request.Messages.Select(m => m.Content));
            if (string.IsNullOrWhiteSpace(prompt)) prompt = "";

            await page.TypeAsync("textarea", prompt, ct: ct);
            await page.PressAsync("textarea", "Enter", ct);

            // Poll for partial response. This is intentionally conservative and robust.
            var last = string.Empty;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!ct.IsCancellationRequested && sw.Elapsed < _options.ResponseTimeout)
            {
                // attempt to read a standard response container
                string content = string.Empty;
                try
                {
                    content = await page.EvaluateAsync<string>("() => { const el = document.querySelector('[data-testid=assistant-message]') || document.querySelector('.assistant'); return el ? el.innerText : ''; }", ct: ct);
                }
                catch (Exception ex)
                {
                    AnthropicLog.LogFailedToEvaluateAnthropic(_logger, ex);
                }

                if (!string.IsNullOrEmpty(content) && content.Length > last.Length)
                {
                    var delta = content.Substring(last.Length);
                    last = content;
                    yield return new InferenceChunk { Delta = delta };
                }

                await Task.Delay(200, ct);
            }

            yield break;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }
}

internal static partial class AnthropicLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to evaluate page for response text")]
    public static partial void LogFailedToEvaluateAnthropic(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigating to {Url}")]
    public static partial void NavigatingTo(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt textbox not found on Anthropic page")]
    public static partial void PromptTextboxNotFound(ILogger logger, Exception ex);
}
