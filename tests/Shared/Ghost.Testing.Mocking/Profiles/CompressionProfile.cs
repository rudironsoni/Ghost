using System.Globalization;
using System.IO.Compression;
using System.Text;
using WireMock.Admin.Mappings;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Testing.Mocking.Profiles;

/// <summary>
/// WireMock profile for testing compression scenarios (gzip, deflate, chunked).
/// </summary>
public static class CompressionProfile
{
    /// <summary>
    /// Configures the server to respond with gzip-compressed content.
    /// </summary>
    public static WireMockServer WithGzipCompression(
        this WireMockServer server,
        string path = "/gzip",
        string content = "This is gzip compressed content for testing")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "gzip")
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(content)); // WireMock.NET handles gzip automatically when header is set

        return server;
    }

    /// <summary>
    /// Configures the server to respond with deflate-compressed content.
    /// </summary>
    public static WireMockServer WithDeflateCompression(
        this WireMockServer server,
        string path = "/deflate",
        string content = "This is deflate compressed content for testing")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "deflate")
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(content));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with brotli-compressed content.
    /// </summary>
    public static WireMockServer WithBrotliCompression(
        this WireMockServer server,
        string path = "/brotli",
        string content = "This is brotli compressed content for testing")
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "br")
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(content));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with chunked transfer encoding.
    /// </summary>
    public static WireMockServer WithChunkedTransfer(
        this WireMockServer server,
        string path = "/chunked",
        int chunkCount = 3)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < chunkCount; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Chunk {i + 1} of {chunkCount}");
        }

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Transfer-Encoding", "chunked")
                .WithHeader("Content-Type", "text/plain")
                .WithBody(sb.ToString()));

        return server;
    }

    /// <summary>
    /// Configures the server to negotiate compression based on Accept-Encoding header.
    /// </summary>
    public static WireMockServer WithCompressionNegotiation(
        this WireMockServer server,
        string path = "/negotiated")
    {
        const string content = "Content negotiated based on Accept-Encoding header";

        // Gzip response
        server
            .Given(Request.Create()
                .WithPath(path)
                .WithHeader("Accept-Encoding", "*gzip*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "gzip")
                .WithHeader("Vary", "Accept-Encoding")
                .WithBody(content));

        // Deflate response
        server
            .Given(Request.Create()
                .WithPath(path)
                .WithHeader("Accept-Encoding", "*deflate*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "deflate")
                .WithHeader("Vary", "Accept-Encoding")
                .WithBody(content));

        // Brotli response
        server
            .Given(Request.Create()
                .WithPath(path)
                .WithHeader("Accept-Encoding", "*br*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "br")
                .WithHeader("Vary", "Accept-Encoding")
                .WithBody(content));

        // No compression
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/plain")
                .WithBody(content));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with large compressed content to test decompression performance.
    /// </summary>
    public static WireMockServer WithLargeCompressedContent(
        this WireMockServer server,
        string path = "/large",
        int sizeKb = 100)
    {
        string content = new string('A', sizeKb * 1024);

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "gzip")
                .WithHeader("Content-Type", "text/plain")
                .WithBody(content));

        return server;
    }

    /// <summary>
    /// Configures the server to respond with mixed compression (some compressed, some not).
    /// </summary>
    public static WireMockServer WithMixedCompression(this WireMockServer server)
    {
        server
            .Given(Request.Create()
                .WithPath("/compressed")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "gzip")
                .WithBody("Compressed content"));

        server
            .Given(Request.Create()
                .WithPath("/uncompressed")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Uncompressed content"));

        return server;
    }
}
