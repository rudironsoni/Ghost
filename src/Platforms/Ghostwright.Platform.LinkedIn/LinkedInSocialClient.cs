using Ghostwright.Contracts.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghostwright.Platform.LinkedIn;

/// <summary>
/// Social client for LinkedIn interactions.
/// </summary>
public sealed class LinkedInSocialClient : ISocialClient
{
    private readonly Ghostwright.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInSocialClient> _logger;

    public LinkedInSocialClient(Ghostwright.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInSocialClient> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInSocialClient>.Instance;
    }

    public string PlatformName => "LinkedIn";

    public async Task<SocialProfile> GetProfileAsync(string profileId, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            var url = $"{_options.BaseUrl}/in/{profileId}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var name = await page.EvaluateAsync<string>("() => document.querySelector('.text-heading-xlarge')?.innerText || ''", ct: ct);
            var bio = await page.EvaluateAsync<string>("() => document.querySelector('.text-body-medium')?.innerText || ''", ct: ct);
            var about = await page.EvaluateAsync<string>("() => document.querySelector('.pv-about__summary-text')?.innerText || ''", ct: ct);

            return new SocialProfile { Id = profileId, Name = name ?? string.Empty, Bio = string.IsNullOrWhiteSpace(about) ? bio : about };
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialProfile>> SearchProfilesAsync(Ghostwright.Contracts.Social.ProfileSearchCriteria criteria, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            var q = System.Uri.EscapeDataString(criteria.Query ?? string.Empty);
            var url = $"{_options.BaseUrl}/search/results/people/?keywords={q}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // Very simple parsing: find profile links
            var nodes = await page.QuerySelectorAllAsync(".reusable-search__result-container a.app-aware-link", ct: ct);
            var list = new List<SocialProfile>();
            foreach (var n in nodes.Take(criteria.MaxResults))
            {
                try
                {
                    var href = await n.GetAttributeAsync("href", ct);
                    if (href is null) continue;
                    var id = href.Split('/').LastOrDefault() ?? href;
                    var name = await n.GetTextContentAsync(ct) ?? string.Empty;
                    list.Add(new SocialProfile { Id = id, Name = name });
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseSearchNode(_logger, ex);
                }
            }

            return list;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<SocialPost> CreatePostAsync(Ghostwright.Contracts.Social.CreatePostRequest request, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct);
            await page.WaitForSelectorAsync("button[data-control-name='sharebox-trigger']", ct: ct);
            await page.ClickAsync("button[data-control-name='sharebox-trigger']", ct: ct);
            await page.TypeAsync("div.ql-editor", request.Content, ct: ct);
            await page.ClickAsync("button[data-control-name='submit_post']", ct: ct);
            await page.WaitForNavigationAsync(ct: ct);

            return new SocialPost { Id = Guid.NewGuid().ToString(), Content = request.Content };
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialPost>> GetFeedAsync(Ghostwright.Contracts.Social.FeedOptions? options = null, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var nodes = await page.QuerySelectorAllAsync(".feed-shared-update-v2", ct: ct);
            var list = new List<SocialPost>();
            foreach (var n in nodes.Take(options?.PageSize ?? 20))
            {
                try
                {
                    var content = await n.GetTextContentAsync(ct) ?? string.Empty;
                    list.Add(new SocialPost { Id = Guid.NewGuid().ToString(), Content = content });
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

    public async Task SendMessageAsync(string recipientId, string message, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/messaging/thread/{recipientId}", ct: ct);
            await page.WaitForSelectorAsync("div.msg-form__contenteditable", ct: ct);
            await page.TypeAsync("div.msg-form__contenteditable", message, ct: ct);
            await page.PressAsync("div.msg-form__contenteditable", "Enter", ct);
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<IReadOnlyList<Ghostwright.Contracts.Social.SocialConnection>> GetConnectionsAsync(Ghostwright.Contracts.Social.ConnectionsOptions? options = null, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/mynetwork/invite-connect/connections/", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var nodes = await page.QuerySelectorAllAsync(".mn-connection-card__details", ct: ct);
            var list = new List<Ghostwright.Contracts.Social.SocialConnection>();
            foreach (var n in nodes.Take(options?.MaxResults ?? 20))
            {
                try
                {
                    var aEl = await n.QuerySelectorAsync("a", ct);
                    var id = aEl is not null ? await aEl.GetAttributeAsync("href", ct) ?? string.Empty : string.Empty;
                    var nameEl = await n.QuerySelectorAsync(".mn-connection-card__name", ct);
                    var name = nameEl is not null ? await nameEl.GetTextContentAsync(ct) ?? string.Empty : string.Empty;
                    list.Add(new Ghostwright.Contracts.Social.SocialConnection { Id = id, FromProfileId = string.Empty, ToProfileId = string.Empty });
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

    public async Task SendConnectionRequestAsync(string profileId, string? message = null, CancellationToken ct = default)
    {
            var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/in/{profileId}", ct: ct);
            await page.WaitForSelectorAsync("button[data-control-name='connect']", ct: ct);
            await page.ClickAsync("button[data-control-name='connect']", ct: ct);
            if (!string.IsNullOrEmpty(message))
            {
                await page.TypeAsync("textarea[name='message']", message, ct: ct);
            }
            await page.ClickAsync("button[data-control-name='send_invite']", ct: ct);
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }
}
