using Ghostwright.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghostwright.Platform.Google;

/// <summary>
/// Browser-driven client for gemini.google.com using the Ghostwright kernel.
/// </summary>
    public sealed partial class GoogleClient : Ghostwright.Contracts.Inference.IInferenceClient
    {
    private readonly Ghostwright.IBrowserSession _session;
    private readonly GoogleOptions _options;
    private readonly ILogger<GoogleClient> _logger;

    public GoogleClient(Ghostwright.IBrowserSession session, IOptions<GoogleOptions> options, ILogger<GoogleClient> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _options = options?.Value ?? new GoogleOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleClient>.Instance;
    }

    public string ProviderName => "Google";

    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in StreamAsync(request, ct)) sb.Append(chunk.Delta);
        return new Ghostwright.Contracts.Inference.InferenceResponse
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
                    content = await page.EvaluateAsync<string>("() => { const el = document.querySelector('[data-testid=assistant]') || document.querySelector('.assistant'); return el ? el.innerText : ''; }", ct: ct);
                }
                catch (Exception ex)
                {
                    GoogleClientLog.LogFailedToEvaluateGoogle(_logger, ex);
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

internal static partial class GoogleClientLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to evaluate gemini page")]
    public static partial void LogFailedToEvaluateGoogle(ILogger logger, Exception ex);
}
