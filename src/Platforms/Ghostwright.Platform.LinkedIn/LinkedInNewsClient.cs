using Ghostwright.Contracts.News;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghostwright.Platform.LinkedIn;

/// <summary>
/// News client for LinkedIn (scrapes articles from posts).
/// </summary>
public sealed class LinkedInNewsClient : INewsClient
{
    private readonly Ghostwright.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInNewsClient> _logger;

    public LinkedInNewsClient(Ghostwright.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInNewsClient> logger)
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

    public Task<IReadOnlyList<NewsArticle>> SearchAsync(string query, Ghostwright.Contracts.News.NewsSearchOptions? options = null, CancellationToken ct = default)
    {
        // LinkedIn does not provide open article search; reuse feed scraping as fallback.
        return GetArticlesAsync(new Ghostwright.Contracts.News.NewsFilter { MaxResults = options?.MaxResults ?? 20 }, ct: ct);
    }
}
