using System.Collections.Concurrent;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Statistics;

/// <summary>
/// Thread-safe implementation of depth tracking for crawled requests.
/// </summary>
/// <remarks>
/// This tracker maintains a concurrent dictionary mapping URLs to their depths.
/// It is designed for use in multi-threaded spider environments.
/// </remarks>
public class DepthTracker : IDepthTracker
{
    private readonly ConcurrentDictionary<string, int> _depths = new();
    private readonly string _startUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepthTracker"/> class.
    /// </summary>
    /// <param name="startUrl">The starting URL for the crawl (depth 0).</param>
    public DepthTracker(string startUrl)
    {
        ArgumentNullException.ThrowIfNull(startUrl);
        _startUrl = startUrl;
    }

    /// <inheritdoc/>
    public int GetDepth(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _depths.GetValueOrDefault(request.Url, 0);
    }

    /// <inheritdoc/>
    public void SetDepth(Request request, int depth)
    {
        ArgumentNullException.ThrowIfNull(request);
        _depths[request.Url] = depth;
    }

    /// <inheritdoc/>
    public DepthStatistics GetStatistics()
    {
        var depths = _depths.Values.ToList();

        if (depths.Count == 0)
        {
            return new DepthStatistics
            {
                MaxDepth = 0,
                AverageDepth = 0,
                TotalUrls = 0,
                Distribution = []
            };
        }

        return new DepthStatistics
        {
            MaxDepth = depths.Max(),
            AverageDepth = depths.Average(),
            TotalUrls = depths.Count,
            Distribution = depths.GroupBy(d => d)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}
