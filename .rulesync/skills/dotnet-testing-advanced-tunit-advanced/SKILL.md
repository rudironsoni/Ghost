---
name: dotnet-testing-advanced-tunit-advanced
description: |
  Complete guide for TUnit advanced applications. Use when using TUnit for data-driven testing, dependency injection, or integration testing. Covers MethodDataSource, ClassDataSource, Matrix Tests, Properties filtering. Includes Retry/Timeout control, WebApplicationFactory integration, Testcontainers multi-service orchestration.
  Keywords: TUnit advanced, TUnit advanced, MethodDataSource, ClassDataSource, Matrix Tests, MatrixDataSource, MicrosoftDependencyInjectionDataSource, Property, Retry, Timeout, data-driven testing, test filtering, WebApplicationFactory TUnit, multi-container orchestration
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: 'tunit, advanced-testing, data-driven, dependency-injection, integration-testing, testcontainers'
  related_skills: 'advanced-tunit-fundamentals, advanced-webapi-integration-testing'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# TUnit Advanced Applications: Data-Driven Testing, Dependency Injection, and Integration Testing

## Applicable Scenarios

This skill covers TUnit advanced application techniques, from data-driven testing to dependency injection, from
execution control to ASP.NET Core integration testing practice.

### Core Topics

- Data-driven testing advanced techniques (MethodDataSource, ClassDataSource, Matrix Tests)
- Properties attribute marking and test filtering
- Test lifecycle and dependency injection
- Execution control (Retry, Timeout, DisplayName)
- ASP.NET Core integration testing (WebApplicationFactory)
- Performance testing and load testing
- TUnit + Testcontainers complex infrastructure orchestration
- TUnit Engine Modes and troubleshooting

---

## Data-Driven Testing Advanced Techniques

TUnit provides MethodDataSource, ClassDataSource, and Matrix Tests as three advanced data sources. MethodDataSource is
most flexible, supporting dynamic generation and external file loading; ClassDataSource is suitable for cross-test class
data sharing and AutoFixture integration; Matrix Tests automatically generates all parameter combinations (note: control
quantity to avoid explosive growth).

> Full examples and comparison table please refer to
> [references/data-driven-testing.md](references/data-driven-testing.md)

---

## Properties Attribute Marking and Test Filtering

### Basic Properties Usage

````csharp
[Test]
[Property("Category", "Database")]
[Property("Priority", "High")]
public async Task DatabaseTest_HighPriority_ShouldBeFilterableByAttribute()
{
    await Assert.That(true).IsTrue();
}

[Test]
[Property("Category", "Unit")]
[Property("Priority", "Medium")]
public async Task UnitTest_MediumPriority_BasicValidation()
{
    await Assert.That(1 + 1).IsEqualTo(2);
}

[Test]
[Property("Category", "Integration")]
[Property("Priority", "Low")]
[Property("Environment", "Development")]
public async Task IntegrationTest_LowPriority_OnlyRunInDevEnvironment()
{
    await Assert.That("Hello World").Contains("World");
}
```text

### Establishing Consistent Attribute Naming Conventions

```csharp
public static class TestProperties
{
    // Test categories
    public const string CATEGORY_UNIT = "Unit";
    public const string CATEGORY_INTEGRATION = "Integration";
    public const string CATEGORY_E2E = "E2E";

    // Priority levels
    public const string PRIORITY_CRITICAL = "Critical";
    public const string PRIORITY_HIGH = "High";
    public const string PRIORITY_MEDIUM = "Medium";
    public const string PRIORITY_LOW = "Low";

    // Environments
    public const string ENV_DEVELOPMENT = "Development";
    public const string ENV_STAGING = "Staging";
    public const string ENV_PRODUCTION = "Production";
}

[Test]
[Property("Category", TestProperties.CATEGORY_UNIT)]
[Property("Priority", TestProperties.PRIORITY_HIGH)]
public async Task ExampleTest_UsingConstants_EnsuresConsistency()
{
    await Assert.That(1 + 1).IsEqualTo(2);
}
```text

### TUnit Test Filtering Execution

TUnit uses `dotnet run` instead of `dotnet test`:

```bash
# Only run unit tests
dotnet run --treenode-filter "/*/*/*/*[Category=Unit]"

