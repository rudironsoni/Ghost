using FluentValidation;
using Ghost.Sdk.Spider.Configuration.Models;

namespace Ghost.Sdk.Spider.Configuration.Validation;

/// <summary>
/// Validator for <see cref="SpiderConfiguration"/>.
/// </summary>
public sealed class SpiderConfigurationValidator : AbstractValidator<SpiderConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpiderConfigurationValidator"/> class.
    /// </summary>
    public SpiderConfigurationValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Spider ID is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Spider name is required")
            .MaximumLength(200)
            .WithMessage("Spider name must not exceed 200 characters");

        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required")
            .Matches(@"^\d+\.\d+\.\d+$")
            .WithMessage("Version must be in semver format (e.g., 1.0.0)");

        RuleFor(x => x.Target)
            .NotNull()
            .WithMessage("Target configuration is required")
            .SetValidator(new TargetConfigurationValidator());

        RuleFor(x => x.Extraction)
            .SetValidator(new ExtractionConfigurationValidator()!)
            .When(x => x.Extraction != null);

        RuleFor(x => x.Navigation)
            .NotNull()
            .SetValidator(new NavigationConfigurationValidator());

        RuleFor(x => x.Strategies)
            .NotNull()
            .SetValidator(new StrategiesConfigurationValidator());

        RuleFor(x => x.Pipeline)
            .NotNull()
            .SetValidator(new PipelineConfigurationValidator());

        RuleFor(x => x.Storage)
            .NotNull()
            .SetValidator(new StorageConfigurationValidator());

        RuleFor(x => x.Schedule)
            .SetValidator(new ScheduleConfigurationValidator()!)
            .When(x => x.Schedule != null);

        RuleFor(x => x.Monitoring)
            .NotNull()
            .SetValidator(new MonitoringConfigurationValidator());

        RuleFor(x => x.Limits)
            .NotNull()
            .SetValidator(new LimitsConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="TargetConfiguration"/>.
/// </summary>
public sealed class TargetConfigurationValidator : AbstractValidator<TargetConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TargetConfigurationValidator"/> class.
    /// </summary>
    public TargetConfigurationValidator()
    {
        RuleFor(x => x.StartUrls)
            .NotEmpty()
            .WithMessage("At least one start URL is required");

        RuleForEach(x => x.StartUrls)
            .Must(BeValidUrl)
            .WithMessage("All start URLs must be valid HTTP(S) URLs");

        RuleFor(x => x.MaxDepth)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxDepth must be >= 0");

        RuleFor(x => x.UserAgent)
            .NotEmpty()
            .WithMessage("User agent is required");

        RuleFor(x => x.Authentication)
            .SetValidator(new AuthenticationConfigurationValidator()!)
            .When(x => x.Authentication != null);
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Validator for <see cref="AuthenticationConfiguration"/>.
/// </summary>
public sealed class AuthenticationConfigurationValidator : AbstractValidator<AuthenticationConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationConfigurationValidator"/> class.
    /// </summary>
    public AuthenticationConfigurationValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type => new[] { "Basic", "Bearer", "Cookie", "OAuth2" }.Contains(type))
            .WithMessage("Authentication type must be Basic, Bearer, Cookie, or OAuth2");

        When(x => x.Type == "Basic", () =>
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .WithMessage("Username is required for Basic authentication");
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required for Basic authentication");
        });

        When(x => x.Type == "Bearer", () =>
        {
            RuleFor(x => x.Token)
                .NotEmpty()
                .WithMessage("Token is required for Bearer authentication");
        });

        When(x => x.Type == "OAuth2", () =>
        {
            RuleFor(x => x.OAuth2)
                .NotNull()
                .WithMessage("OAuth2 configuration is required for OAuth2 authentication")
                .SetValidator(new OAuth2ConfigurationValidator()!);
        });
    }
}

