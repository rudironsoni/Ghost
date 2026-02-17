namespace Ghost.Cloud.Contracts.Delivery;

[GenerateSerializer]
public sealed record StorageCredentials
{
    [Id(0)] public string? RoleArn { get; init; }
    [Id(1)] public string? AccessKey { get; init; }
    [Id(2)] public string? SecretKey { get; init; }
    [Id(3)] public string? SessionToken { get; init; }
    [Id(4)] public string? StorageAccount { get; init; }
}
