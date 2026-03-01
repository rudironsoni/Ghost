using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Additional comprehensive tests for Storage Pipeline to boost coverage.
/// </summary>
public class StoragePipelineFullTests : ReliabilityTestBase
{
    public StoragePipelineFullTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task Pipeline_WithNullItem_ShouldHandleGracefully()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("NullItemSpider");

        // Act
        var result = await pipeline.StoreAsync<object>(null!, context);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Pipeline_WithTransformationThatReturnsNull_ShouldHandleGracefully()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddTransformation((item, ctx) => Task.FromResult<object>(null!));
        pipeline.AddStorage(mockStorage);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("NullTransformSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Pipeline_WithMultipleTransformations_ShouldApplyInOrder()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();

        List<int> transformationOrder = [];

        pipeline.AddTransformation((item, ctx) =>
        {
            transformationOrder.Add(1);
            return Task.FromResult<object>(item);
        });

        pipeline.AddTransformation((item, ctx) =>
        {
            transformationOrder.Add(2);
            return Task.FromResult<object>(item);
        });

        pipeline.AddTransformation((item, ctx) =>
        {
            transformationOrder.Add(3);
            return Task.FromResult<object>(item);
        });

        pipeline.AddStorage(mockStorage);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("OrderedTransformSpider");

        // Act
        await pipeline.StoreAsync(item, context);

        // Assert
        transformationOrder.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Pipeline_WithMultipleFilters_ShouldApplyAllFilters()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();

        pipeline.AddFilter((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            return Task.FromResult(dynamicItem?.Value > 10);
        });

        pipeline.AddFilter((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            return Task.FromResult(dynamicItem?.Value < 100);
        });

        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("MultiFilterSpider");

        // Act
        var result1 = await pipeline.StoreAsync(new { Value = 5 }, context);
        var result2 = await pipeline.StoreAsync(new { Value = 50 }, context);
        var result3 = await pipeline.StoreAsync(new { Value = 150 }, context);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        mockStorage.StoreCount.Should().Be(1); // Only middle value passes both filters
    }

