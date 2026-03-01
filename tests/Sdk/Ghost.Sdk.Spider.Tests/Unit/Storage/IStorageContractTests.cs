using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Contract tests for IStorage implementations.
/// These tests ensure all storage implementations follow the interface contract.
/// </summary>
public class IStorageContractTests : ReliabilityTestBase
{
    public IStorageContractTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public static async Task MockStorage_ShouldImplementIStorage()
    {
        // Arrange
        var storage = new MockStorage();

        // Assert
        storage.Should().BeAssignableTo<IStorage>();
        storage.Name.Should().Be("Mock");
        storage.IsAvailable.Should().BeTrue();

        // Act & Assert
        await storage.InitializeAsync();
        var context = StorageContext.Create("TestSpider");
        var result = await storage.StoreAsync(new { Test = "data" }, context);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public static async Task Storage_StoreAsync_ShouldAcceptCancellationToken()
    {
        // Arrange
        var storage = new MockStorage();
        var context = StorageContext.Create("TestSpider");
        var cts = new CancellationTokenSource();

        // Act
        var result = await storage.StoreAsync(new { Test = "data" }, context, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public static async Task Storage_StoreBatchAsync_ShouldHandleEmptyList()
    {
        // Arrange
        var storage = new MockStorage();
        var context = StorageContext.Create("TestSpider");
        var items = Array.Empty<object>();

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public static async Task Storage_InitializeAsync_ShouldBeCancellable()
    {
        // Arrange
        var storage = new MockStorage();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should complete (mock doesn't cancel)
        await storage.InitializeAsync(cts.Token);
    }

    [Fact]
    public static async Task Storage_LifecycleOperations_ShouldSucceed()
    {
        // Arrange
        var storage = new MockStorage();

        // Act & Assert
        await storage.InitializeAsync();
        await storage.FlushAsync();
        await storage.CloseAsync();
    }

    /// <summary>
    /// Mock storage for contract testing.
    /// </summary>
    private sealed class MockStorage : IStorage
    {
        public string Name => "Mock";
        public bool IsAvailable => true;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<StorageResult> StoreAsync<T>(T item, StorageContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StorageResult.CreateSuccess(1, TimeSpan.FromMilliseconds(10)));
        }

        public Task<StorageResult> StoreBatchAsync<T>(
            IEnumerable<T> items,
            StorageContext context,
            CancellationToken cancellationToken = default)
        {
            var count = items.Count();
            return Task.FromResult(StorageResult.CreateSuccess(count, TimeSpan.FromMilliseconds(10)));
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
