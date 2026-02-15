namespace Ghost.Plugin.Common;

/// <summary>
/// Helper methods for plugin configuration.
/// </summary>
public static class PluginConfigurationHelper
{
    /// <summary>
    /// Gets the configuration section name for an options type.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <returns>The section name.</returns>
    public static string GetSectionName<TOptions>() where TOptions : class
    {
        string typeName = typeof(TOptions).Name;
        // Remove "Options" suffix if present
        if (typeName.EndsWith("Options", StringComparison.OrdinalIgnoreCase))
        {
            return typeName.Substring(0, typeName.Length - 7);
        }
        return typeName;
    }
}
