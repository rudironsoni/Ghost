using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Plugin.LinkedIn.Internal;

public interface IGuestJobSearch
{
    public Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct);
    public Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct);
}
