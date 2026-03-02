---
name: dotnet-testing-autofixture-bogus-integration
description: >
  Complete guide for AutoFixture and Bogus integration. Use when you need to
  combine AutoFixture with Bogus to generate test data that is both anonymous
  and realistic. Covers SpecimenBuilder integration, hybrid generators, test
  data factories, and circular reference handling.

  Keywords: autofixture bogus integration, autofixture bogus, bogus integration,
  Faker, EmailSpecimenBuilder, PhoneSpecimenBuilder, NameSpecimenBuilder,
  realistic test data, semantic data, hybrid generator, HybridTestDataGenerator,
  OmitOnRecursionBehavior, circular reference
---
Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# AutoFixture and Bogus Integration Application Guide

## Applicable Scenarios

Use this skill when asked to perform the following tasks:

- Integrate AutoFixture and Bogus tools
- Create hybrid test data generators
- Design ISpecimenBuilder integrating Bogus data generation
- Create custom AutoData attributes using Bogus
- Handle circular reference issues
- Create unified test data factories
- Design test base classes integrating data generation functionality

---

## Core Concepts

### Why Integration is Needed?

**AutoFixture Advantages**:

- Quickly generate anonymous test data
- Automatically handle complex object structures
- Good circular reference handling mechanism

**Bogus Advantages**:

- Generate realistic semantic data
- Rich data type support (Email, Phone, Address, etc.)
- Data formats friendly to validation

**Integrated Effect**:

````csharp
// Problem before integration
var user = fixture.Create<User>();
// user.Email might be "Email1a2b3c4d", not like a real email

// After integration
var user = integratedFixture.Create<User>();
// user.Email is "john.doe@example.com"
// user.FirstName is "John"
// Other properties automatically filled by AutoFixture
```text

---

## Package Installation

```xml
<PackageReference Include="AutoFixture" Version="4.18.1" />
<PackageReference Include="AutoFixture.Xunit2" Version="4.18.1" />
<PackageReference Include="Bogus" Version="35.6.3" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="AwesomeAssertions" Version="9.1.0" />
```text

---

## Integration Architecture

### Integration Overview

| Integration Method          | Applicable Scenario           | Complexity |
| ---------------------------- | ----------------------------- | ---------- |
| Property-level SpecimenBuilder | Specific properties using Bogus | Low        |
| Type-level SpecimenBuilder | Entire type using Bogus         | Medium     |
| Hybrid Generator (HybridGenerator) | Unified API integration | Medium     |
| Integrated Factory (IntegratedFactory) | Complete test scenario construction | High       |
| Custom AutoData Attribute | xUnit integration               | Low        |

---

## Core Integration Techniques

Implement property-level and type-level integration through the `ISpecimenBuilder` interface, paired with extension methods (`WithBogus()`, `WithOmitOnRecursion()`, `WithSeed()`) to simplify the configuration process. Covers common SpecimenBuilders like Email, Phone, Name, Address, and complete type generator registration patterns.

> For complete content, see [references/core-integration-techniques.md](references/core-integration-techniques.md)

---

## Circular Reference Handling

### Why Circular References are Important?

```csharp
public class User
{
    public Company? Company { get; set; }  // User references Company
}

public class Company
{
    public List<User> Employees { get; set; } = new();  // Company references User
}
```text

**Problem**: User → Company → Employees(User) → Company → ... infinite loop

### Solution: OmitOnRecursionBehavior

```csharp
var fixture = new Fixture();
fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
    .ToList()
    .ForEach(b => fixture.Behaviors.Remove(b));
fixture.Behaviors.Add(new OmitOnRecursionBehavior());
```text

**Effect**:

- ✅ Avoid StackOverflowException
- ✅ Circular reference properties set to null or empty collection
- ⚠️ Some deep properties may be null (this is expected behavior)

---

## Custom AutoData Attributes

### BogusAutoDataAttribute

```csharp
public class BogusAutoDataAttribute : AutoDataAttribute
{
    public BogusAutoDataAttribute()
        : base(() => new Fixture().WithBogus())
    {
    }
}
```text

### Usage

```csharp
[Theory]
[BogusAutoData]
public void UsingIntegratedDataTest(User user, Address address)
{
    user.Email.Should().Contain("@");
    user.FirstName.Should().NotBeNullOrEmpty();
    address.City.Should().NotBeNullOrEmpty();
}
```text

---

## Hybrid Generator

### ITestDataGenerator Interface

```csharp
public interface ITestDataGenerator
{
    T Generate<T>();
    IEnumerable<T> Generate<T>(int count);
    T Generate<T>(Action<T> configure);
}
```text

### HybridTestDataGenerator Implementation

```csharp
public class HybridTestDataGenerator : ITestDataGenerator
{
    private readonly IFixture _fixture;

    public HybridTestDataGenerator(int? seed = null)
    {
        _fixture = new Fixture()
            .WithBogus()
            .WithOmitOnRecursion();

        if (seed.HasValue)
        {
            Bogus.Randomizer.Seed = new Random(seed.Value);
        }
    }

    public T Generate<T>() => _fixture.Create<T>();

    public IEnumerable<T> Generate<T>(int count)
        => Enumerable.Range(0, count).Select(_ => Generate<T>());

    public T Generate<T>(Action<T> configure)
    {
        var item = Generate<T>();
        configure(item);
        return item;
    }
}
```text

---

## Integrated Test Data Factory

### IntegratedTestDataFactory

```csharp
public class IntegratedTestDataFactory
{
    private readonly IFixture _fixture;
    private readonly Dictionary<Type, object> _cache = new();

