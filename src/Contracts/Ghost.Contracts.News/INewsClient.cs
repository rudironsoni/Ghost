using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Contracts.News;

/// <summary>
/// Abstraction for news providers.
/// </summary>
public interface INewsClient
{
    /// <summary>
    /// Platform or provider name.
    /// </summary>
    public string PlatformName { get; }

    /// <summary>
    /// Gets articles matching a filter.
    /// </summary>
    public Task<IReadOnlyList<NewsArticle>> GetArticlesAsync(NewsFilter? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a single article by id.
    /// </summary>
    public Task<NewsArticle> GetArticleAsync(string articleId, CancellationToken ct = default);

    /// <summary>
    /// Searches articles by query.
    /// </summary>
    public Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, NewsSearchOptions? options = null, CancellationToken ct = default);
}
