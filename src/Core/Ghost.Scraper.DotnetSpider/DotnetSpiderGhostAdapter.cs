using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotnetSpider.Downloader;
using DotnetSpider.Http;
using Ghost.Platform.Common.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghost.Scraper.DotnetSpider;

public sealed class DotnetSpiderGhostAdapter
{
    private readonly ISessionOrchestrator _sessionOrchestrator;
    private readonly ILogger<DotnetSpiderGhostAdapter> _logger;
    private readonly IOptions<DotnetSpiderOptions> _options;

    public DotnetSpiderGhostAdapter(ISessionOrchestrator sessionOrchestrator, IOptions<DotnetSpiderOptions> options, ILogger<DotnetSpiderGhostAdapter>? logger = null)
    {
        _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<DotnetSpiderGhostAdapter>.Instance;
    }

    public IDownloader CreateDownloader(string platformName, string? countryCode = null, int? complexityScore = null)
    {
        if (string.IsNullOrWhiteSpace(platformName))
        {
            throw new ArgumentException("Platform name is required", nameof(platformName));
        }

        return new GhostSessionDownloader(_sessionOrchestrator, platformName, countryCode, complexityScore, _options, _logger);
    }

    private sealed class GhostSessionDownloader : IDownloader
    {
        private readonly ISessionOrchestrator _sessionOrchestrator;
        private readonly ILogger _logger;
        private readonly string _platformName;
        private readonly string? _countryCode;
        private readonly int? _complexityScore;
        private readonly IOptions<DotnetSpiderOptions> _options;

        public GhostSessionDownloader(
            ISessionOrchestrator sessionOrchestrator,
            string platformName,
            string? countryCode,
            int? complexityScore,
            IOptions<DotnetSpiderOptions> options,
            ILogger logger)
        {
            _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
            _platformName = platformName;
            _countryCode = countryCode;
            _complexityScore = complexityScore;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task<Response> DownloadAsync(Request request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                if (!_options.Value.Enabled)
                {
                    return BuildFailureResponse(request, HttpStatusCode.ServiceUnavailable, "DotnetSpider integration is disabled");
                }

                var context = new SessionAllocationContext(
                    PlatformName: _platformName,
                    CountryCode: _countryCode,
                    SessionType: SessionType.Http,
                    ComplexityScore: _complexityScore,
                    Metadata: new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["RequestUri"] = request.RequestUri?.ToString() ?? string.Empty,
                        ["Method"] = request.Method ?? "GET"
                    });

                var sessionId = await _sessionOrchestrator.AllocateSessionAsync(context, CancellationToken.None);
                var httpSession = await _sessionOrchestrator.GetHttpSessionAsync(sessionId, CancellationToken.None);
                
                if (httpSession == null)
                {
                    return BuildFailureResponse(request, HttpStatusCode.ServiceUnavailable, "Failed to acquire HTTP session");
                }

                var responseMessage = await httpSession.ExecuteAsync(() => request.ToHttpRequestMessage(), CancellationToken.None);
                var response = await responseMessage.ToResponseAsync();
                response.RequestHash = request.Hash;
                response.Version = responseMessage.Version;
                response.TargetUrl = responseMessage.RequestMessage?.RequestUri?.ToString();
                return response;
            }
            catch (Exception ex)
            {
                return BuildFailureResponse(request, HttpStatusCode.Gone, ex.Message);
            }
        }

        private static Response BuildFailureResponse(Request request, HttpStatusCode statusCode, string reason)
        {
            return new Response
            {
                RequestHash = request.Hash,
                StatusCode = statusCode,
                ReasonPhrase = reason,
                Version = HttpVersion.Version11,
                Content = new global::DotnetSpider.Http.ByteArrayContent(Array.Empty<byte>())
            };
        }
    }
}