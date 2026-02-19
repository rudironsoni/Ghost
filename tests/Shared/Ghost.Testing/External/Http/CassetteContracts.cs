namespace Ghost.Testing.External.Http;

public sealed class CassetteEnvelope
{
    public string Key { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public CassetteRequest Request { get; set; } = new();

    public CassetteResponse Response { get; set; } = new();
}

public sealed class CassetteRequest
{
    public string Method { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public Dictionary<string, List<string>> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CassetteResponse
{
    public int StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public Dictionary<string, List<string>> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string BodyBase64 { get; set; } = string.Empty;
}