/// <summary>
/// Validator for <see cref="OAuth2Configuration"/>.
/// </summary>
public sealed class OAuth2ConfigurationValidator : AbstractValidator<OAuth2Configuration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2ConfigurationValidator"/> class.
    /// </summary>
    public OAuth2ConfigurationValidator()
    {
        RuleFor(x => x.TokenUrl)
            .NotEmpty()
            .Must(BeValidUrl)
            .WithMessage("Token URL must be a valid HTTP(S) URL");

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("Client ID is required");

        RuleFor(x => x.ClientSecret)
            .NotEmpty()
            .WithMessage("Client secret is required");
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Validator for <see cref="ExtractionConfiguration"/>.
/// </summary>
public sealed class ExtractionConfigurationValidator : AbstractValidator<ExtractionConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionConfigurationValidator"/> class.
    /// </summary>
    public ExtractionConfigurationValidator()
    {
        RuleFor(x => x.Engine)
            .Must(engine => new[] { "Playwright", "AngleSharp", "HtmlAgilityPack" }.Contains(engine))
            .WithMessage("Engine must be Playwright, AngleSharp, or HtmlAgilityPack");

        RuleFor(x => x.DefaultSelectorType)
            .Must(type => new[] { "CSS", "XPath", "JsonPath", "JMESPath" }.Contains(type))
            .WithMessage("DefaultSelectorType must be CSS, XPath, JsonPath, or JMESPath");

        RuleFor(x => x.WaitAfterLoad)
            .GreaterThanOrEqualTo(0)
            .WithMessage("WaitAfterLoad must be >= 0");

        RuleForEach(x => x.Entities)
            .SetValidator(new EntityConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="EntityConfiguration"/>.
/// </summary>
public sealed class EntityConfigurationValidator : AbstractValidator<EntityConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityConfigurationValidator"/> class.
    /// </summary>
    public EntityConfigurationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Entity name is required");

        RuleFor(x => x.Container)
            .SetValidator(new SelectorConfigurationValidator()!)
            .When(x => x.Container != null);

        RuleForEach(x => x.Fields)
            .SetValidator(new FieldConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="FieldConfiguration"/>.
/// </summary>
public sealed class FieldConfigurationValidator : AbstractValidator<FieldConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldConfigurationValidator"/> class.
    /// </summary>
    public FieldConfigurationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Field name is required");

        RuleFor(x => x.Type)
            .Must(type => new[] { "String", "Integer", "Decimal", "Boolean", "DateTime", "Url" }.Contains(type))
            .WithMessage("Type must be String, Integer, Decimal, Boolean, DateTime, or Url");

        RuleFor(x => x.Selector)
            .NotNull()
            .SetValidator(new SelectorConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="SelectorConfiguration"/>.
/// </summary>
public sealed class SelectorConfigurationValidator : AbstractValidator<SelectorConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectorConfigurationValidator"/> class.
    /// </summary>
    public SelectorConfigurationValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => new[] { "CSS", "XPath", "JsonPath", "JMESPath", "Regex" }.Contains(type))
            .WithMessage("Selector type must be CSS, XPath, JsonPath, JMESPath, or Regex");

        RuleFor(x => x.Expression)
            .NotEmpty()
            .WithMessage("Selector expression is required");

        RuleFor(x => x.RegexGroup)
            .GreaterThanOrEqualTo(0)
            .WithMessage("RegexGroup must be >= 0");
    }
}

/// <summary>
/// Validator for <see cref="NavigationConfiguration"/>.
/// </summary>
public sealed class NavigationConfigurationValidator : AbstractValidator<NavigationConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationConfigurationValidator"/> class.
    /// </summary>
    public NavigationConfigurationValidator()
    {
        RuleFor(x => x.LinkSelector)
            .NotEmpty()
            .When(x => x.FollowLinks)
            .WithMessage("LinkSelector is required when FollowLinks is true");

        RuleFor(x => x.DelayBetweenRequests)
            .GreaterThanOrEqualTo(0)
            .WithMessage("DelayBetweenRequests must be >= 0");

        RuleFor(x => x.Pagination)
            .SetValidator(new PaginationConfigurationValidator()!)
            .When(x => x.HandlePagination && x.Pagination != null);

        RuleFor(x => x.InfiniteScroll)
            .SetValidator(new InfiniteScrollConfigurationValidator()!)
            .When(x => x.HandleInfiniteScroll && x.InfiniteScroll != null);
    }
}

/// <summary>
/// Validator for <see cref="PaginationConfiguration"/>.
/// </summary>
public sealed class PaginationConfigurationValidator : AbstractValidator<PaginationConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PaginationConfigurationValidator"/> class.
    /// </summary>
    public PaginationConfigurationValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => new[] { "NextButton", "UrlParameter", "LoadMore" }.Contains(type))
            .WithMessage("Pagination type must be NextButton, UrlParameter, or LoadMore");

        RuleFor(x => x.StartPage)
            .GreaterThanOrEqualTo(0)
            .WithMessage("StartPage must be >= 0");

        When(x => x.MaxPages.HasValue, () =>
        {
            RuleFor(x => x.MaxPages!.Value)
                .GreaterThan(0)
                .WithMessage("MaxPages must be > 0 if specified");
        });
    }
}

