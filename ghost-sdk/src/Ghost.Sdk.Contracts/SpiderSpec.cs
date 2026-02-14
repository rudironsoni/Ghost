using System.Text.Json;

namespace Ghost.Sdk.Contracts;

/// <summary>
/// Specification for a spider as a declarative step graph.
/// </summary>
public sealed record SpiderSpec(
    SpiderId SpiderId,
    string Version,
    IReadOnlyDictionary<string, StepSpec> Steps,
    string EntryStepId);

/// <summary>
/// Base class for all step specifications.
/// </summary>
public abstract record StepSpec(string StepId, string Kind);

/// <summary>
/// Step to build an HTTP request from a template.
/// </summary>
public sealed record BuildRequestStep(
    string StepId,
    string UrlTemplate,
    string Method,
    IReadOnlyDictionary<string, string> Headers) : StepSpec(StepId, StepKinds.BuildRequest);

/// <summary>
/// Step to execute an HTTP fetch.
/// </summary>
public sealed record HttpFetchStep(
    string StepId,
    string RequestStepId) : StepSpec(StepId, StepKinds.HttpFetch);

/// <summary>
/// Step to execute a browser-based fetch.
/// </summary>
public sealed record BrowserFetchStep(
    string StepId,
    string RequestStepId) : StepSpec(StepId, StepKinds.BrowserFetch);

/// <summary>
/// Step to parse HTML and extract data.
/// </summary>
public sealed record ParseHtmlStep(
    string StepId,
    string ResponseStepId,
    IReadOnlyList<FieldSelector> Selectors) : StepSpec(StepId, StepKinds.ParseHtml);

/// <summary>
/// Step to emit an extracted item.
/// </summary>
public sealed record EmitItemStep(
    string StepId,
    string ParseStepId,
    string ItemType) : StepSpec(StepId, StepKinds.EmitItem);

/// <summary>
/// Step to follow links and enqueue new requests.
/// </summary>
public sealed record FollowLinksStep(
    string StepId,
    string ParseStepId,
    string LinkSelector) : StepSpec(StepId, StepKinds.FollowLinks);

/// <summary>
/// Selector for extracting a field from HTML.
/// </summary>
public sealed record FieldSelector(
    string Field,
    string CssSelector,
    string? Attribute);

/// <summary>
/// Known step kind constants.
/// </summary>
public static class StepKinds
{
    public const string BuildRequest = "build_request";
    public const string HttpFetch = "http_fetch";
    public const string BrowserFetch = "browser_fetch";
    public const string ParseHtml = "parse_html";
    public const string EmitItem = "emit_item";
    public const string FollowLinks = "follow_links";
}
