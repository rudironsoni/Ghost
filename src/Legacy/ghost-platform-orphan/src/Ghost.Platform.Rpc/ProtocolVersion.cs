namespace Ghost.Platform.Rpc;

/// <summary>
/// Protocol version for executor RPC communication.
/// </summary>
public static class ProtocolVersion
{
    /// <summary>
    /// Current protocol version.
    /// </summary>
    public const string Current = "1.0.0";

    /// <summary>
    /// Minimum supported protocol version.
    /// </summary>
    public const string MinimumSupported = "1.0.0";

    /// <summary>
    /// Checks if a version is compatible with the current protocol.
    /// </summary>
    public static bool IsCompatible(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Simple version comparison - in production, use proper semantic versioning
        var currentParts = Current.Split('.');
        var requestedParts = version.Split('.');

        if (requestedParts.Length < 2)
            return false;

        // Major version must match
        if (currentParts[0] != requestedParts[0])
            return false;

        // Minor version must be at least the minimum supported
        var minParts = MinimumSupported.Split('.');
        if (int.TryParse(requestedParts[1], out var requestedMinor) &&
            int.TryParse(minParts[1], out var minMinor))
        {
            return requestedMinor >= minMinor;
        }

        return false;
    }
}