/// <summary>
/// Validator for <see cref="InfiniteScrollConfiguration"/>.
/// </summary>
public sealed class InfiniteScrollConfigurationValidator : AbstractValidator<InfiniteScrollConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InfiniteScrollConfigurationValidator"/> class.
    /// </summary>
    public InfiniteScrollConfigurationValidator()
    {
        RuleFor(x => x.MaxScrolls)
            .GreaterThan(0)
            .WithMessage("MaxScrolls must be > 0");

        RuleFor(x => x.ScrollDelay)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ScrollDelay must be >= 0");
    }
}

/// <summary>
/// Validator for <see cref="StrategiesConfiguration"/>.
/// </summary>
public sealed class StrategiesConfigurationValidator : AbstractValidator<StrategiesConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StrategiesConfigurationValidator"/> class.
    /// </summary>
    public StrategiesConfigurationValidator()
    {
        RuleFor(x => x.Prioritization)
            .Must(p => new[] { "FIFO", "LIFO", "Priority", "Custom" }.Contains(p))
            .WithMessage("Prioritization must be FIFO, LIFO, Priority, or Custom");

        RuleFor(x => x.Retry)
            .NotNull()
            .SetValidator(new RetryStrategyConfigurationValidator());

        RuleFor(x => x.RateLimit)
            .NotNull()
            .SetValidator(new RateLimitConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="RetryStrategyConfiguration"/>.
/// </summary>
public sealed class RetryStrategyConfigurationValidator : AbstractValidator<RetryStrategyConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryStrategyConfigurationValidator"/> class.
    /// </summary>
    public RetryStrategyConfigurationValidator()
    {
        RuleFor(x => x.MaxAttempts)
            .GreaterThan(0)
            .WithMessage("MaxAttempts must be > 0");

        RuleFor(x => x.BackoffStrategy)
            .Must(s => new[] { "Fixed", "Linear", "Exponential" }.Contains(s))
            .WithMessage("BackoffStrategy must be Fixed, Linear, or Exponential");

        RuleFor(x => x.InitialDelay)
            .GreaterThan(0)
            .WithMessage("InitialDelay must be > 0");

        RuleFor(x => x.MaxDelay)
            .GreaterThan(0)
            .WithMessage("MaxDelay must be > 0");
    }
}

/// <summary>
/// Validator for <see cref="RateLimitConfiguration"/>.
/// </summary>
public sealed class RateLimitConfigurationValidator : AbstractValidator<RateLimitConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitConfigurationValidator"/> class.
    /// </summary>
    public RateLimitConfigurationValidator()
    {
        RuleFor(x => x.RequestsPerSecond)
            .GreaterThan(0)
            .WithMessage("RequestsPerSecond must be > 0");

        RuleFor(x => x.BurstSize)
            .GreaterThan(0)
            .WithMessage("BurstSize must be > 0");

        RuleFor(x => x.MaxConcurrency)
            .GreaterThan(0)
            .WithMessage("MaxConcurrency must be > 0");
    }
}

/// <summary>
/// Validator for <see cref="PipelineConfiguration"/>.
/// </summary>
public sealed class PipelineConfigurationValidator : AbstractValidator<PipelineConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineConfigurationValidator"/> class.
    /// </summary>
    public PipelineConfigurationValidator()
    {
        RuleForEach(x => x.Stages)
            .SetValidator(new PipelineStageConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="PipelineStageConfiguration"/>.
/// </summary>
public sealed class PipelineStageConfigurationValidator : AbstractValidator<PipelineStageConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStageConfigurationValidator"/> class.
    /// </summary>
    public PipelineStageConfigurationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Stage name is required");

        RuleFor(x => x.Type)
            .Must(type => new[] { "Validation", "Transformation", "Enrichment", "Filter", "Custom" }.Contains(type))
            .WithMessage("Stage type must be Validation, Transformation, Enrichment, Filter, or Custom");

        When(x => x.Type == "Custom", () =>
        {
            RuleFor(x => x.ProcessorType)
                .NotEmpty()
                .WithMessage("ProcessorType is required for Custom stage type");
        });
    }
}

/// <summary>
/// Validator for <see cref="StorageConfiguration"/>.
/// </summary>
public sealed class StorageConfigurationValidator : AbstractValidator<StorageConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationValidator"/> class.
    /// </summary>
    public StorageConfigurationValidator()
    {
        RuleFor(x => x.Provider)
            .Must(p => new[] { "InMemory", "PostgreSQL", "Elasticsearch", "Custom" }.Contains(p))
            .WithMessage("Provider must be InMemory, PostgreSQL, Elasticsearch, or Custom");

        When(x => x.Provider == "PostgreSQL", () =>
        {
            RuleFor(x => x.ConnectionString)
                .NotEmpty()
                .WithMessage("ConnectionString is required for PostgreSQL provider");
        });

        When(x => x.Provider == "Elasticsearch", () =>
        {
            RuleFor(x => x.Elasticsearch)
                .NotNull()
                .WithMessage("Elasticsearch configuration is required for Elasticsearch provider")
                .SetValidator(new ElasticsearchConfigurationValidator()!);
        });

        RuleFor(x => x.BatchSize)
            .GreaterThan(0)
            .WithMessage("BatchSize must be > 0");

        RuleFor(x => x.BatchTimeoutMs)
            .GreaterThan(0)
            .WithMessage("BatchTimeoutMs must be > 0");
    }
}

