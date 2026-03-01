using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.LinkedIn;

internal static partial class LinkedInLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse search node")]
    public static partial void LogFailedToParseSearchNode(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse job node")]
    public static partial void LogFailedToParseJobNode(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn job details fetch failed for job {JobId}. Returning mock job data as fallback.")]
    public static partial void LogJobDetailsFetchFailed(ILogger logger, string jobId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn news articles fetch failed. Returning mock news data as fallback.")]
    public static partial void LogNewsArticlesFetchFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn news search failed. Returning mock news data as fallback.")]
    public static partial void LogNewsSearchFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn profile fetch failed for {ProfileId}. Returning mock profile data as fallback.")]
    public static partial void LogProfileFetchFailed(ILogger logger, string profileId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose page in {Operation}")]
    public static partial void LogPageDisposeFailed(ILogger logger, string operation, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse feed post content - element may be stale or detached")]
    public static partial void LogFeedPostParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse connection card - element may be stale or detached")]
    public static partial void LogConnectionCardParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Page loaded: {Url} - Title: {Title}")]
    public static partial void LogPageLoaded(ILogger logger, string url, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Page content length: {Length}")]
    public static partial void LogDebugContentLength(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} job containers on page")]
    public static partial void LogDebugContainerCount(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Debug HTML saved to: {Path}")]
    public static partial void LogDebugHtmlSaved(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Browser strategy starting for URL: {Url}")]
    public static partial void LogBrowserStrategyStarting(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracted {Count} jobs from JavaScript")]
    public static partial void LogDebugExtractedJobCount(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job data - ID: {Id}, Title: '{Title}', Company: '{Company}'")]
    public static partial void LogDebugJobData(ILogger logger, string id, string title, string company);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job added to list: {Id} - {Title}")]
    public static partial void LogDebugJobAdded(ILogger logger, string id, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job skipped (no title): {Id}")]
    public static partial void LogDebugJobSkippedNoTitle(ILogger logger, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Final list count: {Count}")]
    public static partial void LogDebugListCount(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "JavaScript extraction failed: {Error}")]
    public static partial void LogJavaScriptExtractionFailed(ILogger logger, string error, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "JavaScript error: {Error}")]
    public static partial void LogJavaScriptError(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entity parse failed for job {JobId}: {Reason}")]
    public static partial void LogDebugEntityParseFailed(ILogger logger, string jobId, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Parsing entity for job {JobId}, HTML length: {Length}")]
    public static partial void LogDebugParsingEntity(ILogger logger, string jobId, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Title node found: {Title}")]
    public static partial void LogDebugTitleNode(ILogger logger, string title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "AngleSharp found {BodyCount} body elements, {TitleCount} title elements")]
    public static partial void LogDebugAngleSharp(ILogger logger, int bodyCount, int titleCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Body HTML length: {Length}")]
    public static partial void LogDebugBodyHtml(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Body-only title count: {Count}")]
    public static partial void LogDebugBodyTitleCount(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entity parse exception: {Message}")]
    public static partial void LogDebugEntityParseException(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entity values for {JobId}: Title='{Title}', Company='{Company}', EntityJobId='{EntityJobId}'")]
    public static partial void LogDebugEntityValues(ILogger logger, string jobId, string title, string company, string entityJobId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Direct body count: {Count}")]
    public static partial void LogDebugDirectBodyCount(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Body OuterHtml length: {Length}")]
    public static partial void LogDebugBodyOuterHtml(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entity nodes count: {Count}")]
    public static partial void LogDebugEntityNodesCount(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata: Expression='{Expression}', Type='{SelectorType}', Properties={PropertyCount}")]
    public static partial void LogDebugMetadata(ILogger logger, string expression, string selectorType, int propertyCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Test query found {Count} elements")]
    public static partial void LogDebugTestQuery(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Body context length: {Length}")]
    public static partial void LogDebugBodyContext(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trying manual extraction for {JobId}")]
    public static partial void LogDebugTryingManualExtraction(ILogger logger, string jobId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Manual elements: Title='{Title}', Company='{Company}', Location='{Location}'")]
    public static partial void LogDebugManualElements(ILogger logger, string title, string company, string location);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Document-level query found {Count} title elements")]
    public static partial void LogDebugDocQuery(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Body-only query: {BodyCount} bodies, {TitleCount} titles")]
    public static partial void LogDebugBodyOnlyQuery(ILogger logger, int bodyCount, int titleCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Verify query: {BodyCount} bodies, {TitleCount} titles")]
    public static partial void LogDebugVerifyQuery(ILogger logger, int bodyCount, int titleCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "EntitySelect found {Count} bodies")]
    public static partial void LogDebugEntitySelect(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entity OuterHtml length: {Length}")]
    public static partial void LogDebugEntityOuterHtml(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Prop query: {BodyCount} bodies, {TitleCount} titles")]
    public static partial void LogDebugPropQuery(ILogger logger, int bodyCount, int titleCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Context content length: {Length}")]
    public static partial void LogDebugContextContent(ILogger logger, int length);
}
