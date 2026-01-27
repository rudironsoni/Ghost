using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghostwright.Contracts.News;

/// <summary>
/// Abstraction for news providers.
/// </summary>
public interface INewsClient
{
    /// <summary>
    /// Platform or provider name.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets articles matching a filter.
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> GetArticlesAsync(NewsFilter? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a single article by id.
    /// </summary>
    Task<NewsArticle> GetArticleAsync(string articleId, CancellationToken ct = default);

    /// <summary>
    /// Searches articles by query.
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, NewsSearchOptions? options = null, CancellationToken ct = default);
}
