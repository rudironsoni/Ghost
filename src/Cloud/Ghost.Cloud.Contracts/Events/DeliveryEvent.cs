namespace Ghost.Cloud.Contracts.Events;

[GenerateSerializer]
public abstract record DeliveryEvent;

[GenerateSerializer]
public record ResultsDelivered(
    [property: Id(0)] string RunId,
    [property: Id(1)] string SinkType,
    [property: Id(2)] int BatchNumber,
    [property: Id(3)] int ItemCount,
    [property: Id(4)] string? Cursor,
    [property: Id(5)] DateTimeOffset Timestamp
) : DeliveryEvent;

[GenerateSerializer]
public record DeliveryFailed(
    [property: Id(0)] string RunId,
    [property: Id(1)] string SinkType,
    [property: Id(2)] string ErrorCode,
    [property: Id(3)] string ErrorMessage,
    [property: Id(4)] DateTimeOffset Timestamp
) : DeliveryEvent;

[GenerateSerializer]
public record WebhookDispatched(
    [property: Id(0)] string RunId,
    [property: Id(1)] string WebhookUrl,
    [property: Id(2)] string EventType,
    [property: Id(3)] DateTimeOffset Timestamp
) : DeliveryEvent;
