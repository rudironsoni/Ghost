namespace Ghost.Testing.External.Http;

public enum CassetteMode
{
    Replay = 0,
    Record = 1,
    Passthrough = 2
}

public static class CassetteModeResolver
{
    public static CassetteMode FromEnvironment(
        string variableName = "GHOST_CASSETTES",
        CassetteMode defaultMode = CassetteMode.Replay)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);

        return value?.Trim().ToLowerInvariant() switch
        {
            "replay" => CassetteMode.Replay,
            "record" => CassetteMode.Record,
            "off" => CassetteMode.Passthrough,
            "passthrough" => CassetteMode.Passthrough,
            _ => defaultMode
        };
    }
}
