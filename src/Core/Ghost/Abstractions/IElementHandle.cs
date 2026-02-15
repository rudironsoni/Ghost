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
}
