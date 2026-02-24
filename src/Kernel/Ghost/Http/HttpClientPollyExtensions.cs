using System.Net;
using System.Net.Http;
using Polly;
using Polly.Retry;

namespace Ghost.Http;

public static class HttpClientPollyExtensions
{
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(int retries = 3, double backoffFactor = 2.0)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => (int)r.StatusCode == 429 ||
                              r.StatusCode == HttpStatusCode.InternalServerError ||
                              r.StatusCode == HttpStatusCode.BadGateway ||
                              r.StatusCode == HttpStatusCode.ServiceUnavailable ||
                              r.StatusCode == HttpStatusCode.GatewayTimeout)
            .WaitAndRetryAsync(retries, retryAttempt => TimeSpan.FromSeconds(Math.Pow(backoffFactor, retryAttempt)));
    }

    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicyWithJitter(int retries = 3, double backoffFactor = 2.0, int minDelayMs = 250, int maxDelayMs = 1500)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => (int)r.StatusCode == 429 ||
                              r.StatusCode == HttpStatusCode.InternalServerError ||
                              r.StatusCode == HttpStatusCode.BadGateway ||
                              r.StatusCode == HttpStatusCode.ServiceUnavailable ||
                              r.StatusCode == HttpStatusCode.GatewayTimeout)
            .WaitAndRetryAsync(retries,
                retryAttempt =>
                {
                    var backoffDelay = TimeSpan.FromSeconds(Math.Pow(backoffFactor, retryAttempt));
                    var jitterDelay = TimeSpan.FromMilliseconds(Random.Shared.Next(minDelayMs, maxDelayMs));
                    return backoffDelay + jitterDelay;
                });
    }
}
