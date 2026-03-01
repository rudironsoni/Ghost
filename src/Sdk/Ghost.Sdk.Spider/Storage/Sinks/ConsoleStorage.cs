using System.Diagnostics;
using System.Text.Json;
using Ghost.Sdk.Spider.Storage.Contracts;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Spider.Storage.Sinks;

internal static partial class ConsoleStorageLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Console storage initialized")]
    public static partial void LogStorageInitialized(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write item to console")]
    public static partial void LogFailedToWriteItem(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write batch to console")]
    public static partial void LogFailedToWriteBatch(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Console storage closed")]
    public static partial void LogStorageClosed(this ILogger logger);
}

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
    private readonly JsonSerializerOptions _jsonOptions;

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
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogStorageInitialized();
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
            string json = JsonSerializer.Serialize(item, _jsonOptions);

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
            _logger?.LogFailedToWriteItem(ex);
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
        int count = 0;

        try
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Batch: {context.BatchId ?? "N/A"}");
            Console.WriteLine($"Spider: {context.SpiderName}");
            Console.WriteLine($"Items: {itemList.Count}");
            Console.WriteLine($"Time: {context.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            foreach (T? item in itemList)
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
            _logger?.LogFailedToWriteBatch(ex);
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
        if (_logger != null)
            ConsoleStorageLogMessages.LogStorageClosed(_logger);
        return Task.CompletedTask;
    }
}