/// <summary>
/// Validator for <see cref="ElasticsearchConfiguration"/>.
/// </summary>
public sealed class ElasticsearchConfigurationValidator : AbstractValidator<ElasticsearchConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchConfigurationValidator"/> class.
    /// </summary>
    public ElasticsearchConfigurationValidator()
    {
        RuleFor(x => x.Nodes)
            .NotEmpty()
            .WithMessage("At least one Elasticsearch node is required");

        RuleFor(x => x.IndexName)
            .NotEmpty()
            .WithMessage("Index name is required");

        RuleFor(x => x.NumberOfShards)
            .GreaterThan(0)
            .WithMessage("NumberOfShards must be > 0");

        RuleFor(x => x.NumberOfReplicas)
            .GreaterThanOrEqualTo(0)
            .WithMessage("NumberOfReplicas must be >= 0");
    }
}

/// <summary>
/// Validator for <see cref="ScheduleConfiguration"/>.
/// </summary>
public sealed class ScheduleConfigurationValidator : AbstractValidator<ScheduleConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleConfigurationValidator"/> class.
    /// </summary>
    public ScheduleConfigurationValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => new[] { "Cron", "Interval", "Once" }.Contains(type))
            .WithMessage("Schedule type must be Cron, Interval, or Once");

        When(x => x.Type == "Cron", () =>
        {
            RuleFor(x => x.CronExpression)
                .NotEmpty()
                .WithMessage("CronExpression is required for Cron schedule type");
        });

        When(x => x.Type == "Interval", () =>
        {
            RuleFor(x => x.IntervalSeconds)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("IntervalSeconds must be > 0 for Interval schedule type");
        });

        When(x => x.Type == "Once", () =>
        {
            RuleFor(x => x.RunAt)
                .NotNull()
                .WithMessage("RunAt is required for Once schedule type");
        });

        RuleFor(x => x.MaxRuntimeSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxRuntimeSeconds must be >= 0");
    }
}

/// <summary>
/// Validator for <see cref="MonitoringConfiguration"/>.
/// </summary>
public sealed class MonitoringConfigurationValidator : AbstractValidator<MonitoringConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MonitoringConfigurationValidator"/> class.
    /// </summary>
    public MonitoringConfigurationValidator()
    {
        RuleFor(x => x.MetricsExportIntervalSeconds)
            .GreaterThan(0)
            .WithMessage("MetricsExportIntervalSeconds must be > 0");

        RuleFor(x => x.Logging)
            .NotNull()
            .SetValidator(new LoggingConfigurationValidator());
    }
}

/// <summary>
/// Validator for <see cref="LoggingConfiguration"/>.
/// </summary>
public sealed class LoggingConfigurationValidator : AbstractValidator<LoggingConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingConfigurationValidator"/> class.
    /// </summary>
    public LoggingConfigurationValidator()
    {
        RuleFor(x => x.MinimumLevel)
            .Must(level => new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" }.Contains(level))
            .WithMessage("MinimumLevel must be Trace, Debug, Information, Warning, Error, or Critical");
    }
}

/// <summary>
/// Validator for <see cref="LimitsConfiguration"/>.
/// </summary>
public sealed class LimitsConfigurationValidator : AbstractValidator<LimitsConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LimitsConfigurationValidator"/> class.
    /// </summary>
    public LimitsConfigurationValidator()
    {
        RuleFor(x => x.MaxPages)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxPages must be >= 0");

        RuleFor(x => x.MaxDurationSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxDurationSeconds must be >= 0");

        RuleFor(x => x.MaxQueueSize)
            .GreaterThan(0)
            .WithMessage("MaxQueueSize must be > 0");

        RuleFor(x => x.RequestTimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("RequestTimeoutSeconds must be > 0");

        RuleFor(x => x.PageLoadTimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("PageLoadTimeoutSeconds must be > 0");

        RuleFor(x => x.MaxBrowserContexts)
            .GreaterThan(0)
            .WithMessage("MaxBrowserContexts must be > 0");
    }
}