    public IntegratedTestDataFactory(int? seed = null)
    {
        _fixture = new Fixture()
            .WithBogus()
            .WithOmitOnRecursion()
            .WithRepeatCount(3);

        if (seed.HasValue)
        {
            _fixture.WithSeed(seed.Value);
        }
    }

    public T CreateFresh<T>() => _fixture.Create<T>();

    public List<T> CreateMany<T>(int count = 3)
        => _fixture.CreateMany<T>(count).ToList();

    public T GetCached<T>() where T : class
    {
        var type = typeof(T);
        if (_cache.TryGetValue(type, out var cached))
            return (T)cached;

        var instance = CreateFresh<T>();
        _cache[type] = instance;
        return instance;
    }

    public void ClearCache() => _cache.Clear();

    /// <summary>
    /// Create complete test scenario
    /// </summary>
    public TestScenario CreateTestScenario()
    {
        var company = CreateFresh<Company>();
        var users = CreateMany<User>(5);
        var orders = CreateMany<Order>(10);

        // Create associations
        foreach (var user in users)
        {
            user.Company = company;
        }

        company.Employees = users;

        return new TestScenario
        {
            Company = company,
            Users = users,
            Orders = orders
        };
    }
}
```text

---

## Test Base Class

### TestBase Implementation

```csharp
public abstract class TestBase
{
    protected readonly IFixture Fixture;
    protected readonly HybridTestDataGenerator Generator;
    protected readonly IntegratedTestDataFactory Factory;

    protected TestBase(int? seed = null)
    {
        Fixture = new Fixture()
            .WithBogus()
            .WithOmitOnRecursion()
            .WithRepeatCount(3);

        if (seed.HasValue)
        {
            Fixture.WithSeed(seed.Value);
        }

        Generator = new HybridTestDataGenerator(seed);
        Factory = new IntegratedTestDataFactory(seed);
    }

    protected T Create<T>() => Fixture.Create<T>();

    protected List<T> CreateMany<T>(int count = 3)
        => Fixture.CreateMany<T>(count).ToList();

    protected T Create<T>(Action<T> configure)
    {
        var instance = Create<T>();
        configure(instance);
        return instance;
    }
}
```text

---

## Seed Management and Reproducibility

### Important Limitations

Since AutoFixture and Bogus have different random number management mechanisms:

- ✅ Seed ensures test behavior stability
- ✅ Seed ensures data format consistency
- ❌ Cannot guarantee all property values are identical

### Recommended Approach

```csharp
// Use Seed to ensure stability
var factory = new IntegratedTestDataFactory(seed: 12345);

// If fully reproducible is needed, use single tool
var faker = new Faker<User>();
faker.UseSeed(12345);
```text

---

## Usage Examples

### Basic Integration Usage

```csharp
[Fact]
public void AutoFixture_Integrated_With_Bogus_Should_Generate_Realistic_Data()
{
    // Arrange
    var fixture = new Fixture().WithBogus();

    // Act
    var user = fixture.Create<User>();

    // Assert
    user.Email.Should().Contain("@");
    user.FirstName.Should().NotBeNullOrEmpty();
    user.Phone.Should().MatchRegex(@"[\d\-\(\)\s]+");
}
```text

### Using Factory to Create Test Scenarios

```csharp
[Fact]
public void Factory_Should_Create_Complete_Test_Scenario()
{
    // Arrange
    var factory = new IntegratedTestDataFactory(seed: 42);

    // Act
    var scenario = factory.CreateTestScenario();

    // Assert
    scenario.Company.Should().NotBeNull();
    scenario.Users.Should().HaveCount(5);
    scenario.Orders.Should().HaveCount(10);

    scenario.Users.Should().AllSatisfy(user =>
    {
        user.Company.Should().Be(scenario.Company);
        user.Email.Should().Contain("@");
    });
}
```text

---

## Best Practices

### Recommended Practices

1. **Always handle circular references first**

   ```csharp
   fixture.WithOmitOnRecursion().WithBogus();
```text

2. **Create dedicated SpecimenBuilders for common entities**

3. **Use Seed to ensure test stability**

4. **Create test base classes to unify data generation logic**

5. **Use cache appropriately to improve performance**

### Things to Avoid

1. ❌ Over-engineering, keep it simple and practical
2. ❌ Expecting integration environment to be fully reproducible
3. ❌ Ignoring circular reference handling
4. ❌ Recreating Fixture in every test

---

## Comparison: Integration vs AutoFixture/Bogus Alone

| Aspect              | Pure AutoFixture | Pure Bogus     | Integrated Solution |
| ------------------- | ---------------- | -------------- | ------------------- |
| Data Realism        | Low              | High           | High                |
| Configuration Complexity | Low         | Medium         | Medium              |
| Object Relationship Handling | Automatic | Manual         | Automatic           |
| Circular Reference Handling | Built-in   | None           | Integrated          |
| Reproducibility     | High             | High           | Medium              |
| Applicable Scenarios | Unit tests      | Integration tests/Prototypes | Both |

---

## Reference Resources

### Original Articles

This skill content is distilled from the "Old School Software Engineer's Testing Practice - 30 Day Challenge" article series:

- **Day 15 - AutoFixture and Bogus Integration: Combining Both Advantages**
  - Article: https://ithelp.ithome.com.tw/articles/10375620
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day15

### Official Documentation

- [AutoFixture GitHub](https://github.com/AutoFixture/AutoFixture)
- [Bogus GitHub Repository](https://github.com/bchavez/Bogus)

---

## Related Skills

- [autofixture-basics](../autofixture-basics/) - AutoFixture basics
- [autofixture-customization](../autofixture-customization/) - AutoFixture customization strategies
- [autodata-xunit-integration](../autodata-xunit-integration/) - AutoData attribute integration
- [bogus-fake-data](../bogus-fake-data/) - Bogus fake data generator

````
