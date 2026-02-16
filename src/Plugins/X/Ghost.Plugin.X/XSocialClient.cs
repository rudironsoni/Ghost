using System.Text.RegularExpressions;
using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Ghost.Plugin.X.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X;

public partial class XSocialClient : ISocialClient
{
    private readonly IBrowserSession _session;
    private readonly XOptions _options;
    private readonly ILogger<XSocialClient> _logger;
    private readonly XAuthenticator _authenticator;
    private readonly XThreadComposer _threadComposer;
    private readonly XSimulationValidator? _simulationValidator;

    public XSocialClient(
        IBrowserSession session,
        IOptions<XOptions> options,
        ILogger<XSocialClient> logger,
        XAuthenticator authenticator,
        XThreadComposer threadComposer,
        XSimulationValidator? simulationValidator = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _options = options?.Value ?? new XOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XSocialClient>.Instance;
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _threadComposer = threadComposer ?? throw new ArgumentNullException(nameof(threadComposer));
        _simulationValidator = simulationValidator;
    }

    public string PlatformName => "X";

    public async Task<SocialProfile> GetProfileAsync(string profileId, CancellationToken ct = default)
    {
        Log.FetchingProfile(_logger, profileId);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            string url = $"{_options.BaseUrl}/{profileId}";
            await page.NavigateAsync(url, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            SocialProfile profile = await ExtractProfileDataAsync(page, profileId, ct).ConfigureAwait(false);
            Log.ProfileFetched(_logger, profileId);
            return profile;
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialProfile>> SearchProfilesAsync(
        ProfileSearchCriteria criteria,
        CancellationToken ct = default)
    {
        Log.SearchingProfiles(_logger, criteria.Query);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            string encodedQuery = Uri.EscapeDataString(criteria.Query ?? "");
            string url = $"{_options.BaseUrl}/search?q={encodedQuery}&f=user";
            await page.NavigateAsync(url, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            List<SocialProfile> profiles = await ExtractSearchResultsAsync(page, criteria.MaxResults, ct).ConfigureAwait(false);
            Log.ProfilesFound(_logger, profiles.Count, criteria.Query);
            return profiles.AsReadOnly();
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public async Task<SocialPost> CreatePostAsync(
        CreatePostRequest request,
        CancellationToken ct = default)
    {
        Log.CreatingPost(_logger, request.Content?.Length ?? 0);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            string postId = await _threadComposer.ComposeAndPostAsync(page, request, ct).ConfigureAwait(false);

            var post = new SocialPost
            {
                Id = postId,
                AuthorId = "current_user",
                Content = request.Content ?? "",
                CreatedAt = DateTime.UtcNow
            };

            Log.PostCreated(_logger, postId);
            return post;
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialPost>> GetFeedAsync(
        FeedOptions? options = null,
        CancellationToken ct = default)
    {
        Log.FetchingFeed(_logger);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            string feedUrl = $"{_options.BaseUrl}/home";

            await page.NavigateAsync(feedUrl, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            List<SocialPost> posts = await ExtractFeedPostsAsync(page, options?.PageSize ?? 25, ct).ConfigureAwait(false);
            Log.FeedFetched(_logger, posts.Count);
            return posts.AsReadOnly();
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public async Task SendMessageAsync(
        string recipientId,
        string message,
        CancellationToken ct = default)
    {
        Log.SendingMessage(_logger, recipientId);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            await page.NavigateAsync($"{_options.BaseUrl}/messages/compose", ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            IElement searchBox = await page.WaitForSelectorAsync(
                "input[placeholder*='Search']",
                new WaitOptions { Timeout = 10000 },
                ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find recipient search box");

            await searchBox.TypeAsync(recipientId, ct: ct).ConfigureAwait(false);
            await Task.Delay(1000, ct).ConfigureAwait(false);

            IElement firstResult = await page.QuerySelectorAsync("[data-testid='TypeaheadUser']", ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not find user: {recipientId}");

            await firstResult.ClickAsync(ct: ct).ConfigureAwait(false);

            IElement nextButton = await page.WaitForSelectorAsync(
                "button[data-testid='nextButton']",
                new WaitOptions { Timeout = 10000 },
                ct).ConfigureAwait(false);

            if (nextButton != null)
            {
                await nextButton.ClickAsync(ct: ct).ConfigureAwait(false);
            }

            IElement messageBox = await page.WaitForSelectorAsync(
                "div[role='textbox'][contenteditable='true']",
                new WaitOptions { Timeout = 10000 },
                ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find message input box");

            await messageBox.TypeAsync(message, ct: ct).ConfigureAwait(false);

            IElement sendButton = await page.WaitForSelectorAsync(
                "button[data-testid='send']",
                new WaitOptions { Timeout = 10000 },
                ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find send button");

            await sendButton.ClickAsync(ct: ct).ConfigureAwait(false);
            await Task.Delay(1000, ct).ConfigureAwait(false);

            Log.MessageSent(_logger, recipientId);
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialConnection>> GetConnectionsAsync(
        ConnectionsOptions? options = null,
        CancellationToken ct = default)
    {
        string profileId = options?.ProfileId ?? "me";
        Log.FetchingConnections(_logger, profileId);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            string connectionUrl = $"{_options.BaseUrl}/{profileId}/following";

            await page.NavigateAsync(connectionUrl, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            List<SocialConnection> connections = await ExtractSocialConnectionsAsync(page, profileId, ct).ConfigureAwait(false);
            Log.ConnectionsFetched(_logger, connections.Count, profileId);
            return connections.AsReadOnly();
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public async Task SendConnectionRequestAsync(
        string profileId,
        string? message = null,
        CancellationToken ct = default)
    {
        Log.FollowingUser(_logger, profileId);
        PageOptions pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/{profileId}", ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);

            IElement followButton = await page.WaitForSelectorAsync(
                "button[data-testid='follow']",
                new WaitOptions { Timeout = 10000 },
                ct).ConfigureAwait(false);

            if (followButton == null)
            {
                IElement? followingButton = await page.QuerySelectorAsync("button[data-testid='unfollow']", ct).ConfigureAwait(false);
                if (followingButton != null)
                {
                    Log.AlreadyFollowing(_logger, profileId);
                    return;
                }

                throw new InvalidOperationException("Could not find follow button");
            }

            await followButton.ClickAsync(ct: ct).ConfigureAwait(false);
            await Task.Delay(1000, ct).ConfigureAwait(false);

            Log.UserFollowed(_logger, profileId);
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    private async Task<SocialProfile> ExtractProfileDataAsync(
        IPage page,
        string profileId,
        CancellationToken ct)
    {
        string? name = null;
        string? bio = null;
        int? followersCount = null;

        try
        {
            name = await page.EvaluateAsync<string?>(
                @"() => {
                    const nameEl = document.querySelector('[data-testid=""UserName""]');
                    if (nameEl) {
                        const span = nameEl.querySelector('span');
                        return span?.innerText || '';
                    }
                    return '';
                }",
                ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.ProfileNameExtractionFailed(_logger, ex);
        }

        try
        {
            bio = await page.EvaluateAsync<string?>(
                @"() => {
                    const bioEl = document.querySelector('[data-testid=""UserDescription""]');
                    return bioEl?.innerText || '';
                }",
                ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.ProfileBioExtractionFailed(_logger, ex);
        }

        try
        {
            string? followersText = await page.EvaluateAsync<string?>(
                "() => {" +
                "    const link = document.querySelector('a[href$=\"/followers\"]');" +
                "    return link?.innerText || '';" +
                "}", ct: ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(followersText))
            {
                Match match = Regex.Match(followersText, @"([\d,\.]+)\s*[KkMm]?");
                if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out int followers))
                {
                    followersCount = followers;
                }
            }
        }
        catch (Exception ex)
        {
            Log.FollowerCountExtractionFailed(_logger, ex);
        }

        return new SocialProfile
        {
            Id = profileId,
            Name = name ?? profileId,
            Bio = bio,
            FollowersCount = followersCount ?? 0
        };
    }

    private async Task<List<SocialProfile>> ExtractSearchResultsAsync(
        IPage page,
        int maxResults,
        CancellationToken ct)
    {
        List<SocialProfile> profiles = [];

        try
        {
            IReadOnlyList<IElement> cells = await page.QuerySelectorAllAsync("[data-testid='UserCell']", ct).ConfigureAwait(false);

            foreach (IElement? cell in cells.Take(maxResults))
            {
                try
                {
                    IElement? userNameEl = await cell.QuerySelectorAsync("[data-testid='UserName']", ct).ConfigureAwait(false);
                    if (userNameEl == null) continue;

                    string? name = await userNameEl.GetTextContentAsync(ct).ConfigureAwait(false);

                    IElement? linkEl = await cell.QuerySelectorAsync("a[href^='/']", ct).ConfigureAwait(false);
                    string? handle = linkEl != null
                        ? await linkEl.GetAttributeAsync("href", ct).ConfigureAwait(false)
                        : null;

                    if (!string.IsNullOrWhiteSpace(handle))
                    {
                        handle = handle.TrimStart('/');
                        profiles.Add(new SocialProfile
                        {
                            Id = handle,
                            Name = name ?? handle
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.SearchResultExtractionFailed(_logger, ex);
                }
            }
        }
        catch (Exception ex)
        {
            Log.SearchResultsExtractionFailed(_logger, ex);
        }

        return profiles;
    }

    private async Task<List<SocialPost>> ExtractFeedPostsAsync(
        IPage page,
        int maxPosts,
        CancellationToken ct)
    {
        List<SocialPost> posts = [];

        try
        {
            IReadOnlyList<IElement> articles = await page.QuerySelectorAllAsync("article[data-testid='tweet']", ct).ConfigureAwait(false);

            foreach (IElement? article in articles.Take(maxPosts))
            {
                try
                {
                    SocialPost? post = await ExtractPostFromArticleAsync(article, ct).ConfigureAwait(false);
                    if (post != null)
                    {
                        posts.Add(post);
                    }
                }
                catch (Exception ex)
                {
                    Log.FeedPostExtractionFailed(_logger, ex);
                }
            }
        }
        catch (Exception ex)
        {
            Log.FeedPostsExtractionFailed(_logger, ex);
        }

        return posts;
    }

    private async Task<SocialPost?> ExtractPostFromArticleAsync(IElement article, CancellationToken ct)
    {
        try
        {
            IElement? linkEl = await article.QuerySelectorAsync("a[href*='/status/']", ct).ConfigureAwait(false);
            string? href = linkEl != null ? await linkEl.GetAttributeAsync("href", ct).ConfigureAwait(false) : null;

            if (string.IsNullOrWhiteSpace(href)) return null;

            Match match = Regex.Match(href, @"/status/(\d+)");
            if (!match.Success) return null;

            string tweetId = match.Groups[1].Value;

            IElement? contentEl = await article.QuerySelectorAsync("[data-testid='tweetText']", ct).ConfigureAwait(false);
            string? content = contentEl != null
                ? await contentEl.GetTextContentAsync(ct).ConfigureAwait(false)
                : "";

            IElement? userNameEl = await article.QuerySelectorAsync("[data-testid='User-Name']", ct).ConfigureAwait(false);
            IElement? authorLinkEl = userNameEl != null
                ? await userNameEl.QuerySelectorAsync("a[href^='/']", ct).ConfigureAwait(false)
                : null;
            string authorId = authorLinkEl != null
                ? (await authorLinkEl.GetAttributeAsync("href", ct).ConfigureAwait(false))?.TrimStart('/') ?? "unknown"
                : "unknown";

            return new SocialPost
            {
                Id = tweetId,
                AuthorId = authorId,
                Content = content ?? "",
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Log.PostFromArticleExtractionFailed(_logger, ex);
            return null;
        }
    }

    private async Task<List<SocialConnection>> ExtractSocialConnectionsAsync(
        IPage page,
        string profileId,
        CancellationToken ct)
    {
        List<SocialConnection> connections = [];

        try
        {
            IReadOnlyList<IElement> cells = await page.QuerySelectorAllAsync("[data-testid='UserCell']", ct).ConfigureAwait(false);

            foreach (IElement cell in cells)
            {
                try
                {
                    IElement? userNameEl = await cell.QuerySelectorAsync("[data-testid='UserName']", ct).ConfigureAwait(false);
                    if (userNameEl == null) continue;

                    string? name = await userNameEl.GetTextContentAsync(ct).ConfigureAwait(false);

                    IElement? linkEl = await cell.QuerySelectorAsync("a[href^='/']", ct).ConfigureAwait(false);
                    string? handle = linkEl != null
                        ? await linkEl.GetAttributeAsync("href", ct).ConfigureAwait(false)
                        : null;

                    if (!string.IsNullOrWhiteSpace(handle))
                    {
                        handle = handle.TrimStart('/');
                        connections.Add(new SocialConnection
                        {
                            Id = Guid.NewGuid().ToString(),
                            FromProfileId = profileId,
                            ToProfileId = handle,
                            ConnectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.ConnectionExtractionFailed(_logger, ex);
                }
            }
        }
        catch (Exception ex)
        {
            Log.ConnectionsExtractionFailed(_logger, ex);
        }

        return connections;
    }
}
