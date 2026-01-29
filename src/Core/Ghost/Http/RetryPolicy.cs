using Polly;
using Polly.Retry;
using System.Net.Http;
using System.Net;

namespace Ghost.Http;

public static class RetryPolicy
{
    public static IAsyncPolicy<HttpResponseMessage> CreatePolicy(int retries, double backoffFactor)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => (int)r.StatusCode == 429 || r.StatusCode == HttpStatusCode.InternalServerError || r.StatusCode == HttpStatusCode.BadGateway || r.StatusCode == HttpStatusCode.ServiceUnavailable || r.StatusCode == HttpStatusCode.GatewayTimeout)
            .WaitAndRetryAsync(retries, attempt => TimeSpan.FromSeconds(Math.Pow(backoffFactor, attempt)));
    }
}
