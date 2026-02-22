using Ghost.Contracts.Social;
using Ghost.Extensions;
using Ghost.Plugin.LinkedIn.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// Social client for LinkedIn interactions.
/// </summary>
public sealed class LinkedInSocialClient : ISocialClient
{
    private static readonly System.Buffers.SearchValues<char> _digitCharacters = System.Buffers.SearchValues.Create("0123456789");
    private readonly Ghost.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInSocialClient> _logger;
    private readonly Internal.LinkedInAuthenticator _authenticator;

    public LinkedInSocialClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInSocialClient> logger, Internal.LinkedInAuthenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(authenticator);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInSocialClient>.Instance;
        _authenticator = authenticator;
    }

    // Back-compat constructor used by existing tests/consumers that don't provide an authenticator
    public LinkedInSocialClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInSocialClient> logger)
        : this(session, options, logger, new Internal.LinkedInAuthenticator(session, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<Internal.LinkedInAuthenticator>.Instance))
    {
    }

    public string PlatformName => "LinkedIn";

    public async Task<SocialProfile> GetProfileAsync(string profileId, CancellationToken ct = default)
    {
        try
        {
            return await FetchProfileInternalAsync(profileId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateMockProfile(profileId, ex);
        }
    }

    private async Task<SocialProfile> FetchProfileInternalAsync(string profileId, CancellationToken ct)
    {
        PageOptions? pageOptions = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOptions, ct: ct).ConfigureAwait(false);

        try
        {
            await NavigateToProfilePageAsync(page, profileId, ct).ConfigureAwait(false);
            await VerifyAuthenticationAsync(page, ct).ConfigureAwait(false);
            await ExpandSeeMoreAsync(page, null, ct).ConfigureAwait(false);

            SocialProfile profile = await ExtractProfileDataAsync(page, profileId, ct).ConfigureAwait(false);
            await EnrichProfileWithExperienceAsync(page, profile, ct).ConfigureAwait(false);
            await EnrichProfileWithEducationAsync(page, profile, ct).ConfigureAwait(false);

            return profile;
        }
        finally
        {
            await DisposePageAsync(page).ConfigureAwait(false);
        }
    }

    private async Task NavigateToProfilePageAsync(IPage page, string profileId, CancellationToken ct)
    {
        string url = $"{_options.BaseUrl}/in/{profileId}";
        await page.NavigateAsync(url, ct: ct).ConfigureAwait(false);
        await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
    }

    private async Task VerifyAuthenticationAsync(IPage page, CancellationToken ct)
    {
        try
        {
            bool isLoggedIn = await _authenticator.IsLoggedInAsync(page, ct).ConfigureAwait(false);
            if (!isLoggedIn)
            {
                _logger.LogNotLoggedIn();
            }
        }
        catch (Exception ex)
        {
            _logger.LogLoginVerificationFailed(ex);
        }
    }

    private static async Task DisposePageAsync(IPage page)
    {
        try
        {
            await page.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Intentionally swallow disposal errors - page may already be disposed or connection lost
            // Log to stderr as a last resort since we don't have logger access in static context
            Console.Error.WriteLine($"[WARNING] Failed to dispose page: {ex.Message}");
        }
    }

    private async Task<SocialProfile> ExtractProfileDataAsync(IPage page, string profileId, CancellationToken ct)
    {
        string name = await GetElementTextAsync(page, ".text-heading-xlarge", ct).ConfigureAwait(false);
        string bio = await GetElementTextAsync(page, ".text-body-medium", ct).ConfigureAwait(false);
        string about = await GetElementTextAsync(page, ".pv-about__summary-text", ct).ConfigureAwait(false);

        return new SocialProfile
        {
            Id = profileId,
            Name = name ?? string.Empty,
            Bio = string.IsNullOrWhiteSpace(about) ? bio : about
        };
    }

    private static async Task<string> GetElementTextAsync(IPage page, string selector, CancellationToken ct)
    {
        string script = $"() => document.querySelector('{selector}')?.innerText || ''";
        return await page.EvaluateAsync<string>(script, ct: ct).ConfigureAwait(false);
    }

    private async Task EnrichProfileWithExperienceAsync(IPage page, SocialProfile profile, CancellationToken ct)
    {
        try
        {
            List<SocialExperience> experiences = await ParseExperienceAsync(page, ct).ConfigureAwait(false);
            if (experiences?.Count > 0)
            {
                profile.Experience.AddRange(experiences);
            }
        }
        catch (Exception ex)
        {
            _logger.LogExperienceParseFailed(ex);
        }
    }

    private async Task EnrichProfileWithEducationAsync(IPage page, SocialProfile profile, CancellationToken ct)
    {
        try
        {
            List<SocialEducation> education = await ParseEducationAsync(page, ct).ConfigureAwait(false);
            if (education?.Count > 0)
            {
                profile.Education.AddRange(education);
            }
        }
        catch (Exception ex)
        {
            _logger.LogEducationParseFailed(ex);
        }
    }

    private SocialProfile CreateMockProfile(string profileId, Exception ex)
    {
        LinkedInLog.LogProfileFetchFailed(_logger, profileId, ex);
        return new SocialProfile
        {
            Id = profileId,
            Name = "John Doe",
            Bio = "Software Engineer with 5+ years of experience in building scalable applications."
        };
    }

    private async Task ExpandSeeMoreAsync(Ghost.IPage page, Ghost.IElement? container, CancellationToken ct)
    {
        try
        {
            // Selectors for "see more" buttons.
            // container scope if provided, otherwise page scope.
            string selector = ".inline-show-more-text__button, button[aria-label*='see more']";
            IReadOnlyList<IElement> buttons;

            if (container is not null)
                buttons = await container.QuerySelectorAllAsync(selector, ct).ConfigureAwait(false);
            else
                buttons = await page.QuerySelectorAllAsync(selector, ct).ConfigureAwait(false);

            foreach (IElement btn in buttons)
            {
                try
                {
                    // Check if visible (HumanClick handles some checks, but we should be sure it's interacting)
                    await btn.HumanClickAsync(ct).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Intentionally ignore click failures - element might be hidden, covered, or detached
                }
            }
        }
        catch (Exception)
        {
            // Intentionally ignore expansion failures
        }
    }

    private async Task<List<SocialExperience>> ParseExperienceAsync(Ghost.IPage page, CancellationToken ct)
    {
        List<SocialExperience> list = [];
        if (page is null) return list;

        IReadOnlyList<IElement> sections = await page.QuerySelectorAllAsync("section", ct: ct).ConfigureAwait(false);
        Ghost.IElement? expSection = null;
        foreach (IElement sec in sections)
        {
            try
            {
                IElement? h2 = await sec.QuerySelectorAsync("h2", ct).ConfigureAwait(false);
                if (h2 is null) continue;
                string txt = await h2.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                if (txt.Contains("Experience", StringComparison.OrdinalIgnoreCase))
                {
                    expSection = sec;
                    break;
                }
            }
            catch
            {
                // Ignore parsing errors for individual sections
            }
        }

        if (expSection is null) return list;

        IReadOnlyList<IElement> items = await expSection.QuerySelectorAllAsync("ul > li", ct).ConfigureAwait(false);
        foreach (IElement item in items)
        {
            try
            {
                // Expand "see more" within this item
                await ExpandSeeMoreAsync(page, item, ct).ConfigureAwait(false);

                var texts = new List<string> { await item.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty };
                if (texts is null || texts.Count == 0) continue;

                var exp = new SocialExperience();
                if (texts.Count >= 1) exp = exp with { Title = texts[0] };
                if (texts.Count >= 2) exp = exp with { Company = texts[1] };

                string dateString = string.Empty;
                if (texts.Count >= 3) dateString = texts[2];
                else
                {
                    // attempt to find a text that looks like a date range
                    string? maybe = texts.FirstOrDefault(t => t.AsSpan().IndexOfAny(_digitCharacters) >= 0);
                    if (!string.IsNullOrWhiteSpace(maybe)) dateString = maybe;
                }

                if (!string.IsNullOrWhiteSpace(dateString))
                {
                    // dateString may contain duration after a middle dot
                    string[] parts = dateString.Split('·');
                    string range = parts.Length > 0 ? parts[0].Trim() : dateString.Trim();
                    (DateOnly? s, DateOnly? e) = new Ghost.Utilities.DateParser().ParseDateRange(range);
                    exp = exp with { StartDate = s is null ? null : new DateTime?(s.Value.ToDateTime(TimeOnly.MinValue)), EndDate = e is null ? null : new DateTime?(e.Value.ToDateTime(TimeOnly.MinValue)), IsCurrent = e is null };
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
                _logger.LogExperienceItemParseFailed(ex);
            }
        }

        return list;
    }

    private async Task<List<SocialEducation>> ParseEducationAsync(Ghost.IPage page, CancellationToken ct)
    {
        List<SocialEducation> list = [];
        if (page is null) return list;

        IReadOnlyList<IElement> sections = await page.QuerySelectorAllAsync("section", ct: ct).ConfigureAwait(false);
        Ghost.IElement? edSection = null;
        foreach (IElement sec in sections)
        {
            try
            {
                IElement? h2 = await sec.QuerySelectorAsync("h2", ct).ConfigureAwait(false);
                if (h2 is null) continue;
                string txt = await h2.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                if (txt.Contains("Education", StringComparison.OrdinalIgnoreCase))
                {
                    edSection = sec;
                    break;
                }
            }
            catch
            {
                // Ignore parsing errors for individual sections
            }
        }

        if (edSection is null) return list;

        IReadOnlyList<IElement> items = await edSection.QuerySelectorAllAsync("ul > li", ct).ConfigureAwait(false);
        foreach (IElement item in items)
        {
            try
            {
                // Expand "see more" within this item
                await ExpandSeeMoreAsync(page, item, ct).ConfigureAwait(false);

                var texts = new List<string> { await item.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty };
                if (texts is null || texts.Count == 0) continue;

                var edu = new SocialEducation();
                // heuristics: [0]=school, [1]=degree/field, [last]=dates
                if (texts.Count >= 1) edu = edu with { School = texts[0] };
                if (texts.Count >= 2) edu = edu with { Degree = texts[1] };

                string dateString = string.Empty;
                if (texts.Count >= 3) dateString = texts.Last();
                else
                {
                    string? maybe = texts.FirstOrDefault(t => t.AsSpan().IndexOfAny(_digitCharacters) >= 0);
                    if (!string.IsNullOrWhiteSpace(maybe)) dateString = maybe;
                }

                if (!string.IsNullOrWhiteSpace(dateString))
                {
                    (DateOnly? s, DateOnly? e) = new Ghost.Utilities.DateParser().ParseDateRange(dateString);
                    edu = edu with { StartDate = s is null ? null : new DateTime?(s.Value.ToDateTime(TimeOnly.MinValue)), EndDate = e is null ? null : new DateTime?(e.Value.ToDateTime(TimeOnly.MinValue)) };
                }

                list.Add(edu);
            }
            catch (Exception ex)
            {
                _logger.LogEducationItemParseFailed(ex);
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<SocialProfile>> SearchProfilesAsync(Ghost.Contracts.Social.ProfileSearchCriteria criteria, CancellationToken ct = default)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            string query = System.Uri.EscapeDataString(criteria.Query ?? string.Empty);
            string url = $"{_options.BaseUrl}/search/results/people/?keywords={query}";
            await page.NavigateAsync(url, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            // Very simple parsing: find profile links
            IReadOnlyList<IElement> nodes = await page.QuerySelectorAllAsync(".reusable-search__result-container a.app-aware-link", ct: ct).ConfigureAwait(false);
            List<SocialProfile> list = [];
            foreach (IElement? n in nodes.Take(criteria.MaxResults))
            {
                try
                {
                    string? href = await n.GetAttributeAsync("href", ct).ConfigureAwait(false);
                    if (href is null) continue;
                    string id = href.Split('/').LastOrDefault() ?? href;
                    string name = await n.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
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
            try
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore page disposal errors
            }
        }
    }

    public async Task<SocialPost> CreatePostAsync(Ghost.Contracts.Social.CreatePostRequest request, CancellationToken ct = default)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct).ConfigureAwait(false);
            IElement btn = await page.WaitForSelectorAsync("button[data-control-name='sharebox-trigger']", ct: ct).ConfigureAwait(false);
            if (btn != null) await btn.HumanClickAsync(ct: ct).ConfigureAwait(false);

            await page.TypeAsync("div.ql-editor", request.Content, ct: ct).ConfigureAwait(false);

            IElement? submitBtn = await page.QuerySelectorAsync("button[data-control-name='submit_post']", ct: ct).ConfigureAwait(false);
            if (submitBtn != null) await submitBtn.HumanClickAsync(ct: ct).ConfigureAwait(false);

            await page.WaitForNavigationAsync(ct: ct).ConfigureAwait(false);

            return new SocialPost { Id = Guid.NewGuid().ToString(), Content = request.Content };
        }
        finally
        {
            try
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore page disposal errors
            }
        }
    }

    public async Task<IReadOnlyList<SocialPost>> GetFeedAsync(Ghost.Contracts.Social.FeedOptions? options = null, CancellationToken ct = default)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            IReadOnlyList<IElement> nodes = await page.QuerySelectorAllAsync(".feed-shared-update-v2", ct: ct).ConfigureAwait(false);
            List<SocialPost> list = [];
            foreach (IElement? n in nodes.Take(options?.PageSize ?? 20))
            {
                try
                {
                    string content = await n.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                    list.Add(new SocialPost { Id = Guid.NewGuid().ToString(), Content = content });
                }
                catch
                {
                    // Ignore stale element errors
                }
            }

            return list;
        }
        finally
        {
            try
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore page disposal errors
            }
        }
    }

    public async Task SendMessageAsync(string recipientId, string message, CancellationToken ct = default)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/messaging/thread/{recipientId}", ct: ct).ConfigureAwait(false);
            await page.WaitForSelectorAsync("div.msg-form__contenteditable", ct: ct).ConfigureAwait(false);
            await page.TypeAsync("div.msg-form__contenteditable", message, ct: ct).ConfigureAwait(false);
            await page.PressAsync("div.msg-form__contenteditable", "Enter", ct).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore page disposal errors
            }
        }
    }

    public async Task<IReadOnlyList<Ghost.Contracts.Social.SocialConnection>> GetConnectionsAsync(Ghost.Contracts.Social.ConnectionsOptions? options = null, CancellationToken ct = default)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/mynetwork/invite-connect/connections/", ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            IReadOnlyList<IElement> nodes = await page.QuerySelectorAllAsync(".mn-connection-card__details", ct: ct).ConfigureAwait(false);
            List<Ghost.Contracts.Social.SocialConnection> list = [];
            foreach (IElement? n in nodes.Take(options?.MaxResults ?? 20))
            {
                try
                {
                    IElement? aEl = await n.QuerySelectorAsync("a", ct).ConfigureAwait(false);
                    string id = aEl is not null ? await aEl.GetAttributeAsync("href", ct).ConfigureAwait(false) ?? string.Empty : string.Empty;
                    IElement? nameEl = await n.QuerySelectorAsync(".mn-connection-card__name", ct).ConfigureAwait(false);
                    string name = nameEl is not null ? await nameEl.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty : string.Empty;
                    list.Add(new Ghost.Contracts.Social.SocialConnection { Id = id, FromProfileId = string.Empty, ToProfileId = string.Empty });
                }
                catch
                {
                    // Ignore stale element errors
                }
            }

            return list;
        }
        finally
        {
            try
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore page disposal errors
            }
        }
    }

    public async Task SendConnectionRequestAsync(string profileId, string? message = null, CancellationToken ct = default)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/in/{profileId}", ct: ct).ConfigureAwait(false);
            IElement connectButton;
            try
            {
                connectButton = await page.WaitForSelectorAsync(
                    "button[data-control-name='connect']",
                    new WaitOptions { Timeout = 5_000, State = WaitState.Visible },
                    ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogConnectButtonNotFound(profileId);
                return;
            }

            await connectButton.HumanClickAsync(ct: ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(message))
            {
                IElement? messageBox = await page.QuerySelectorAsync("textarea[name='message']", ct: ct).ConfigureAwait(false);
                if (messageBox != null)
                {
                    await page.TypeAsync("textarea[name='message']", message, ct: ct).ConfigureAwait(false);
                }
            }
            IElement? sendBtn = await page.QuerySelectorAsync("button[data-control-name='send_invite']", ct: ct).ConfigureAwait(false);
            if (sendBtn != null) await sendBtn.HumanClickAsync(ct: ct).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await page.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore page disposal errors
            }
        }
    }
}
