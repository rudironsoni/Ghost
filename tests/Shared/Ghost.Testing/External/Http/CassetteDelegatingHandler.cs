using System.Linq;
using System.Net;

namespace Ghost.Testing.External.Http;

public sealed class CassetteDelegatingHandler : DelegatingHandler
{
    private readonly CassetteStore _store;
    private readonly Func<CassetteMode> _modeResolver;

    public CassetteDelegatingHandler(CassetteStore store, CassetteMode mode)
        : this(store, () => mode)
    {
    }

    public CassetteDelegatingHandler(CassetteStore store, Func<CassetteMode> modeResolver)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _modeResolver = modeResolver ?? throw new ArgumentNullException(nameof(modeResolver));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Request URI cannot be null when using cassette handler.");
        }

        CassetteMode mode = _modeResolver();

        return mode switch
        {
            CassetteMode.Replay => await ReplayAsync(request, cancellationToken).ConfigureAwait(false),
            CassetteMode.Record => await RecordAsync(request, cancellationToken).ConfigureAwait(false),
            CassetteMode.Passthrough => await base.SendAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported cassette mode '{mode}'.")
        };
    }

    private async Task<HttpResponseMessage> ReplayAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string key = _store.BuildKey(request.Method, request.RequestUri!);
        CassetteEnvelope? cassette = await _store.ReadAsync(key, cancellationToken).ConfigureAwait(false);

        if (cassette is null)
        {
            throw new CassetteNotFoundException(
                $"Cassette not found for {request.Method} {request.RequestUri}. " +
                $"Missing key: {key}. Run with GHOST_CASSETTES=record to capture this request.");
        }

        HttpResponseMessage response = new((HttpStatusCode)cassette.Response.StatusCode)
        {
            ReasonPhrase = cassette.Response.ReasonPhrase,
            RequestMessage = request
        };

        byte[] body = string.IsNullOrEmpty(cassette.Response.BodyBase64)
            ? []
            : Convert.FromBase64String(cassette.Response.BodyBase64);

        response.Content = new ByteArrayContent(body);
        ApplyHeaders(response, cassette.Response.Headers);

        return response;
    }

    private async Task<HttpResponseMessage> RecordAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (InnerHandler is null)
        {
            throw new InvalidOperationException("Record mode requires an inner HTTP handler.");
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        byte[] responseBody = response.Content is null
            ? []
            : await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        string key = _store.BuildKey(request.Method, request.RequestUri!);
        CassetteEnvelope envelope = new()
        {
            Key = key,
            RecordedAt = DateTimeOffset.UtcNow,
            Request = new CassetteRequest
            {
                Method = request.Method.Method,
                Url = CassetteRedactor.RedactUrl(request.RequestUri!.AbsoluteUri),
                Headers = CassetteRedactor.RedactHeaders(request.Headers)
            },
            Response = new CassetteResponse
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                Headers = CassetteRedactor.RedactHeaders(GetResponseHeaders(response)),
                BodyBase64 = Convert.ToBase64String(responseBody)
            }
        };

        await _store.WriteAsync(key, envelope, cancellationToken).ConfigureAwait(false);

        if (response.Content is not null)
        {
            List<KeyValuePair<string, IEnumerable<string>>> contentHeaders = response.Content.Headers
                .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value))
                .ToList();

            HttpContent originalContent = response.Content;
            ByteArrayContent replayableContent = new(responseBody);

            foreach (KeyValuePair<string, IEnumerable<string>> header in contentHeaders)
            {
                replayableContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = replayableContent;
            originalContent.Dispose();
        }

        return response;
    }

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> GetResponseHeaders(HttpResponseMessage response)
    {
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHeaders = response.Headers
            .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));

        if (response.Content is null)
        {
            return responseHeaders;
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders = response.Content.Headers
            .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));

        return responseHeaders.Concat(contentHeaders);
    }

    private static void ApplyHeaders(HttpResponseMessage response, Dictionary<string, List<string>> headers)
    {
        foreach (KeyValuePair<string, List<string>> header in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }
}
