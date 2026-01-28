using Ghost.Contracts.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Social client for LinkedIn interactions.
/// </summary>
    public sealed class LinkedInSocialClient : ISocialClient
    {
        private static readonly char[] _digitChars = "0123456789".ToCharArray();
    private readonly Ghost.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInSocialClient> _logger;
    private readonly Internal.LinkedInAuthenticator _authenticator;

    public LinkedInSocialClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInSocialClient> logger, Internal.LinkedInAuthenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInSocialClient>.Instance;
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }

    // Back-compat constructor used by existing tests/consumers that don't provide an authenticator
    public LinkedInSocialClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInSocialClient> logger)
        : this(session, options, logger, new Internal.LinkedInAuthenticator(session, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<Internal.LinkedInAuthenticator>.Instance))
    {
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

            // Ensure we're authenticated / logged in for richer scraping
            try
            {
                var logged = await _authenticator.IsLoggedInAsync(page, ct).ConfigureAwait(false);
                if (!logged)
                {
                    _logger.LogWarning("LinkedInSocialClient.GetProfileAsync: not logged in - scraping may be limited");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to verify LinkedIn login state");
            }

            var name = await page.EvaluateAsync<string>("() => document.querySelector('.text-heading-xlarge')?.innerText || ''", ct: ct);
            var bio = await page.EvaluateAsync<string>("() => document.querySelector('.text-body-medium')?.innerText || ''", ct: ct);
            var about = await page.EvaluateAsync<string>("() => document.querySelector('.pv-about__summary-text')?.innerText || ''", ct: ct);

            var profile = new SocialProfile { Id = profileId, Name = name ?? string.Empty, Bio = string.IsNullOrWhiteSpace(about) ? bio : about };

            // Parse more advanced sections
            try
            {
                var experiences = await ParseExperienceAsync(page, ct).ConfigureAwait(false);
                if (experiences?.Count > 0)
                {
                    profile.Experience.AddRange(experiences);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse experience section");
            }

            try
            {
                var education = await ParseEducationAsync(page, ct).ConfigureAwait(false);
                if (education?.Count > 0)
                {
                    profile.Education.AddRange(education);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse education section");
            }

            return profile;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    private async Task<List<SocialExperience>> ParseExperienceAsync(Ghost.IPage page, CancellationToken ct)
    {
        var list = new List<SocialExperience>();
        if (page == null) return list;

        var sections = await page.QuerySelectorAllAsync("section", ct: ct).ConfigureAwait(false);
        Ghost.IElement? expSection = null;
        foreach (var sec in sections)
        {
            try
            {
                var h2 = await sec.QuerySelectorAsync("h2", ct).ConfigureAwait(false);
                if (h2 == null) continue;
                var txt = await h2.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                if (txt.Contains("Experience", StringComparison.OrdinalIgnoreCase))
                {
                    expSection = sec;
                    break;
                }
            }
            catch { }
        }

        if (expSection == null) return list;

        var items = await expSection.QuerySelectorAllAsync("ul > li", ct).ConfigureAwait(false);
        foreach (var item in items)
        {
            try
            {
                var texts = await Internal.TextExtractor.ExtractAllUniqueTextsAsync(item, ct).ConfigureAwait(false);
                if (texts == null || texts.Count == 0) continue;

                var exp = new SocialExperience();
                if (texts.Count >= 1) exp = exp with { Title = texts[0] };
                if (texts.Count >= 2) exp = exp with { Company = texts[1] };

                string dateString = string.Empty;
                if (texts.Count >= 3) dateString = texts[2];
                else
                {
                    // attempt to find a text that looks like a date range
                    var maybe = texts.FirstOrDefault(t => t.IndexOfAny(_digitChars) >= 0);
                    if (!string.IsNullOrWhiteSpace(maybe)) dateString = maybe;
                }

                if (!string.IsNullOrWhiteSpace(dateString))
                {
                    // dateString may contain duration after a middle dot
                    var parts = dateString.Split('·');
                    var range = parts.Length > 0 ? parts[0].Trim() : dateString.Trim();
                    var (s, e) = Internal.DateParser.Parse(range);
                    exp = exp with { StartDate = s, EndDate = e, IsCurrent = e == null };
                    if (parts.Length > 1)
                    {
                        exp = exp with { Duration = parts[1].Trim() };
                    }
                }

                // location might be present as a subsequent text
                if (texts.Count >= 4)
                {
                    exp = exp with { Location = texts[3] };
                }

                list.Add(exp);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse an experience item");
            }
        }

        return list;
    }

    private async Task<List<SocialEducation>> ParseEducationAsync(Ghost.IPage page, CancellationToken ct)
    {
        var list = new List<SocialEducation>();
        if (page == null) return list;

        var sections = await page.QuerySelectorAllAsync("section", ct: ct).ConfigureAwait(false);
        Ghost.IElement? edSection = null;
        foreach (var sec in sections)
        {
            try
            {
                var h2 = await sec.QuerySelectorAsync("h2", ct).ConfigureAwait(false);
                if (h2 == null) continue;
                var txt = await h2.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                if (txt.Contains("Education", StringComparison.OrdinalIgnoreCase))
                {
                    edSection = sec;
                    break;
                }
            }
            catch { }
        }

        if (edSection == null) return list;

        var items = await edSection.QuerySelectorAllAsync("ul > li", ct).ConfigureAwait(false);
        foreach (var item in items)
        {
            try
            {
                var texts = await Internal.TextExtractor.ExtractAllUniqueTextsAsync(item, ct).ConfigureAwait(false);
                if (texts == null || texts.Count == 0) continue;

                var edu = new SocialEducation();
                // heuristics: [0]=school, [1]=degree/field, [last]=dates
                if (texts.Count >= 1) edu = edu with { School = texts[0] };
                if (texts.Count >= 2) edu = edu with { Degree = texts[1] };

                string dateString = string.Empty;
                if (texts.Count >= 3) dateString = texts.Last();
                else
                {
                    var maybe = texts.FirstOrDefault(t => t.IndexOfAny(_digitChars) >= 0);
                    if (!string.IsNullOrWhiteSpace(maybe)) dateString = maybe;
                }

                if (!string.IsNullOrWhiteSpace(dateString))
                {
                    var (s, e) = Internal.DateParser.Parse(dateString);
                    edu = edu with { StartDate = s, EndDate = e };
                }

                list.Add(edu);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse an education item");
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<SocialProfile>> SearchProfilesAsync(Ghost.Contracts.Social.ProfileSearchCriteria criteria, CancellationToken ct = default)
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

    public async Task<SocialPost> CreatePostAsync(Ghost.Contracts.Social.CreatePostRequest request, CancellationToken ct = default)
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

    public async Task<IReadOnlyList<SocialPost>> GetFeedAsync(Ghost.Contracts.Social.FeedOptions? options = null, CancellationToken ct = default)
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

    public async Task<IReadOnlyList<Ghost.Contracts.Social.SocialConnection>> GetConnectionsAsync(Ghost.Contracts.Social.ConnectionsOptions? options = null, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/mynetwork/invite-connect/connections/", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var nodes = await page.QuerySelectorAllAsync(".mn-connection-card__details", ct: ct);
            var list = new List<Ghost.Contracts.Social.SocialConnection>();
            foreach (var n in nodes.Take(options?.MaxResults ?? 20))
            {
                try
                {
                    var aEl = await n.QuerySelectorAsync("a", ct);
                    var id = aEl is not null ? await aEl.GetAttributeAsync("href", ct) ?? string.Empty : string.Empty;
                    var nameEl = await n.QuerySelectorAsync(".mn-connection-card__name", ct);
                    var name = nameEl is not null ? await nameEl.GetTextContentAsync(ct) ?? string.Empty : string.Empty;
                    list.Add(new Ghost.Contracts.Social.SocialConnection { Id = id, FromProfileId = string.Empty, ToProfileId = string.Empty });
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