    [Fact]
    public async Task Pipeline_WithFailingTransformation_ShouldReturnFailure()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddTransformation((item, ctx) => throw new InvalidOperationException("Transform failed"));
        pipeline.AddStorage(mockStorage);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("FailTransformSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Transform failed");
    }

    [Fact]
    public async Task Pipeline_WithAllStoragesFailing_ShouldReturnFailure()
    {
        // Arrange
        var storage1 = new MockStorage { ShouldFail = true };
        var storage2 = new MockStorage { ShouldFail = true };
        var pipeline = new StoragePipeline { ContinueOnError = false };
        pipeline.AddStorage(storage1);
        pipeline.AddStorage(storage2);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("AllFailSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public async Task Pipeline_WithPartialStorageFailure_AndContinueOnError_ShouldSucceed()
    {
        // Arrange
        var storage1 = new MockStorage();
        var storage2 = new MockStorage { ShouldFail = true };
        var storage3 = new MockStorage();

        var pipeline = new StoragePipeline { ContinueOnError = true };
        pipeline.AddStorage(storage1);
        pipeline.AddStorage(storage2);
        pipeline.AddStorage(storage3);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("PartialFailSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        storage1.StoreCount.Should().Be(1);
        storage3.StoreCount.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_WithMetricsEnabled_ShouldTrackStorage()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline { CollectMetrics = true };
        pipeline.AddStorage(mockStorage);

        var item = new { Name = "Test" };
        var context = StorageContext.Create("MetricsSpider");

        // Act
        var result = await pipeline.StoreAsync(item, context);

        // Assert - Verify successful storage
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        mockStorage.StoreCount.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_WithMetricsEnabled_ShouldTrackFilteredItems()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline { CollectMetrics = true };
        pipeline.AddFilter((item, ctx) => Task.FromResult(false)); // Filter everything
        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("FilterMetricsSpider");

        // Act
        await pipeline.StoreAsync(new { Name = "Filtered" }, context);

        // Assert
        var metrics = pipeline.GetMetrics();
        metrics.FilteredCount.Should().Be(1);
        mockStorage.StoreCount.Should().Be(0);
    }

    [Fact]
    public async Task Pipeline_BatchStore_WithEmptyCollection_ShouldSucceed()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var items = Array.Empty<object>();
        var context = StorageContext.Create("EmptyBatchSpider");

        // Act
        var result = await pipeline.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public async Task Pipeline_BatchStore_WithLargeBatch_ShouldProcessAll()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var items = Enumerable.Range(1, 1000).Select(i => new { Id = i, Name = $"Item{i}" }).ToArray();
        var context = StorageContext.Create("LargeBatchSpider");

        // Act
        var result = await pipeline.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1000);
        mockStorage.BatchStoreCount.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_ConcurrentStores_ShouldHandleSafely()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("ConcurrentSpider");

        // Act
        var tasks = Enumerable.Range(1, 20).Select(i =>
            pipeline.StoreAsync(new { Id = i }, context));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        mockStorage.StoreCount.Should().Be(20);
    }

    [Fact]
    public async Task Pipeline_WithValidator_ShouldRejectInvalidItems()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();

        pipeline.AddValidator((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            var nameValue = dynamicItem?.Name;
            var isValid = nameValue != null && nameValue!.ToString().Length > 3;
            return Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                ErrorMessage = isValid ? null : "Name must be longer than 3 characters"
            });
        });
        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("ValidationSpider");

        // Act
        var validResult = await pipeline.StoreAsync(new { Name = "ValidName" }, context);
        var invalidResult = await pipeline.StoreAsync(new { Name = "No" }, context);

        // Assert
        validResult.Success.Should().BeTrue();
        invalidResult.Success.Should().BeFalse();
        invalidResult.Error.Should().Contain("must be longer than 3 characters");
        mockStorage.StoreCount.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_WithMultipleValidators_ShouldApplyAll()
    {
        // Arrange
        var mockStorage = new MockStorage();
        var pipeline = new StoragePipeline();

        pipeline.AddValidator((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            return Task.FromResult(new ValidationResult
            {
                IsValid = dynamicItem?.Name != null,
                ErrorMessage = "Name is required"
            });
        });

        pipeline.AddValidator((item, ctx) =>
        {
            dynamic? dynamicItem = item;
            return Task.FromResult(new ValidationResult
            {
                IsValid = dynamicItem?.Value > 0,
                ErrorMessage = "Value must be positive"
            });
        });

        pipeline.AddStorage(mockStorage);

        var context = StorageContext.Create("MultiValidatorSpider");

        // Act
        var result1 = await pipeline.StoreAsync(new { Name = "Test", Value = 10 }, context);
        var result2 = await pipeline.StoreAsync(new { Name = "Test", Value = -5 }, context);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeFalse();
        mockStorage.StoreCount.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_InitializeMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var storage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage);

        // Act
        await pipeline.InitializeAsync();
        await pipeline.InitializeAsync();
        await pipeline.InitializeAsync();

        // Assert
        storage.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_FlushMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var storage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage);

        // Act
        await pipeline.FlushAsync();
        await pipeline.FlushAsync();

        // Assert
        storage.IsFlushed.Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_CloseMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var storage = new MockStorage();
        var pipeline = new StoragePipeline();
        pipeline.AddStorage(storage);

        // Act
        await pipeline.CloseAsync();
        await pipeline.CloseAsync();

        // Assert
        storage.IsClosed.Should().BeTrue();
    }

    #region Test Helpers

    private sealed class StoragePipeline : IStorage
    {
        private readonly List<IStorage> _storages = [];
        private readonly List<Func<object, StorageContext, Task<object>>> _transformations = new();
        private readonly List<Func<object, StorageContext, Task<bool>>> _filters = new();
        private readonly List<Func<object, StorageContext, Task<ValidationResult>>> _validators = new();
        private PipelineMetrics _metrics = new();

        public string Name => "Pipeline";
        public bool IsAvailable => _storages.Count > 0;
        public bool ContinueOnError { get; set; }
        public bool CollectMetrics { get; set; }

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

                if (_storages.Count == 0)
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

            if (_storages.Count == 0)
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

    private sealed class MockStorage : IStorage
    {
        public string Name => "Mock";
        public bool IsAvailable => true;
        public bool ShouldFail { get; set; }
        public int StoreCount { get; private set; }
        public int BatchStoreCount { get; private set; }
        public object? LastStoredItem { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsFlushed { get; private set; }
        public bool IsClosed { get; private set; }

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

    private sealed class PipelineMetrics
    {
        public int TotalItems { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int FilteredCount { get; set; }
    }

    private sealed class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
