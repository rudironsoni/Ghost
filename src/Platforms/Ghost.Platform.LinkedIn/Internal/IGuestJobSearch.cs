using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.LinkedIn.Internal;

public interface IGuestJobSearch
{
    Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct);
    Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct);
}
