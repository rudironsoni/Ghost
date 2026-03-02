---
name: dotnet-testing-autofixture-nsubstitute-integration
description: >
  AutoFixture and NSubstitute Integration Guide - Implementing Auto-Mocking. Use
  when you need to automatically create mock objects and simplify testing of
  complex dependency injection. Covers AutoNSubstituteDataAttribute, Frozen
  mechanism, Greedy construction strategy. Includes customized handling for
  special dependencies like IMapper (AutoMapper/Mapster).

  Keywords: autofixture nsubstitute, auto mocking, AutoNSubstituteDataAttribute,
  auto-mocking, Frozen, AutoNSubstituteCustomization,
  AutoFixture.AutoNSubstitute, Greedy, fixture.Freeze, Received(), Returns(),
  IMapper, AutoMapper, Mapster, mapper testing
---
Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# AutoFixture + NSubstitute Auto-Mocking Integration

## Applicable Scenarios

This skill introduces how to integrate AutoFixture with NSubstitute through the `AutoFixture.AutoNSubstitute` package to
implement auto-mocking functionality. This integration approach significantly simplifies testing of service classes with
multiple dependencies, allowing developers to focus on test logic rather than tedious object creation.

### When to Use

Use this skill when asked to perform the following tasks:

- Test service classes with multiple interface dependencies
- Create test setups that automatically mock all interface dependencies
- Use `[Frozen]` attribute to ensure dependency instances remain consistent throughout tests
- Create project-level custom AutoData attributes to integrate multiple customization settings
- Combine fixed test values with automatically generated objects for parameterized tests

### Core Value

- **Reduce boilerplate code**: No need to manually create `Substitute.For<T>()` for each interface
- **Automatically handle complex dependency graphs**: AutoFixture automatically resolves and creates required objects
- **Improve test maintainability**: When constructors change, test code usually doesn't need to be modified
- **Maintain test focus**: Let developers focus on test logic rather than object creation

---

## Package Installation and Configuration

### Required Packages

````bash
# Core packages
dotnet add package AutoFixture.AutoNSubstitute

# Related packages (if not already installed)
dotnet add package AutoFixture
dotnet add package AutoFixture.Xunit2
dotnet add package NSubstitute
dotnet add package xunit
```text

### NuGet Package Information

| Package Name                  | Purpose                               | NuGet Link                                                               |
| ----------------------------- | ------------------------------------- | ------------------------------------------------------------------------ |
| `AutoFixture.AutoNSubstitute` | AutoFixture and NSubstitute Integration | [nuget.org](https://www.nuget.org/packages/AutoFixture.AutoNSubstitute/) |
| `AutoFixture.Xunit2`          | xUnit Integration (AutoData attributes) | [nuget.org](https://www.nuget.org/packages/AutoFixture.Xunit2/)          |
| `NSubstitute`                 | Mocking Framework                     | [nuget.org](https://www.nuget.org/packages/NSubstitute/)                 |

---

## Core Concepts

### Role of AutoNSubstituteCustomization

When adding `AutoNSubstituteCustomization` to AutoFixture, it automatically:

1. **Detects interface types**: When AutoFixture encounters interfaces or abstract classes
2. **Automatically creates substitutes**: Uses NSubstitute's `Substitute.For<T>()` to create mock objects
3. **Injects dependencies**: Injects these substitute objects into required constructors
4. **Maintains instance consistency**: Ensures same type substitutes remain consistent within a test

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;

// Create a Fixture with AutoNSubstitute functionality
var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

// Automatically create service and its dependencies
// All interface dependencies of MyService become NSubstitute substitutes
var service = fixture.Create<MyService>();
```text

### FrozenAttribute Freezing Mechanism

The `[Frozen]` attribute controls instances of a type in tests:

- When a parameter is marked with `[Frozen]`, AutoFixture creates one instance of this class and **freezes** it
- The same frozen instance is used throughout the test method
- This is especially important for tests that need to set dependency behavior and then verify the SUT

```csharp
[Theory]
[AutoData]
public async Task TestMethod(
    [Frozen] IRepository repository,  // This repository will be frozen
    MyService sut)                    // sut will use the same repository
{
    // Set behavior of frozen instance
    repository.GetAsync(Arg.Any<int>()).Returns(someData);

    // SUT internally uses the same repository instance
    var result = await sut.DoSomething();
}
```text

### Importance of Parameter Order

When using `[Frozen]`, **parameter order is very important**:

```csharp
// ✅ Correct: Frozen parameter before SUT
public async Task TestMethod(
    [Frozen] IRepository repository,
    MyService sut)

// ❌ Wrong: SUT uses a different repository instance
public async Task TestMethod(
    MyService sut,
    [Frozen] IRepository repository)  // Frozen too late
```text

---

## Traditional Approach vs AutoNSubstitute Approach

### Traditional Manual Approach

