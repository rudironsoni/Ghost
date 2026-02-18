using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost;

// Minimal element handle abstraction used by platform internals when interacting
// directly with low-level page element handles.
public interface IElementHandle : IAsyncDisposable
{
    public Task<IElementHandle?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    public Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
    public Task<string?> TextContentAsync(CancellationToken ct = default);

    /// <summary>
    /// Evaluates a JavaScript function on the element handle.
    /// </summary>
    /// <typeparam name="T">The return type of the evaluation.</typeparam>
    /// <param name="expression">The JavaScript expression to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the evaluation.</returns>
    public Task<T> EvaluateAsync<T>(string expression, CancellationToken ct = default);
}
