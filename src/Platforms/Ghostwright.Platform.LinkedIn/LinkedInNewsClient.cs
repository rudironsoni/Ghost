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

            var nodes = await page.QuerySelectorAllAsync(".feed-shared-update-v2");
            var list = new List<NewsArticle>();
            foreach (var n in nodes.Take(filter?.Limit ?? 20))
            {
                try
                {
                    var title = await n.EvaluateAsync<string>("el => el.querySelector('h3')?.innerText || ''", ct: ct);
                    var url = await n.EvaluateAsync<string>("el => el.querySelector('a')?.getAttribute('href') || ''", ct: ct);
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
        return GetArticlesAsync(new Ghostwright.Contracts.News.NewsFilter { Limit = options?.Limit ?? 20 }, ct: ct);
    }
}
