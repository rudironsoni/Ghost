using WireMock.Admin.Mappings;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Testing.Mocking.Profiles;

/// <summary>
/// WireMock profile for testing redirect scenarios (301, 302, circular redirects).
/// </summary>
public static class RedirectProfile
{
    /// <summary>
    /// Configures the server to simulate a single 301 permanent redirect.
    /// </summary>
    public static WireMockServer WithPermanentRedirect(
        this WireMockServer server,
        string fromPath = "/old",
        string toPath = "/new")
    {
        server
            .Given(Request.Create()
                .WithPath(fromPath)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(301)
                .WithHeader("Location", $"{server.Url}{toPath}"));

        server
            .Given(Request.Create()
                .WithPath(toPath)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Redirected content"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate a 302 temporary redirect.
    /// </summary>
    public static WireMockServer WithTemporaryRedirect(
        this WireMockServer server,
        string fromPath = "/temp",
        string toPath = "/destination")
    {
        server
            .Given(Request.Create()
                .WithPath(fromPath)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", $"{server.Url}{toPath}"));

        server
            .Given(Request.Create()
                .WithPath(toPath)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Temporary redirect target"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate a redirect chain (multiple redirects).
    /// </summary>
    public static WireMockServer WithRedirectChain(
        this WireMockServer server,
        int chainLength = 3)
    {
        for (int i = 0; i < chainLength; i++)
        {
            string currentPath = $"/redirect{i}";
            string nextPath = i < chainLength - 1 ? $"/redirect{i + 1}" : "/final";
            int statusCode = i % 2 == 0 ? 301 : 302;

            server
                .Given(Request.Create()
                    .WithPath(currentPath)
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(statusCode)
                    .WithHeader("Location", $"{server.Url}{nextPath}"));
        }

        server
            .Given(Request.Create()
                .WithPath("/final")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Final destination after redirect chain"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate a circular redirect loop.
    /// </summary>
    public static WireMockServer WithCircularRedirect(
        this WireMockServer server,
        string path1 = "/loop1",
        string path2 = "/loop2")
    {
        server
            .Given(Request.Create()
                .WithPath(path1)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", $"{server.Url}{path2}"));

        server
            .Given(Request.Create()
                .WithPath(path2)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", $"{server.Url}{path1}"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate a 307 redirect (preserves HTTP method).
    /// </summary>
    public static WireMockServer WithMethodPreservingRedirect(
        this WireMockServer server,
        string fromPath = "/post-redirect",
        string toPath = "/post-target")
    {
        server
            .Given(Request.Create()
                .WithPath(fromPath)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(307)
                .WithHeader("Location", $"{server.Url}{toPath}"));

        server
            .Given(Request.Create()
                .WithPath(toPath)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("POST preserved after redirect"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate a relative redirect (Location without full URL).
    /// </summary>
    public static WireMockServer WithRelativeRedirect(
        this WireMockServer server,
        string fromPath = "/relative",
        string toPath = "/target")
    {
        server
            .Given(Request.Create()
                .WithPath(fromPath)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", toPath)); // Relative path only

        server
            .Given(Request.Create()
                .WithPath(toPath)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Relative redirect target"));

        return server;
    }
}
