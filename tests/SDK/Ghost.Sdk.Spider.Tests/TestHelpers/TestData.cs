using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Tests.TestHelpers;

/// <summary>
/// Provides test data and factory methods for unit tests
/// </summary>
public static class TestData
{
    public static string SampleHtml => @"
        <html>
            <head><title>Test Page</title></head>
            <body>
                <div class=""container"">
                    <h1 id=""title"">Sample Title</h1>
                    <p class=""description"">Sample description text</p>
                    <div class=""products"">
                        <div class=""product"" data-id=""1"">
                            <h2 class=""product-name"">Product One</h2>
                            <span class=""price"">$10.00</span>
                            <p class=""description"">Description for product one.</p>
                        </div>
                        <div class=""product"" data-id=""2"">
                            <h2 class=""product-name"">Product Two</h2>
                            <span class=""price"">$20.00</span>
                            <p class=""description"">Description for product two.</p>
                        </div>
                    </div>
                </div>
            </body>
        </html>";

    public static string SampleJson => @"
        {
            ""id"": 123,
            ""name"": ""Test Item"",
            ""price"": 99.99,
            ""tags"": [""tag1"", ""tag2"", ""tag3""],
            ""metadata"": {
                ""created"": ""2026-01-01T00:00:00Z"",
                ""updated"": ""2026-02-01T00:00:00Z""
            }
        }";

    public static string SampleNestedJson => @"
        {
            ""data"": {
                ""items"": [
                    { ""id"": 1, ""title"": ""First"", ""value"": 10.5 },
                    { ""id"": 2, ""title"": ""Second"", ""value"": 20.75 },
                    { ""id"": 3, ""title"": ""Third"", ""value"": 30.0 }
                ],
                ""total"": 3
            },
            ""status"": ""success""
        }";

    public static Request CreateRequest(
        string url = "https://example.com",
        string method = "GET",
        Dictionary<string, string>? headers = null,
        string? body = null)
    {
        return new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = url,
            Method = method,
            Headers = headers ?? new Dictionary<string, string>(),
            Body = body,
            Timeout = TimeSpan.FromSeconds(30),
            ExpectedContentType = ContentType.Unknown,
            Metadata = new Dictionary<string, object>()
        };
    }

    public static Response CreateResponse(
        string url = "https://example.com",
        string content = "",
        int statusCode = 200,
        ContentType contentType = ContentType.StaticHtml)
    {
        var contentResult = new ContentResult
        {
            Content = content,
            ContentType = contentType,
            MimeType = contentType switch
            {
                ContentType.StaticHtml => "text/html",
                ContentType.Json => "application/json",
                ContentType.Xml => "application/xml",
                _ => "text/plain"
            },
            Encoding = "utf-8",
            ContentLength = content.Length,
            ExtractedAt = DateTimeOffset.UtcNow,
            Success = statusCode >= 200 && statusCode < 300
        };

        return new Response(contentResult)
        {
            StatusCode = statusCode,
            ReasonPhrase = statusCode == 200 ? "OK" : "Error",
            FinalUrl = url,
            AdapterName = "TestAdapter",
            IsSuccess = statusCode >= 200 && statusCode < 300,
            RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            RespondedAt = DateTimeOffset.UtcNow
        };
    }

    public static string GetFixturePath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Fixtures", filename);
    }

    public static async Task<string> ReadFixtureAsync(string filename)
    {
        var path = GetFixturePath(filename);
        return await File.ReadAllTextAsync(path);
    }

    public static string ReadFixture(string filename)
    {
        var path = GetFixturePath(filename);
        return File.ReadAllText(path);
    }
}
