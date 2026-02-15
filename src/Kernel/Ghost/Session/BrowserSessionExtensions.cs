using Microsoft.Playwright;

namespace Ghost.Session;

/// <summary>
/// Extension methods for working with browser sessions.
/// </summary>
public static class BrowserSessionExtensions
{
    /// <summary>
    /// Generate a script to restore localStorage data.
    /// </summary>
    /// <param name="session">The browser session containing localStorage data.</param>
    /// <returns>JavaScript code to restore localStorage.</returns>
    public static string GetLocalStorageRestoreScript(this BrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.LocalStorage.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach ((string? key, string? value) in session.LocalStorage)
        {
            // Key format: "origin::name"
            string[] parts = key.Split("::", 2);
            if (parts.Length != 2) continue;

            string name = parts[1];
            string escapedName = System.Text.Json.JsonSerializer.Serialize(name);
            string escapedValue = System.Text.Json.JsonSerializer.Serialize(value);

            lines.Add($"localStorage.setItem({escapedName}, {escapedValue});");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Generate a script to restore sessionStorage data.
    /// </summary>
    /// <param name="session">The browser session containing sessionStorage data.</param>
    /// <returns>JavaScript code to restore sessionStorage.</returns>
    public static string GetSessionStorageRestoreScript(this BrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.SessionStorage.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach ((string? key, string? value) in session.SessionStorage)
        {
            // Key format: "origin::name"
            string[] parts = key.Split("::", 2);
            if (parts.Length != 2) continue;

            string name = parts[1];
            string escapedName = System.Text.Json.JsonSerializer.Serialize(name);
            string escapedValue = System.Text.Json.JsonSerializer.Serialize(value);

            lines.Add($"sessionStorage.setItem({escapedName}, {escapedValue});");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Apply session persistence to a browser context by adding init scripts.
    /// This should be called after creating a new context and before creating pages.
    /// </summary>
    /// <param name="context">The browser context to apply session to.</param>
    /// <param name="session">The browser session to restore.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task ApplySessionAsync(this IBrowserContext context, BrowserSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);

        // Add localStorage restore script
        string localStorageScript = session.GetLocalStorageRestoreScript();
        if (!string.IsNullOrEmpty(localStorageScript))
        {
            await context.AddInitScriptAsync(localStorageScript).ConfigureAwait(false);
        }

        // Add sessionStorage restore script
        string sessionStorageScript = session.GetSessionStorageRestoreScript();
        if (!string.IsNullOrEmpty(sessionStorageScript))
        {
            await context.AddInitScriptAsync(sessionStorageScript).ConfigureAwait(false);
        }

        // Cookies are already restored via RestoreSessionAsync
    }
}
