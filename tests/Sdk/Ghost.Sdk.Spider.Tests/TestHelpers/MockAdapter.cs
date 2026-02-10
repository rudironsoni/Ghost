using Ghost.Sdk.Spider.Adapters.Contracts;
using Moq;

namespace Ghost.Sdk.Spider.Tests.TestHelpers;

/// <summary>
/// Concrete implementation of AdapterOptions for testing
/// </summary>
public class MockAdapterOptions : AdapterOptions
{
}

/// <summary>
/// Mock content adapter for testing
/// </summary>
public class MockAdapter : IContentAdapter
{
    public string Name { get; set; } = "MockAdapter";
    public ContentType ContentType { get; set; } = ContentType.StaticHtml;
    public bool IsAvailable { get; set; } = true;

    public Func<Request, CancellationToken, Task<bool>>? CanHandleFunc { get; set; }
    public Func<Request, AdapterOptions, CancellationToken, Task<Response>>? ExtractFunc { get; set; }

    public List<Request> ReceivedRequests { get; } = new();

    public Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);
        return CanHandleFunc?.Invoke(request, cancellationToken) ?? Task.FromResult(true);
    }

    public Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default)
    {
        return ExtractAsync(request, new MockAdapterOptions(), cancellationToken);
    }

    public Task<Response> ExtractAsync(Request request, AdapterOptions options, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);
        return ExtractFunc?.Invoke(request, options, cancellationToken)
            ?? Task.FromResult(CreateDefaultResponse(request));
    }

    private Response CreateDefaultResponse(Request request)
    {
        var contentResult = new ContentResult
        {
            Content = "<html><body>Mock Content</body></html>",
            ContentType = ContentType.StaticHtml,
            MimeType = "text/html",
            Encoding = "utf-8",
            ContentLength = 100,
            ExtractedAt = DateTimeOffset.UtcNow,
            Success = true
        };

        return new Response(contentResult)
        {
            StatusCode = 200,
            ReasonPhrase = "OK",
            FinalUrl = request.Url,
            AdapterName = Name,
            IsSuccess = true,
            RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            RespondedAt = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Helper class for creating mock adapters using Moq
/// </summary>
public static class MockAdapterFactory
{
    public static Mock<IContentAdapter> CreateMockAdapter(
        string name = "MockAdapter",
        ContentType contentType = ContentType.StaticHtml,
        bool isAvailable = true)
    {
        var mock = new Mock<IContentAdapter>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.ContentType).Returns(contentType);
        mock.Setup(a => a.IsAvailable).Returns(isAvailable);
        return mock;
    }

    public static Mock<IContentAdapter> CreateMockAdapterWithResponse(
        string responseContent,
        int statusCode = 200,
        string name = "MockAdapter")
    {
        var mock = CreateMockAdapter(name);

        mock.Setup(a => a.CanHandleAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mock.Setup(a => a.ExtractAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request req, CancellationToken ct) => CreateResponse(req, responseContent, statusCode, name));

        mock.Setup(a => a.ExtractAsync(It.IsAny<Request>(), It.IsAny<AdapterOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request req, AdapterOptions opts, CancellationToken ct) =>
                CreateResponse(req, responseContent, statusCode, name));

        return mock;
    }

    private static Response CreateResponse(Request request, string content, int statusCode, string adapterName)
    {
        var contentResult = new ContentResult
        {
            Content = content,
            ContentType = ContentType.StaticHtml,
            MimeType = "text/html",
            Encoding = "utf-8",
            ContentLength = content.Length,
            ExtractedAt = DateTimeOffset.UtcNow,
            Success = statusCode >= 200 && statusCode < 300
        };

        return new Response(contentResult)
        {
            StatusCode = statusCode,
            ReasonPhrase = statusCode == 200 ? "OK" : "Error",
            FinalUrl = request.Url,
            AdapterName = adapterName,
            IsSuccess = statusCode >= 200 && statusCode < 300,
            RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            RespondedAt = DateTimeOffset.UtcNow
        };
    }
}
