using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ghost.Plugin.Google.Jobs.Internal;

internal static class GoogleJobsApiClientProxyHelpers
{
    // Sends a GET request using a proxy (http proxy string like http://ip:port)
    public static async Task<string?> SendRequestUsingProxyAsync(string url, string proxy, IWebProxy? defaultProxy = null)
    {
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy),
            UseProxy = true,
            AllowAutoRedirect = true,
        };

        using var client = new HttpClient(handler, disposeHandler: true);
        client.Timeout = System.TimeSpan.FromSeconds(20);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        HttpResponseMessage res = await client.SendAsync(req).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            return null;

        return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
}