```csharp
[Fact]
public async Task TraditionalWay()
{
    // Arrange - Manually create each dependency
    var repository = Substitute.For<IRepository>();
    var logger = Substitute.For<ILogger<OrderService>>();
    var notificationService = Substitute.For<INotificationService>();
    var cacheService = Substitute.For<ICacheService>();

    var sut = new OrderService(repository, logger, notificationService, cacheService);

    // Set substitute behavior
    repository.GetOrderAsync(Arg.Any<int>()).Returns(someOrder);

    // Act
    var result = await sut.GetOrderAsync(orderId);

    // Assert
    result.Should().NotBeNull();
}
```text

**Problems**:

- When services add new dependencies, all tests need modification
- Large amounts of repetitive `Substitute.For<T>()` calls
- Test code is verbose, making it difficult to quickly understand test intent

### Using AutoNSubstitute Approach

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task WithAutoNSubstitute(
    [Frozen] IRepository repository,
    OrderService sut)
{
    // Arrange - Dependencies automatically created, only need to set required behavior
    repository.GetOrderAsync(Arg.Any<int>()).Returns(someOrder);

    // Act
    var result = await sut.GetOrderAsync(orderId);

    // Assert
    result.Should().NotBeNull();
}
```text

**Advantages**:

- Only declare dependencies that need interaction
- Other dependencies (logger, notificationService, cacheService) are automatically created
- When constructors change, tests usually don't need modification

---

## Custom AutoData Attributes

### Why Need Custom AutoData Attributes?

In actual projects, you typically need to integrate multiple customization settings:

- **AutoNSubstituteCustomization**: Automatically creates NSubstitute substitutes for interfaces
- **Project-specific Customizations**: Such as Mapper settings, validator settings, etc.
- **Consistent test infrastructure**: Ensures the entire project uses the same settings

### AutoDataWithCustomizationAttribute Implementation

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;

namespace MyProject.Tests.AutoFixtureConfigurations;

/// <summary>
/// AutoData attribute with custom customization settings
/// </summary>
public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    /// <summary>
    /// Constructor
    /// </summary>
    public AutoDataWithCustomizationAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture()
            .Customize(new AutoNSubstituteCustomization())
            .Customize(new MapsterMapperCustomization())  // Project-specific settings
            .Customize(new DomainCustomization());        // Domain model settings

        return fixture;
    }
}
```text

### InlineAutoDataWithCustomizationAttribute Implementation

For combining fixed test values with automatically generated objects:

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;

namespace MyProject.Tests.AutoFixtureConfigurations;

/// <summary>
/// InlineAutoData attribute with custom customization settings
/// </summary>
public class InlineAutoDataWithCustomizationAttribute : InlineAutoDataAttribute
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="values">Fixed values (will fill first few parameters of test method)</param>
    public InlineAutoDataWithCustomizationAttribute(params object[] values)
        : base(new AutoDataWithCustomizationAttribute(), values)
    {
    }
}
```text

### Important Implementation Details

### Why use `new AutoDataWithCustomizationAttribute()` instead of `CreateFixture` method?

```csharp
// ❌ Wrong: InlineAutoDataAttribute needs AutoDataAttribute, not Func<IFixture>
public InlineAutoDataWithCustomizationAttribute(params object[] values)
    : base(CreateFixture, values)  // Compile error or unexpected behavior

// ✅ Correct: Pass AutoDataAttribute instance
public InlineAutoDataWithCustomizationAttribute(params object[] values)
    : base(new AutoDataWithCustomizationAttribute(), values)
