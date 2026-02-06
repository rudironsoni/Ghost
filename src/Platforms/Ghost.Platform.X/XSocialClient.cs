using System.Text.RegularExpressions;
using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Ghost.Platform.X.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.X;

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
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            var url = $"{_options.BaseUrl}/{profileId}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            var profile = await ExtractProfileDataAsync(page, profileId, ct);
            Log.ProfileFetched(_logger, profileId);
            return profile;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialProfile>> SearchProfilesAsync(
        ProfileSearchCriteria criteria,
        CancellationToken ct = default)
    {
        Log.SearchingProfiles(_logger, criteria.Query);
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            var encodedQuery = Uri.EscapeDataString(criteria.Query ?? "");
            var url = $"{_options.BaseUrl}/search?q={encodedQuery}&f=user";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            var profiles = await ExtractSearchResultsAsync(page, criteria.MaxResults, ct);
            Log.ProfilesFound(_logger, profiles.Count, criteria.Query);
            return profiles.AsReadOnly();
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<SocialPost> CreatePostAsync(
        CreatePostRequest request,
        CancellationToken ct = default)
    {
        Log.CreatingPost(_logger, request.Content?.Length ?? 0);
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            var postId = await _threadComposer.ComposeAndPostAsync(page, request, ct);

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
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialPost>> GetFeedAsync(
        FeedOptions? options = null,
        CancellationToken ct = default)
    {
        Log.FetchingFeed(_logger);
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            var feedUrl = $"{_options.BaseUrl}/home";

            await page.NavigateAsync(feedUrl, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            var posts = await ExtractFeedPostsAsync(page, options?.PageSize ?? 25, ct);
            Log.FeedFetched(_logger, posts.Count);
            return posts.AsReadOnly();
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task SendMessageAsync(
        string recipientId,
        string message,
        CancellationToken ct = default)
    {
        Log.SendingMessage(_logger, recipientId);
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            await page.NavigateAsync($"{_options.BaseUrl}/messages/compose", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var searchBox = await page.WaitForSelectorAsync(
                "input[placeholder*='Search']",
                new WaitOptions { Timeout = 10000 },
                ct);

            if (searchBox == null)
            {
                throw new InvalidOperationException("Could not find recipient search box");
            }

            await searchBox.TypeAsync(recipientId, ct: ct);
            await Task.Delay(1000, ct);

            var firstResult = await page.QuerySelectorAsync("[data-testid='TypeaheadUser']", ct);
            if (firstResult == null)
            {
                throw new InvalidOperationException($"Could not find user: {recipientId}");
            }

            await firstResult.ClickAsync(ct: ct);

            var nextButton = await page.WaitForSelectorAsync(
                "button[data-testid='nextButton']",
                new WaitOptions { Timeout = 10000 },
                ct);

            if (nextButton != null)
            {
                await nextButton.ClickAsync(ct: ct);
            }

            var messageBox = await page.WaitForSelectorAsync(
                "div[role='textbox'][contenteditable='true']",
                new WaitOptions { Timeout = 10000 },
                ct);

            if (messageBox == null)
            {
                throw new InvalidOperationException("Could not find message input box");
            }

            await messageBox.TypeAsync(message, ct: ct);

            var sendButton = await page.WaitForSelectorAsync(
                "button[data-testid='send']",
                new WaitOptions { Timeout = 10000 },
                ct);

            if (sendButton == null)
            {
                throw new InvalidOperationException("Could not find send button");
            }

            await sendButton.ClickAsync(ct: ct);
            await Task.Delay(1000, ct);

            Log.MessageSent(_logger, recipientId);
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<IReadOnlyList<SocialConnection>> GetConnectionsAsync(
        ConnectionsOptions? options = null,
        CancellationToken ct = default)
    {
        var profileId = options?.ProfileId ?? "me";
        Log.FetchingConnections(_logger, profileId);
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            var connectionUrl = $"{_options.BaseUrl}/{profileId}/following";

            await page.NavigateAsync(connectionUrl, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);
            var connections = await ExtractSocialConnectionsAsync(page, profileId, ct);
            Log.ConnectionsFetched(_logger, connections.Count, profileId);
            return connections.AsReadOnly();
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task SendConnectionRequestAsync(
        string profileId,
        string? message = null,
        CancellationToken ct = default)
    {
        Log.FollowingUser(_logger, profileId);
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);

        try
        {
            await page.NavigateAsync($"{_options.BaseUrl}/{profileId}", ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);
            await _authenticator.EnsureAuthenticatedAsync(page, ct).ConfigureAwait(false);

            var followButton = await page.WaitForSelectorAsync(
                "button[data-testid='follow']",
                new WaitOptions { Timeout = 10000 },
                ct);

            if (followButton == null)
            {
                var followingButton = await page.QuerySelectorAsync("button[data-testid='unfollow']", ct);
                if (followingButton != null)
                {
                    Log.AlreadyFollowing(_logger, profileId);
                    return;
                }

                throw new InvalidOperationException("Could not find follow button");
            }

            await followButton.ClickAsync(ct: ct);
            await Task.Delay(1000, ct);

            Log.UserFollowed(_logger, profileId);
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
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
                ct: ct);
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
                ct: ct);
        }
        catch (Exception ex)
        {
            Log.ProfileBioExtractionFailed(_logger, ex);
        }

        try
        {
            var followersText = await page.EvaluateAsync<string?>(
                "() => {" +
                "    const link = document.querySelector('a[href$=\"/followers\"]');" +
                "    return link?.innerText || '';" +
                "}", ct);

            if (!string.IsNullOrWhiteSpace(followersText))
            {
                var match = Regex.Match(followersText, @"([\d,\.]+)\s*[KkMm]?");
                if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out var followers))
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
        var profiles = new List<SocialProfile>();

        try
        {
            var cells = await page.QuerySelectorAllAsync("[data-testid='UserCell']", ct);

            foreach (var cell in cells.Take(maxResults))
            {
                try
                {
                    var userNameEl = await cell.QuerySelectorAsync("[data-testid='UserName']", ct);
                    if (userNameEl == null) continue;

                    var name = await userNameEl.GetTextContentAsync(ct);

                    var linkEl = await cell.QuerySelectorAsync("a[href^='/']", ct);
                    var handle = linkEl != null
                        ? await linkEl.GetAttributeAsync("href", ct)
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
        var posts = new List<SocialPost>();

        try
        {
            var articles = await page.QuerySelectorAllAsync("article[data-testid='tweet']", ct);

            foreach (var article in articles.Take(maxPosts))
            {
                try
                {
                    var post = await ExtractPostFromArticleAsync(article, ct);
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
            var linkEl = await article.QuerySelectorAsync("a[href*='/status/']", ct);
            var href = linkEl != null ? await linkEl.GetAttributeAsync("href", ct) : null;

            if (string.IsNullOrWhiteSpace(href)) return null;

            var match = Regex.Match(href, @"/status/(\d+)");
            if (!match.Success) return null;

            var tweetId = match.Groups[1].Value;

            var contentEl = await article.QuerySelectorAsync("[data-testid='tweetText']", ct);
            var content = contentEl != null
                ? await contentEl.GetTextContentAsync(ct)
                : "";

            var userNameEl = await article.QuerySelectorAsync("[data-testid='User-Name']", ct);
            var authorLinkEl = userNameEl != null
                ? await userNameEl.QuerySelectorAsync("a[href^='/']", ct)
                : null;
            var authorId = authorLinkEl != null
                ? (await authorLinkEl.GetAttributeAsync("href", ct))?.TrimStart('/') ?? "unknown"
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
        var connections = new List<SocialConnection>();

        try
        {
            var cells = await page.QuerySelectorAllAsync("[data-testid='UserCell']", ct);

            foreach (var cell in cells)
            {
                try
                {
                    var userNameEl = await cell.QuerySelectorAsync("[data-testid='UserName']", ct);
                    if (userNameEl == null) continue;

                    var name = await userNameEl.GetTextContentAsync(ct);

                    var linkEl = await cell.QuerySelectorAsync("a[href^='/']", ct);
                    var handle = linkEl != null
                        ? await linkEl.GetAttributeAsync("href", ct)
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
