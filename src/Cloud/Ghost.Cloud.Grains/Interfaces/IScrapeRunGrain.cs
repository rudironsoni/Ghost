using Ghost.Cloud.Contracts.Endpoints;
using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Grains.Interfaces;

public interface IScrapeRunGrain : IGrainWithStringKey
{
    public Task<ScrapeRunStatus> TriggerAsync(ScrapeRunRequest request);
    public Task StartAsync(string workerId);
    public Task ReportItemAsync(string itemId, JsonElement data);
    public Task ReportArtifactAsync(string itemId, string artifactType, string storageUri, string hash);
    public Task CompleteAsync(int itemsDiscovered, int artifactsCaptured);
    public Task FailAsync(string errorCode, string errorMessage, bool retryable);
    public Task<ScrapeRunStatus> GetStatusAsync();
    public Task CancelAsync();
}
