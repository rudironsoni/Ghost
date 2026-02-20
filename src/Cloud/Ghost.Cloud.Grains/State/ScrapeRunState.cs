using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Contracts.Events;

namespace Ghost.Cloud.Grains.State;

[GenerateSerializer]
public sealed class ScrapeRunState
{
    [Id(0)] public string RunId { get; set; } = string.Empty;
    [Id(1)] public string EndpointId { get; set; } = string.Empty;
    [Id(2)] public Guid TenantId { get; set; }
    [Id(3)] public string Status { get; set; } = "Pending";
    [Id(4)] public string Mode { get; set; } = "async";
    [Id(5)] public string? WorkerId { get; set; }
    [Id(6)] public int ItemsDiscovered { get; set; }
    [Id(7)] public int ArtifactsCaptured { get; set; }
    [Id(8)] public DateTimeOffset StartedAt { get; set; }
    [Id(9)] public DateTimeOffset? CompletedAt { get; set; }
    [Id(10)] public string? ErrorMessage { get; set; }
    [Id(11)] public DeliveryConfig? DeliveryConfig { get; set; }
    [Id(12)] public string? ErrorCode { get; set; }

    public void Apply(ScrapeRunEvent @event)
    {
        switch (@event)
        {
            case ScrapeRunTriggered e:
                RunId = e.RunId;
                EndpointId = e.EndpointId;
                TenantId = e.TenantId;
                Mode = e.Mode;
                Status = "Pending";
                break;
            case ScrapeRunStarted:
                Status = "Running";
                break;
            case ItemDiscovered:
                ItemsDiscovered++;
                break;
            case ArtifactCaptured:
                ArtifactsCaptured++;
                break;
            case ScrapeRunCompleted:
                Status = "Completed";
                ErrorCode = null;
                ErrorMessage = null;
                CompletedAt = DateTimeOffset.UtcNow;
                break;
            case ScrapeRunFailed e:
                Status = "Failed";
                ErrorCode = e.ErrorCode;
                ErrorMessage = e.ErrorMessage;
                CompletedAt = DateTimeOffset.UtcNow;
                break;
        }
    }
}
