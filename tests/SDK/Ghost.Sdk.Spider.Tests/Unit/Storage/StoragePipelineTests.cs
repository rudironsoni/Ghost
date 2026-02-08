using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for Storage Pipeline functionality.
/// These tests verify the pipeline pattern for storage operations including
/// transformation, validation, and multi-storage scenarios.
/// </summary>
[TestFixture]
public class StoragePipelineTests
{
    [Test]
    public async Task Pipeline_WithSingleStorage_ShouldStore()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var item = new { Name = "Test", Value = 42 };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
        mockStorage.StoreCount.Should().Be(1);
    }

    [Test]
    public async Task Pipeline_WithMultipleStorages_ShouldStoreToAll()
    {
        // Arrange
        var storage1 = new MockStorage();
        var storage2 = new MockStorage();
        var storage3 = new MockStorage();

        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage1);
        pipeline.AddStorage(storage2);
        pipeline.AddStorage(storage3);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("MultiStorageSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        storage1.StoreCount.Should().Be(1);
        storage2.StoreCount.Should().Be(1);
        storage3.StoreCount.Should().Be(1);
    }

    [Test]
    public async Task Pipeline_WithFailingStorage_ShouldContinueToOthers()
    {
        // Arrange
        var successStorage1 = new MockStorage();
        var failingStorage = new MockStorage { ShouldFail = true };
        var successStorage2 = new MockStorage();

        var pipeline = new StoragePipeline { ContinueOnError = true };
        pipeline.AddStorage(successStorage1);
        pipeline.AddStorage(failingStorage);
        pipeline.AddStorage(successStorage2);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("ResilientSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        // Pipeline should continue despite one failure
        successStorage1.StoreCount.Should().Be(1);
        successStorage2.StoreCount.Should().Be(1);
    }

    [Test]
    public async Task Pipeline_WithTransformation_ShouldTransformBeforeStore()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddTransformation((item, ctx) =>
        {
            // Transform by adding a timestamp field
            dynamic transformed = new System.Dynamic.ExpandoObject();
            var dict = (IDictionary<string, object>)transformed;

            foreach (var prop in item.GetType().GetProperties())
            {
                dict[prop.Name] = prop.GetValue(item)!;
            }
            dict["TransformedAt"] = DateTime.UtcNow;

            return Task.FromResult<object>(transformed);
        });
        pipeline.AddStorage(mockStorage);

        var item = new { Name = "Original" };
        var context = StorageContext.Create("TransformSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        mockStorage.StoreCount.Should().Be(1);
        mockStorage.LastStoredItem.Should().NotBeNull();
    }

    [Test]
    public async Task Pipeline_WithBatch_ShouldBatchStore()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var items = new[]
        {
            new { Id = 1, Name = "Item1" },
            new { Id = 2, Name = "Item2" },
            new { Id = 3, Name = "Item3" }
        };
        var context = new StorageContext
        {
            SpiderName = "BatchSpider",
            BatchId = "batch-001"
        };

        // Act
        var result = await pipeline.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
        mockStorage.BatchStoreCount.Should().Be(1);
    }

    [Test]
    public async Task Pipeline_WithEmptyPipeline_ShouldReturnSuccess()
    {
        // Arrange
        var pipeline = new StoragePipeline();
        var item = new { Name = "Test" };
        var context = StorageContext.Create("EmptyPipelineSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Test]
    public async Task Pipeline_WithFilter_ShouldFilterItems()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();

        // Filter: only store items with Value > 50
        pipeline.AddFilter((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            return Task.FromResult(dynamicItem?.Value > 50);
        });
        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("FilterSpider");

        // Act
        var result1 = await pipeline.StoreAsync(new { Value = 30 }, context);
        var result2 = await pipeline.StoreAsync(new { Value = 75 }, context);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        mockStorage.StoreCount.Should().Be(1); // Only second item stored
    }

    [Test]
    public async Task Pipeline_WithMetricsCollection_ShouldCollectMetrics()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline { CollectMetrics = true };
        pipeline.AddStorage(mockStorage);

        var items = Enumerable.Range(1, 10).Select(i => new { Id = i }).ToArray();
        var context = StorageContext.Create("MetricsSpider");

        // Act
        foreach (var item in items)
        {
            await pipeline.StoreAsync(item, context);
        }

        // Assert
        var metrics = pipeline.GetMetrics();
        metrics.Should().NotBeNull();
        metrics.TotalItems.Should().Be(10);
        metrics.SuccessCount.Should().Be(10);
        metrics.FailureCount.Should().Be(0);
    }

    [Test]
    public async Task Pipeline_WithCancellation_ShouldCancelGracefully()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var item = new { Name = "Test" };
        var context = StorageContext.Create("CancelSpider");

        // Act
        var act = async () => await pipeline.StoreAsync(item, context, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task Pipeline_WithValidation_ShouldValidateBeforeStore()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();

        pipeline.AddValidator((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            var nameValue = dynamicItem?.Name?.ToString();
            var isValid = !string.IsNullOrEmpty(nameValue);
            return Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                ErrorMessage = isValid ? null : "Name is required"
            });
        });
        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("ValidationSpider");

        // Act
        var validResult = await pipeline.StoreAsync(new { Name = "Valid" }, context);
        var invalidResult = await pipeline.StoreAsync(new { Name = "" }, context);

        // Assert
        validResult.Success.Should().BeTrue();
        mockStorage.StoreCount.Should().Be(1);
    }

    [Test]
    public async Task Pipeline_Initialization_ShouldInitializeAllStorages()
    {
        // Arrange
        var storage1 = new MockStorage();
        var storage2 = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage1);
        pipeline.AddStorage(storage2);

        // Act
        await pipeline.InitializeAsync();

        // Assert
        storage1.IsInitialized.Should().BeTrue();
        storage2.IsInitialized.Should().BeTrue();
    }

    [Test]
    public async Task Pipeline_Flush_ShouldFlushAllStorages()
    {
        // Arrange
        var storage1 = new MockStorage();
        var storage2 = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage1);
        pipeline.AddStorage(storage2);

        // Act
        await pipeline.FlushAsync();

        // Assert
        storage1.IsFlushed.Should().BeTrue();
        storage2.IsFlushed.Should().BeTrue();
    }

    [Test]
    public async Task Pipeline_Close_ShouldCloseAllStorages()
    {
        // Arrange
        var storage1 = new MockStorage();
        var storage2 = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage1);
        pipeline.AddStorage(storage2);

        // Act
        await pipeline.CloseAsync();

        // Assert
        storage1.IsClosed.Should().BeTrue();
        storage2.IsClosed.Should().BeTrue();
    }

    #region Test Helpers

    private class StoragePipeline : IStorage
    {
        private readonly List<IStorage> _storages = new();
        private readonly List<Func<object, StorageContext, Task<object>>> _transformations = new();
        private readonly List<Func<object, StorageContext, Task<bool>>> _filters = new();
        private readonly List<Func<object, StorageContext, Task<ValidationResult>>> _validators = new();
        private PipelineMetrics _metrics = new();

        public string Name => "Pipeline";
        public bool IsAvailable => _storages.Any();
        public bool ContinueOnError { get; set; } = false;
        public bool CollectMetrics { get; set; } = false;

        public void AddStorage(IStorage storage) => _storages.Add(storage);
        public void AddTransformation(Func<object, StorageContext, Task<object>> transform) => _transformations.Add(transform);
        public void AddFilter(Func<object, StorageContext, Task<bool>> filter) => _filters.Add(filter);
        public void AddValidator(Func<object, StorageContext, Task<ValidationResult>> validator) => _validators.Add(validator);
        public PipelineMetrics GetMetrics() => _metrics;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var storage in _storages)
            {
                await storage.InitializeAsync(cancellationToken);
            }
        }

        public async Task<StorageResult> StoreAsync<T>(T item, StorageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startTime = DateTimeOffset.UtcNow;

            try
            {
                object transformedItem = item!;

                // Apply validations
                foreach (var validator in _validators)
                {
                    var validationResult = await validator(transformedItem, context);
                    if (!validationResult.IsValid)
                    {
                        if (CollectMetrics) _metrics.FailureCount++;
                        return StorageResult.CreateFailure(validationResult.ErrorMessage ?? "Validation failed", null, DateTimeOffset.UtcNow - startTime);
                    }
                }

                // Apply filters
                foreach (var filter in _filters)
                {
                    if (!await filter(transformedItem, context))
                    {
                        if (CollectMetrics) _metrics.FilteredCount++;
                        return StorageResult.CreateSuccess(0, DateTimeOffset.UtcNow - startTime);
                    }
                }

                // Apply transformations
                foreach (var transformation in _transformations)
                {
                    transformedItem = await transformation(transformedItem, context);
                }

                if (!_storages.Any())
                {
                    return StorageResult.CreateSuccess(0, DateTimeOffset.UtcNow - startTime);
                }

                // Store to all storages
                var tasks = _storages.Select(s => s.StoreAsync(transformedItem, context, cancellationToken));
                var results = await Task.WhenAll(tasks);

                var success = ContinueOnError || results.All(r => r.Success);
                var itemsStored = success ? 1 : 0;

                if (CollectMetrics)
                {
                    _metrics.TotalItems++;
                    if (success) _metrics.SuccessCount++;
                    else _metrics.FailureCount++;
                }

                return StorageResult.CreateSuccess(itemsStored, DateTimeOffset.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                if (CollectMetrics) _metrics.FailureCount++;
                return StorageResult.CreateFailure(ex.Message, ex, DateTimeOffset.UtcNow - startTime);
            }
        }

        public async Task<StorageResult> StoreBatchAsync<T>(IEnumerable<T> items, StorageContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;
            var itemList = items.ToList();

            if (!_storages.Any())
            {
                return StorageResult.CreateSuccess(0, DateTimeOffset.UtcNow - startTime);
            }

            var tasks = _storages.Select(s => s.StoreBatchAsync(itemList, context, cancellationToken));
            var results = await Task.WhenAll(tasks);

            var success = ContinueOnError || results.All(r => r.Success);
            var itemsStored = success ? itemList.Count : 0;

            if (CollectMetrics)
            {
                _metrics.TotalItems += itemList.Count;
                if (success) _metrics.SuccessCount += itemList.Count;
                else _metrics.FailureCount += itemList.Count;
            }

            return StorageResult.CreateSuccess(itemsStored, DateTimeOffset.UtcNow - startTime);
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            foreach (var storage in _storages)
            {
                await storage.FlushAsync(cancellationToken);
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            foreach (var storage in _storages)
            {
                await storage.CloseAsync(cancellationToken);
            }
        }
    }

    private class MockStorage : IStorage
    {
        public string Name => "Mock";
        public bool IsAvailable => true;
        public bool ShouldFail { get; set; } = false;
        public int StoreCount { get; private set; } = 0;
        public int BatchStoreCount { get; private set; } = 0;
        public object? LastStoredItem { get; private set; }
        public bool IsInitialized { get; private set; } = false;
        public bool IsFlushed { get; private set; } = false;
        public bool IsClosed { get; private set; } = false;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<StorageResult> StoreAsync<T>(T item, StorageContext context, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult(StorageResult.CreateFailure("Mock failure", null, TimeSpan.FromMilliseconds(10)));
            }

            StoreCount++;
            LastStoredItem = item;
            return Task.FromResult(StorageResult.CreateSuccess(1, TimeSpan.FromMilliseconds(10)));
        }

        public Task<StorageResult> StoreBatchAsync<T>(IEnumerable<T> items, StorageContext context, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult(StorageResult.CreateFailure("Mock failure", null, TimeSpan.FromMilliseconds(10)));
            }

            BatchStoreCount++;
            var count = items.Count();
            return Task.FromResult(StorageResult.CreateSuccess(count, TimeSpan.FromMilliseconds(10)));
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            IsFlushed = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsClosed = true;
            return Task.CompletedTask;
        }
    }

    private class PipelineMetrics
    {
        public int TotalItems { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int FilteredCount { get; set; }
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
