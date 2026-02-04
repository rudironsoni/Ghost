using Ghost.Sdk.Spider.Storage.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Ghost.Sdk.Spider.Storage.Sinks;

/// <summary>
/// Storage implementation that writes data to the console.
/// </summary>
/// <remarks>
/// This storage is primarily useful for development and debugging.
/// It serializes items to JSON and writes them to the console.
/// </remarks>
public class ConsoleStorage : IStorage
{
    private readonly ILogger<ConsoleStorage>? _logger;
    private readonly JsonSerializerSettings _jsonSettings;

    /// <inheritdoc/>
    public string Name => "Console";

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleStorage"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public ConsoleStorage(ILogger<ConsoleStorage>? logger = null)
    {
        _logger = logger;
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Console storage initialized");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<StorageResult> StoreAsync<T>(
        T item,
        StorageContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var json = JsonConvert.SerializeObject(item, _jsonSettings);
            
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Spider: {context.SpiderName}");
            Console.WriteLine($"Source: {context.SourceUrl}");
            Console.WriteLine($"Time: {context.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("-".PadRight(80, '-'));
            Console.WriteLine(json);
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            stopwatch.Stop();
            return Task.FromResult(StorageResult.CreateSuccess(1, stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Failed to write item to console");
            return Task.FromResult(StorageResult.CreateFailure(
                $"Console write failed: {ex.Message}",
                ex,
                stopwatch.Elapsed));
        }
    }

    /// <inheritdoc/>
    public async Task<StorageResult> StoreBatchAsync<T>(
        IEnumerable<T> items,
        StorageContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var itemList = items.ToList();
        var count = 0;

        try
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Batch: {context.BatchId ?? "N/A"}");
            Console.WriteLine($"Spider: {context.SpiderName}");
            Console.WriteLine($"Items: {itemList.Count}");
            Console.WriteLine($"Time: {context.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            foreach (var item in itemList)
            {
                await StoreAsync(item, context, cancellationToken).ConfigureAwait(false);
                count++;
            }

            stopwatch.Stop();
            return StorageResult.CreateSuccess(count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Failed to write batch to console");
            return StorageResult.CreateFailure(
                $"Console batch write failed: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Console output is already flushed
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Console storage closed");
        return Task.CompletedTask;
    }
}
