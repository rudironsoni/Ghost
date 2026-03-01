using Ghost.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Anthropic;

/// <summary>
/// Browser-driven inference client that automates claude.ai via the Ghost browser kernel.
/// </summary>
public sealed partial class AnthropicClient : Ghost.Contracts.Inference.IInferenceClient
{
    private readonly Ghost.IBrowserSession _session;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicClient> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AnthropicClient"/>.
    /// </summary>
    public AnthropicClient(Ghost.IBrowserSession session, IOptions<AnthropicOptions> options, ILogger<AnthropicClient> logger)
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
        await foreach (InferenceChunk chunk in StreamAsync(request, ct).ConfigureAwait(false))
        {
            // accumulate
        }

        // As a simple implementation, replay stream and build final message
        var buffer = new System.Text.StringBuilder();
        await foreach (InferenceChunk c in StreamAsync(request, ct).ConfigureAwait(false))
        {
            buffer.Append(c.Delta);
        }

        // Contracts expect InferenceResponse with Content property
        return new Ghost.Contracts.Inference.InferenceResponse
        {
            Model = request.Model ?? _options.DefaultModel,
            Content = buffer.ToString()
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string model = string.IsNullOrWhiteSpace(request.Model) ? _options.DefaultModel : request.Model;

        using var pageCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pageCt.CancelAfter(_options.ResponseTimeout);

        IPage page = await _session.NewPageAsync(ct: ct).ConfigureAwait(false);
        try
        {
            AnthropicLog.NavigatingTo(_logger, _options.BaseUrl);
            await page.NavigateAsync(_options.BaseUrl, ct: ct).ConfigureAwait(false);

            // Very small, robust automation flow using simple selectors.
            // Wait for prompt box
            try
            {
                await page.WaitForSelectorAsync("textarea", options: null, ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AnthropicLog.PromptTextboxNotFound(_logger, ex);
            }

            // Type prompt
            string prompt = string.Join("\n", request.Messages.Select(m => m.Content));
            if (string.IsNullOrWhiteSpace(prompt)) prompt = "";

            await page.TypeAsync("textarea", prompt, ct: ct).ConfigureAwait(false);
            await page.PressAsync("textarea", "Enter", ct).ConfigureAwait(false);

            // Poll for partial response. This is intentionally conservative and robust.
            string last = string.Empty;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!ct.IsCancellationRequested && stopwatch.Elapsed < _options.ResponseTimeout)
            {
                // attempt to read a standard response container
                string content = string.Empty;
                try
                {
                    content = await page.EvaluateAsync<string>("() => { const el = document.querySelector('[data-testid=assistant-message]') || document.querySelector('.assistant'); return el ? el.innerText : ''; }", ct: ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AnthropicLog.LogFailedToEvaluateAnthropic(_logger, ex);
                }

                if (!string.IsNullOrEmpty(content) && content.Length > last.Length)
                {
                    string delta = content.Substring(last.Length);
                    last = content;
                    yield return new InferenceChunk { Delta = delta };
                }

                await Task.Delay(200, ct).ConfigureAwait(false);
            }

            yield break;
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose page: {ex.Message}"); }
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
