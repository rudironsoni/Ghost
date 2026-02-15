using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Plugin.Indeed.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedJobClientParallelTests
{
    [Fact]
    public async Task SearchJobsParallelAsync_YieldsJobsFromPages()
    {
        var handler = new SequenceHandler(new[]
        {
            ResponseWithJobs("cursor-1", true),
            ResponseWithJobs(null, false)
        });
        var api = CreateClient(handler);
        var client = new IndeedJobClient(api, NullLogger<IndeedJobClient>.Instance);

        var criteria = new JobSearchCriteria { Query = "dev", Location = "remote", MaxResults = 50 };
        int count = 0;
        await foreach (var _ in client.SearchJobsParallelAsync(criteria, CancellationToken.None))
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    private static IndeedApiClient CreateClient(HttpMessageHandler handler)
    {
        var options = new IndeedOptions { ApiKey = "test-key", Country = Ghost.Models.CountryCode.US };
        return new IndeedApiClient(
            proxyProvider: null,
            sessionOrchestrator: null,
            options: options,
            logger: NullLogger<IndeedApiClient>.Instance,
            handler: handler,
            timeProvider: TimeProvider.System);
    }

    private static HttpResponseMessage ResponseWithJobs(string? cursor, bool hasNext)
    {
        string json = JsonSerializer.Serialize(new
        {
            data = new
            {
                jobSearch = new
                {
                    pageInfo = new { nextCursor = cursor, hasNextPage = hasNext },
                    results = new[]
                    {
                        new
                        {
                            job = new
                            {
                                key = "job-1",
                                title = "Engineer",
                                employer = new { name = "ACME" },
                                location = new { formatted = new { @long = "Remote" } },
                                description = new { html = "<p>Job</p>" }
                            }
                        }
                    }
                }
            }
        });

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage[] _responses;
        private int _index;

        public SequenceHandler(HttpResponseMessage[] responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = _responses[Math.Min(_index, _responses.Length - 1)];
            _index++;
            return Task.FromResult(response);
        }
    }
}
