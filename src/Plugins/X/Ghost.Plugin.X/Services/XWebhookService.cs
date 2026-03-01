
namespace Ghost.Plugin.X.Services;

/// <summary>
/// Service for handling X platform webhooks and callbacks.
/// </summary>
public interface IXWebhookService
{
    public void RegisterCallback(string eventName, Func<XEventArgs, Task> callback);
    public void UnregisterCallback(string eventName, Func<XEventArgs, Task> callback);
    public Task NotifyAsync(string eventName, XEventArgs args);
}

/// <summary>
/// Event arguments for X platform events.
/// </summary>
public class XEventArgs : EventArgs
{
    /// <summary>
    /// Gets the timestamp of the event.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the operation that triggered the event.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets the post ID (if applicable).
    /// </summary>
    public string? PostId { get; set; }

    /// <summary>
    /// Gets the error message (if operation failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the duration of the operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets additional metadata about the event.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Implementation of webhook service.
/// </summary>
public class XWebhookService : IXWebhookService
{
    private readonly Dictionary<string, List<Func<XEventArgs, Task>>> _callbacks = new();
    private readonly object _lock = new();

    public void RegisterCallback(string eventName, Func<XEventArgs, Task> callback)
    {
        lock (_lock)
        {
            if (!_callbacks.TryGetValue(eventName, out List<Func<XEventArgs, Task>>? callbacks))
            {
                callbacks = new List<Func<XEventArgs, Task>>();
                _callbacks[eventName] = callbacks;
            }
            callbacks.Add(callback);
        }
    }

    public void UnregisterCallback(string eventName, Func<XEventArgs, Task> callback)
    {
        lock (_lock)
        {
            if (_callbacks.TryGetValue(eventName, out List<Func<XEventArgs, Task>>? callbacks))
            {
                callbacks.Remove(callback);
            }
        }
    }

    public async Task NotifyAsync(string eventName, XEventArgs args)
    {
        List<Func<XEventArgs, Task>>? callbacks;

        lock (_lock)
        {
            if (!_callbacks.TryGetValue(eventName, out List<Func<XEventArgs, Task>>? eventCallbacks))
            {
                return;
            }
            callbacks = eventCallbacks.ToList();
        }

        IEnumerable<Task> tasks = callbacks.Select(callback => ExecuteCallbackAsync(callback, args));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task ExecuteCallbackAsync(Func<XEventArgs, Task> callback, XEventArgs args)
    {
        try
        {
            await callback(args).ConfigureAwait(false);
        }
        catch
        {
            // Don't let callback exceptions break the flow
            // Could add logging here
        }
    }
}

/// <summary>
/// Predefined event names for X platform.
/// </summary>
public static class XEventNames
{
    public const string PostCreated = "PostCreated";
    public const string PostFailed = "PostFailed";
    public const string ThreadCreated = "ThreadCreated";
    public const string AuthFailed = "AuthFailed";
    public const string RateLimited = "RateLimited";
    public const string BrowserError = "BrowserError";
}
