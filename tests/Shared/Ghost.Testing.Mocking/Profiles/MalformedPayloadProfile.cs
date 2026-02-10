using System.Globalization;
using WireMock.Admin.Mappings;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Testing.Mocking.Profiles;

/// <summary>
/// WireMock profile for testing malformed payload scenarios (invalid JSON, HTML).
/// </summary>
public static class MalformedPayloadProfile
{
    /// <summary>
    /// Configures the server to respond with invalid JSON.
    /// </summary>
    public static WireMockServer WithInvalidJson(
        this WireMockServer server,
        string path = "/invalid-json")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"key\": \"value\", invalid json {{{"));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with incomplete JSON.
    /// </summary>
    public static WireMockServer WithIncompleteJson(
        this WireMockServer server,
        string path = "/incomplete-json")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"key\": \"value\", \"nested\": {\"incomplete\":"));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with malformed HTML (unclosed tags).
    /// </summary>
    public static WireMockServer WithMalformedHtml(
        this WireMockServer server,
        string path = "/malformed-html")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html")
                .WithBody("<html><body><div><p>Unclosed tags<div></body>"));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with empty body but claims content type.
    /// </summary>
    public static WireMockServer WithEmptyBody(
        this WireMockServer server,
        string path = "/empty",
        string contentType = "application/json")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", contentType)
                .WithBody(string.Empty));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with mismatched content type.
    /// </summary>
    public static WireMockServer WithMismatchedContentType(
        this WireMockServer server,
        string path = "/mismatched")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("<html><body>This is HTML, not JSON</body></html>"));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with invalid XML.
    /// </summary>
    public static WireMockServer WithInvalidXml(
        this WireMockServer server,
        string path = "/invalid-xml")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/xml")
                .WithBody("<?xml version=\"1.0\"?><root><item>Unclosed"));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with binary data claiming to be text.
    /// </summary>
    public static WireMockServer WithBinaryAsText(
        this WireMockServer server,
        string path = "/binary-as-text")
    {
        var binaryData = new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x02, 0x03 };
        var corruptedText = System.Text.Encoding.UTF8.GetString(binaryData);

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/plain")
                .WithBody(corruptedText));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with truncated response (incomplete transfer).
    /// </summary>
    public static WireMockServer WithTruncatedResponse(
        this WireMockServer server,
        string path = "/truncated")
    {
        var largeContent = new string('A', 10000);
        var truncatedContent = largeContent[..100]; // First 100 chars only

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Length", largeContent.Length.ToString(CultureInfo.InvariantCulture))
                .WithHeader("Content-Type", "text/plain")
                .WithBody(truncatedContent)); // Body shorter than Content-Length

        return server;
    }

    /// <summary>
    /// Configures the server to respond with various GraphQL error formats.
    /// </summary>
    public static WireMockServer WithMalformedGraphQL(this WireMockServer server)
    {
        // Invalid GraphQL response (missing data field)
        server
            .Given(Request.Create()
                .WithPath("/graphql/missing-data")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"errors\": []}"));

        // Malformed errors array
        server
            .Given(Request.Create()
                .WithPath("/graphql/malformed-errors")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"data\": null, \"errors\": \"not an array\"}"));

        // Invalid JSON in GraphQL response
        server
            .Given(Request.Create()
                .WithPath("/graphql/invalid-json")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{data: null errors: []}"));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with charset encoding issues.
    /// </summary>
    public static WireMockServer WithEncodingIssues(
        this WireMockServer server,
        string path = "/encoding")
    {
        // Claims UTF-8 but sends invalid UTF-8 sequences
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("Valid text followed by \xC0\xC1 invalid UTF-8"));

        return server;
    }
}
