using System;

namespace Ghost.Contracts.News;

/// <summary>
/// Represents a news article.
/// </summary>
public sealed record NewsArticle
{
    /// <summary>
    /// Unique article id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Article title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Article content or body.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Short summary or excerpt.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Source or publisher name.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Article url.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// When the article was published.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Article category.
    /// </summary>
    public NewsCategory Category { get; init; } = NewsCategory.Other;
}