```text

Reason:

- `InlineAutoDataAttribute` inherits from `CompositeDataAttribute`
- It needs to receive an `AutoDataAttribute` instance as the data source provider
- This allows reusing all settings from `AutoDataWithCustomizationAttribute`

---

## Customization for Common Dependencies

Certain dependencies (like IMapper) are not suitable for Mock and should use real instances. Includes customization examples for Mapster and AutoMapper.

> For complete customization examples, see [references/dependency-customization.md](references/dependency-customization.md)

---

## Test Implementation Examples

Covers basic tests, Frozen dependency behavior setup, automatically generated test data, InlineAutoData parameterized tests, CollectionSize control, IFixture complex data setup, Nullable reference type handling, and other complete examples.

> For complete test implementation examples, see [references/test-implementation-examples.md](references/test-implementation-examples.md)

---

## Applicable Scenario Judgment

### Recommended Scenarios

| Scenario              | Reason                                    |
| --------------------- | ----------------------------------------- |
| Service Layer Testing | Usually has multiple dependencies, maximum benefit from auto-mocking |
| Complex Dependency Graph | AutoFixture automatically handles multi-layer dependencies |
| Parameterized Testing | Combine fixed values with automatically generated data |
| Need for Large Test Data | Reduce manual test data creation work |
| Rapid Iteration Development | Tests usually don't need modification when constructors change |

### Use with Caution Scenarios

| Scenario                | Reason                                           |
| ---------------------- | ------------------------------------------------ |
| Single Dependency Testing | Manual creation may be clearer and more intuitive |
| Precise Property Value Control | Requires additional `fixture.Build().With()` setup |
| Team Unfamiliar with AutoFixture | Learning cost may affect development efficiency |
| Difficult Debugging Scenarios | Automatically generated objects may complicate debugging |
| Performance-Sensitive Tests | Object creation overhead may affect execution speed |

---

## Best Practices

### Adoption Strategy

1. **Gradual Adoption**
   - Start with simple service classes
   - Gradually expand to complex scenarios
   - Let the team gradually become familiar with the pattern

2. **Team Training**
   - Ensure team understands Frozen mechanism
   - Explain importance of parameter order
   - Share debugging techniques

3. **Establish Conventions**
   - When to use auto-generation vs manual creation
   - Naming and organization of custom Customizations
   - Test data control strategy

### Code Organization

```text
MyProject.Tests/
├── AutoFixtureConfigurations/
│   ├── AutoDataWithCustomizationAttribute.cs
│   ├── InlineAutoDataWithCustomizationAttribute.cs
│   ├── AutoMapperCustomization.cs
│   └── DomainCustomization.cs
├── Services/
│   ├── OrderServiceTests.cs
│   └── ShipperServiceTests.cs
└── ...
```text

### Naming Conventions

- **Custom AutoData Attributes**: `[ProjectName]AutoDataAttribute` or `AutoDataWithCustomizationAttribute`
- **Customization Classes**: `[Feature]Customization` (e.g., `MapsterMapperCustomization`)
- **Test Methods**: Maintain `Method_Scenario_Expected` naming pattern

---

## Notes and Limitations

### Common Pitfalls

1. **Wrong Parameter Order**

   ```csharp
   // ❌ Frozen parameter after SUT, won't take effect
   public void Test(MyService sut, [Frozen] IRepository repo)

   // ✅ Frozen parameter must be before SUT
   public void Test([Frozen] IRepository repo, MyService sut)
```text

2. **Forgetting AutoNSubstituteCustomization**

   ```csharp
   // ❌ Without AutoNSubstitute, interfaces will produce exceptions
   var fixture = new Fixture();

   // ✅ Add AutoNSubstituteCustomization
   var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
```text

3. **Over-reliance on Auto-Generation**

   ```csharp
   // ❌ Test intent unclear
   public void Test(Order order, Customer customer, MyService sut)
   {
       var result = sut.Process(order);
       result.Should().NotBeNull();  // Validating what?
   }

   // ✅ Explicitly control key properties
   public void Test(IFixture fixture, MyService sut)
   {
       var order = fixture.Build<Order>()
                          .With(o => o.Status, OrderStatus.Pending)
                          .Create();

       var result = sut.Process(order);
       result.Status.Should().Be(OrderStatus.Processed);
   }
```text

### Performance Considerations

- Each test method creates a new Fixture and all dependencies
- Complex object graphs may increase test execution time
- Consider using `[ClassData]` or `IClassFixture<T>` to share setup

---

## Related Skills

| Skill Name                   | Relationship Description                               |
| ---------------------------- | ------------------------------------------------------ |
| `autofixture-basics`         | AutoFixture basics, prerequisite knowledge for this skill |
| `autofixture-customization`  | Advanced usage of custom Customizations                |
| `autodata-xunit-integration` | Complete explanation of AutoData attribute family      |
| `nsubstitute-mocking`        | NSubstitute basics, detailed Mock setup explanation    |

---

## Reference Resources

### Original Articles

This skill content is distilled from the "Old School Software Engineer's Testing Practice - 30 Day Challenge" article series:

- **Day 13 - AutoFixture Integration with NSubstitute: Automatically Creating Mock Objects**
  - Article: https://ithelp.ithome.com.tw/articles/10375419
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day13

### Official Documentation

- [AutoFixture.AutoNSubstitute NuGet Package](https://www.nuget.org/packages/AutoFixture.AutoNSubstitute/)
- [AutoFixture Documentation - Auto Mocking](https://autofixture.readthedocs.io/en/stable/)
- [NSubstitute Documentation](https://nsubstitute.github.io/help/getting-started/)

### Extended Reading

- [Using AutoFixture.AutoData to Rewrite Previous Test Code | mrkt's Programming Learning Notes](https://www.dotblogs.com.tw/mrkt/2024/09/29/191300)


### Sample Code

- [custom-autodata-attributes.cs](templates/custom-autodata-attributes.cs) - Custom AutoData attributes template
- [frozen-patterns.cs](templates/frozen-patterns.cs) - Frozen mechanism usage patterns
- [service-testing-examples.cs](templates/service-testing-examples.cs) - Complete service layer testing examples
````
