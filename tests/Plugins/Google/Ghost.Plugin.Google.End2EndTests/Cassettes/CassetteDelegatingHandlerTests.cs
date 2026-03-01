using System.Linq;
using System.Net;
using Ghost.Testing.External.Http;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Google.End2EndTests.Cassettes;

[Trait("Category", "Integration")]
public class CassetteDelegatingHandlerTests : ReliabilityTestBase
{
    public CassetteDelegatingHandlerTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void CassetteModeResolver_ParsesModes_AndDefaultsToReplay()
    {
        const string variableName = "GHOST_CASSETTES_TEST_MODE";
        string? originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "record");
            Assert.Equal(CassetteMode.Record, CassetteModeResolver.FromEnvironment(variableName));

            Environment.SetEnvironmentVariable(variableName, "passthrough");
            Assert.Equal(CassetteMode.Passthrough, CassetteModeResolver.FromEnvironment(variableName));

            Environment.SetEnvironmentVariable(variableName, null);
            Assert.Equal(CassetteMode.Replay, CassetteModeResolver.FromEnvironment(variableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [Fact]
    public async Task ReplayMode_UsesStoredCassette_WithDeterministicKeyAsync()
    {
        string cassetteDirectory = CreateTempDirectory();
        CassetteStore store = new(cassetteDirectory);
        Uri requestUri = new("https://example.test/search?b=2&a=1");
        string key = CassetteStore.BuildKey(HttpMethod.Get, requestUri);

        await store.WriteAsync(key, new CassetteEnvelope
        {
            Key = key,
            Request = new CassetteRequest
            {
                Method = "GET",
                Url = requestUri.AbsoluteUri
            },
            Response = new CassetteResponse
            {
                StatusCode = 200,
                ReasonPhrase = "OK",
                Headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = ["application/json"]
                },
                BodyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"))
            }
        });

        using HttpClient client = new(new CassetteDelegatingHandler(store, CassetteMode.Replay));
        HttpResponseMessage response = await client.GetAsync("https://example.test/search?a=1&b=2");
        string payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", payload);
    }

    [Fact]
    public async Task ReplayMode_MissingCassette_ThrowsFailFastErrorAsync()
    {
        string cassetteDirectory = CreateTempDirectory();
        CassetteStore store = new(cassetteDirectory);
        using HttpClient client = new(new CassetteDelegatingHandler(store, CassetteMode.Replay));

        CassetteNotFoundException exception = await Assert.ThrowsAsync<CassetteNotFoundException>(
            () => client.GetAsync("https://example.test/search?q=missing"));

        Assert.Contains("GHOST_CASSETTES=record", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordMode_RedactsSensitiveHeadersAndQueryParametersAsync()
    {
        string cassetteDirectory = CreateTempDirectory();
        CassetteStore store = new(cassetteDirectory);

        using CassetteDelegatingHandler handler = new(store, CassetteMode.Record)
        {
            InnerHandler = new StaticResponseHandler(
                HttpStatusCode.OK,
                "{\"result\":\"ok\"}",
                "Set-Cookie",
                "session=super-secret")
        };

        using HttpClient client = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/jobs?api_key=plain-secret&q=dev");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer super-secret-token");

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string key = CassetteStore.BuildKey(HttpMethod.Get, new Uri("https://example.test/jobs?api_key=plain-secret&q=dev"));
        CassetteEnvelope? cassette = await store.ReadAsync(key);

        Assert.NotNull(cassette);
        Assert.Contains("api_key=%5BREDACTED%5D", cassette.Request.Url, StringComparison.Ordinal);
        Assert.Equal(CassetteRedactor.RedactedValue, cassette.Request.Headers["Authorization"].Single());
        Assert.Equal(CassetteRedactor.RedactedValue, cassette.Response.Headers["Set-Cookie"].Single());

        string cassetteJson = await File.ReadAllTextAsync(store.GetPath(key));
        Assert.DoesNotContain("super-secret-token", cassetteJson, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-secret", cassetteJson, StringComparison.Ordinal);
        Assert.DoesNotContain("session=super-secret", cassetteJson, StringComparison.Ordinal);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ghost-cassettes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly string? _headerName;
        private readonly string? _headerValue;

        public StaticResponseHandler(
            HttpStatusCode statusCode,
            string responseBody,
            string? headerName = null,
            string? headerValue = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _headerName = headerName;
            _headerValue = headerValue;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };

            if (!string.IsNullOrWhiteSpace(_headerName) && !string.IsNullOrWhiteSpace(_headerValue))
            {
                response.Headers.TryAddWithoutValidation(_headerName, _headerValue);
            }

            return Task.FromResult(response);
        }
    }
}