# Only run high priority tests
dotnet run --treenode-filter "/*/*/*/*[Priority=High]"

# Combined conditions: run high priority unit tests
dotnet run --treenode-filter "/*/*/*/*[(Category=Unit)&(Priority=High)]"

# OR condition: run unit tests or smoke tests
dotnet run --treenode-filter "/*/*/*/*[(Category=Unit)|(Suite=Smoke)]"

# Run tests for specific features
dotnet run --treenode-filter "/*/*/*/*[Feature=OrderProcessing]"
```text

### Filter Syntax Notes

- Path pattern `/*/*/*/*` represents Assembly/Namespace/Class/Method level
- Attribute names are case-sensitive
- Combined conditions must be properly enclosed in parentheses

---

## Test Lifecycle Management

TUnit provides complete lifecycle hooks: `[Before(Class)]` -> Constructor -> `[Before(Test)]` -> Test Method -> `[After(Test)]` -> Dispose -> `[After(Class)]`. Also has Assembly/TestSession level and `[BeforeEvery]`/`[AfterEvery]` global hooks. Constructor always executes first, BeforeClass/AfterClass each only executes once.

> Full attribute family and examples please refer to [references/lifecycle-management.md](references/lifecycle-management.md)

---

## Dependency Injection Patterns

### TUnit Dependency Injection Core Concepts

TUnit's dependency injection is built on Data Source Generators:

```csharp
public class MicrosoftDependencyInjectionDataSourceAttribute : DependencyInjectionDataSourceAttribute<IServiceScope>
{
    private static readonly IServiceProvider ServiceProvider = CreateSharedServiceProvider();

    public override IServiceScope CreateScope(DataGeneratorMetadata dataGeneratorMetadata)
    {
        return ServiceProvider.CreateScope();
    }

    public override object? Create(IServiceScope scope, Type type)
    {
        return scope.ServiceProvider.GetService(type);
    }

    private static IServiceProvider CreateSharedServiceProvider()
    {
        return new ServiceCollection()
            .AddSingleton<IOrderRepository, MockOrderRepository>()
            .AddSingleton<IDiscountCalculator, MockDiscountCalculator>()
            .AddSingleton<IShippingCalculator, MockShippingCalculator>()
            .AddSingleton<ILogger<OrderService>, MockLogger<OrderService>>()
            .AddTransient<OrderService>()
            .BuildServiceProvider();
    }
}
```text

### Using TUnit Dependency Injection

```csharp
[MicrosoftDependencyInjectionDataSource]
public class DependencyInjectionTests(OrderService orderService)
{
    [Test]
    public async Task CreateOrder_UsingTUnitDependencyInjection_ShouldWorkCorrectly()
    {
        // Arrange - dependencies automatically injected through TUnit DI
        var items = new List<OrderItem>
        {
            new() { ProductId = "PROD001", ProductName = "Test Product", UnitPrice = 100m, Quantity = 2 }
        };

        // Act
        var order = await orderService.CreateOrderAsync("CUST001", CustomerLevel.VIP, items);

        // Assert
        await Assert.That(order).IsNotNull();
        await Assert.That(order.CustomerId).IsEqualTo("CUST001");
        await Assert.That(order.CustomerLevel).IsEqualTo(CustomerLevel.VIP);
    }

    [Test]
    public async Task TUnitDependencyInjection_ValidateAutoInjection_ServiceShouldBeCorrectType()
    {
        await Assert.That(orderService).IsNotNull();
        await Assert.That(orderService.GetType().Name).IsEqualTo("OrderService");
    }
}
```text

### TUnit DI vs Manual Dependency Creation Comparison

| Feature | TUnit DI | Manual Dependency Creation |
| :------ | :------- | :------------------------- |
| **Setup Complexity** | Setup once, reuse | Manual creation needed for each test |
| **Maintainability** | Dependency changes only in one place | Need to modify all tests that use it |
| **Consistency** | Consistent with production code DI | May be inconsistent with actual application |
| **Test Readability** | Focus on test logic | Interfered by dependency creation code |
| **Scope Management** | Automatic service scope management | Need to manually manage object lifecycle |
| **Error Risk** | Framework guarantees correct injection | May miss or incorrectly create dependencies |

---

## Execution Control and Test Quality

- **`[Retry(n)]`**: Only for unstable tests caused by external dependencies (network, file locking), not for logic errors
- **`[Timeout(ms)]`**: Set reasonable upper limit for performance-sensitive tests, validate SLA with `Stopwatch`
- **`[DisplayName]`**: Supports `{0}` parameter interpolation, making test reports more business-language aligned

> Full examples (Retry/Timeout/DisplayName) please refer to [references/execution-control.md](references/execution-control.md)

---

## ASP.NET Core Integration Testing

Using `WebApplicationFactory<Program>` in TUnit for ASP.NET Core integration testing, managing lifecycle through `IDisposable` implementation. Covers API response validation, Content-Type header checking, and performance baseline and parallel load testing.

> Full WebApplicationFactory integration and load testing examples please refer to [references/aspnet-integration.md](references/aspnet-integration.md)

---

## TUnit + Testcontainers Infrastructure Orchestration

Using `[Before(Assembly)]` / `[After(Assembly)]` at Assembly level to manage multi-container orchestration for PostgreSQL, Redis, Kafka, combined with `NetworkBuilder` to create shared network. Containers only start once, significantly reducing startup time and resource consumption while maintaining data isolation between tests.

> Full multi-container orchestration and global sharing examples please refer to [references/tunit-testcontainers.md](references/tunit-testcontainers.md)

---

## TUnit Engine Modes

### Source Generation Mode (Default Mode)

```text
████████╗██╗   ██╗███╗   ██╗██╗████████╗
╚══██╔══╝██║   ██║████╗  ██║██║╚══██╔══╝
   ██║   ██║   ██║██╔██╗ ██║██║   ██║
   ██║   ██║   ██║██║╚██╗██║██║   ██║
   ██║   ╚██████╔╝██║ ╚████║██║   ██║
   ╚═╝    ╚═════╝ ╚═╝  ╚═══╝╚═╝   ╚═╝

   Engine Mode: SourceGenerated
```text

### Features and Advantages

- **Compile-time generation**: All test discovery logic generated at compile time, no runtime reflection needed
- **Excellent performance**: Several times faster than reflection mode
- **Type safety**: Compile-time validation of test configuration and data sources
- **AOT compatible**: Fully supports Native AOT compilation

### Reflection Mode

```bash
# Enable reflection mode
dotnet run -- --reflection

# Or set environment variable
$env:TUNIT_EXECUTION_MODE = "reflection"
dotnet run
```text

### Applicable Scenarios

- Dynamic test discovery
- F# and VB.NET projects (automatically used)
- Certain reflection-dependent test patterns

### Native AOT Support

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```text

```bash
dotnet publish -c Release
```text

---

## Common Issues and Troubleshooting

### Test Statistics Display Abnormal Issue

**Problem:** `Test Summary: Total: 0, Failed: 0, Success: 0`

### Solution Steps

1. **Ensure project file is correctly configured:**

```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
</PropertyGroup>
```text

2. **Ensure GlobalUsings.cs is correct:**

```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using TUnit.Core;
global using TUnit.Assertions;
global using TUnit.Assertions.Extensions;
```text

3. **Special configuration for integration tests:**

```csharp
// Add at the end of WebApi project's Program.cs
public partial class Program { }  // Allow integration tests to access
```text

4. **Clean and rebuild:**

```bash
dotnet clean; dotnet build
dotnet test --verbosity normal
```text

### Source Generator Related Issues

### Issue: Test classes not discoverable

- **Solution**: Ensure project is fully rebuilt (`dotnet clean; dotnet build`)

### Issue: Strange errors at compile time

- **Solution**: Check if other Source Generator packages exist, consider updating to compatible versions

### Diagnostic Options

```ini
# .editorconfig
tunit.enable_verbose_diagnostics = true
```text

```xml
<PropertyGroup>
    <TUnitEnableVerboseDiagnostics>true</TUnitEnableVerboseDiagnostics>
</PropertyGroup>
```text

---

## Practical Recommendations

### Data-Driven Testing Selection Strategy

- **MethodDataSource**: Suitable for dynamic data, complex objects, external file loading
- **ClassDataSource**: Suitable for shared data, AutoFixture integration, cross-test class reuse
- **Matrix Tests**: Suitable for combination testing, but control parameter quantity to avoid explosive growth

### Execution Control Best Practices

- **Retry**: Only for truly unstable external dependency tests
- **Timeout**: Set reasonable limits for performance-sensitive tests
- **DisplayName**: Make test reports more business-language aligned

### Integration Testing Strategy

- Use WebApplicationFactory for complete Web API testing
- Use TUnit + Testcontainers to build complex multi-service test environments
- Manage complex dependency relationships through attribute injection system
- Only test actually existing functionality, avoid testing non-existent endpoints

---

## Template Files

| File Name | Description |
| --------- | ----------- |
| [data-source-examples.cs](templates/data-source-examples.cs) | MethodDataSource, ClassDataSource examples |
| [matrix-tests-examples.cs](templates/matrix-tests-examples.cs) | Matrix Tests combination testing examples |
| [lifecycle-di-examples.cs](templates/lifecycle-di-examples.cs) | Lifecycle management and dependency injection examples |
| [execution-control-examples.cs](templates/execution-control-examples.cs) | Retry, Timeout, DisplayName examples |
| [aspnet-integration-tests.cs](templates/aspnet-integration-tests.cs) | ASP.NET Core integration testing examples |
| [testcontainers-examples.cs](templates/testcontainers-examples.cs) | Testcontainers infrastructure orchestration examples |

---

## Reference Resources

### Original Articles

This skill content is distilled from the "Old School Software Engineer's Testing Practice - 30 Day Challenge" article series:

- **Day 29 - TUnit Advanced Applications: Data-Driven Testing and Dependency Injection Deep Practice**
  - Article: https://ithelp.ithome.com.tw/articles/10377970
  - Sample code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day29

- **Day 30 - TUnit Advanced Applications - Execution Control and Test Quality and ASP.NET Core Integration Testing**
  - Article: https://ithelp.ithome.com.tw/articles/10378176
  - Sample code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day30

### TUnit Official Resources

- [TUnit Official Website](https://tunit.dev/)
- [TUnit GitHub Repository](https://github.com/thomhurst/TUnit)

### Advanced Feature Documentation

- [TUnit Method Data Source Documentation](https://tunit.dev/docs/test-authoring/method-data-source)
- [TUnit Class Data Source Documentation](https://tunit.dev/docs/test-authoring/class-data-source)
- [TUnit Matrix Tests Documentation](https://tunit.dev/docs/test-authoring/matrix-tests)
- [TUnit Properties Documentation](https://tunit.dev/docs/test-lifecycle/properties)
- [TUnit Dependency Injection Documentation](https://tunit.dev/docs/test-lifecycle/dependency-injection)
- [TUnit Retrying Documentation](https://tunit.dev/docs/execution/retrying)
- [TUnit Timeouts Documentation](https://tunit.dev/docs/execution/timeouts)
- [TUnit Engine Modes Documentation](https://tunit.dev/docs/execution/engine-modes)
- [TUnit ASP.NET Core Documentation](https://tunit.dev/docs/examples/aspnet)
- [TUnit Complex Test Infrastructure](https://tunit.dev/docs/examples/complex-test-infrastructure-orchestration)

### Testcontainers Related Resources

- [Testcontainers.NET Official Website](https://dotnet.testcontainers.org/)
- [Testcontainers.NET GitHub](https://github.com/testcontainers/testcontainers-dotnet)
````
