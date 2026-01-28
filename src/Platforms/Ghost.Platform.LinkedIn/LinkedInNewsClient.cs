using Ghost.Contracts.News;
using Ghost.Extensions;
using Ghost.Platform.LinkedIn.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// News client for LinkedIn (scrapes articles from posts).
/// </summary>
public sealed class LinkedInNewsClient : INewsClient
{
    private readonly Ghost.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInNewsClient> _logger;

    public LinkedInNewsClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInNewsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInNewsClient>.Instance;
    }

    public string PlatformName => "LinkedIn";

    public async Task<IReadOnlyList<NewsArticle>> GetArticlesAsync(NewsFilter? filter = null, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var nodes = await page.QuerySelectorAllAsync(".feed-shared-update-v2", ct: ct);
            var list = new List<NewsArticle>();
            foreach (var n in nodes.Take(filter?.MaxResults ?? 20))
            {
                try
                {
                    var titleEl = await n.QuerySelectorAsync("h3", ct);
                    string title;
                    if (titleEl is not null)
                        title = await titleEl.GetTextContentAsync(ct) ?? string.Empty;
                    else
                        title = string.Empty;

                    var aEl = await n.QuerySelectorAsync("a", ct);
                    string url;
                    if (aEl is not null)
                        url = await aEl.GetAttributeAsync("href", ct) ?? string.Empty;
                    else
                        url = string.Empty;
                    list.Add(new NewsArticle { Id = Guid.NewGuid().ToString(), Title = title, Url = url });
                }
                catch { }
            }

            return list;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public Task<NewsArticle> GetArticleAsync(string articleId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, Ghost.Contracts.News.NewsSearchOptions? options = null, CancellationToken ct = default)
    {
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);
        try
        {
            var q = System.Uri.EscapeDataString(query);
            await page.NavigateAsync($"{_options.BaseUrl}/search/results/content/?keywords={q}", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // Search results for content usually appear as update cards
            // Selectors: .search-results-container .search-update-card, or reusing feed classes
            var nodes = await page.QuerySelectorAllAsync(".search-update-card, .feed-shared-update-v2", ct: ct);
            var list = new List<NewsArticle>();
            foreach (var n in nodes.Take(options?.MaxResults ?? 20))
            {
                try
                {
                    // Title usually in the update text or a shared article title
                    // .update-components-text is common for the post text
                    // article titles often in .update-components-article__title
                    var titleEl = await n.QuerySelectorAsync(".update-components-article__title, .update-components-text span[dir='ltr']", ct);
                    string title = titleEl is not null 
                        ? await titleEl.GetTextContentAsync(ct) ?? string.Empty 
                        : string.Empty;

                    // Link
                    var aEl = await n.QuerySelectorAsync("a.app-aware-link", ct);
                    string url = aEl is not null 
                        ? await aEl.GetAttributeAsync("href", ct) ?? string.Empty 
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(url))
                    {
                        list.Add(new NewsArticle { Id = Guid.NewGuid().ToString(), Title = title.Trim(), Url = url });
                    }
                }
                catch { }
            }

            return list;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }
}
