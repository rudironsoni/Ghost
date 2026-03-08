---
name: dotnet-testing-autofixture-customization
category: testing
subcategory: test-data
description: |
  Complete guide for AutoFixture advanced customization techniques. Use when you need to customize AutoFixture builders or handle special type test data generation rules. Covers DataAnnotations automatic integration, ISpecimenBuilder implementation, priority management. Includes DateTime/numeric range builders, generic design, and fluent extension methods.
  Keywords: autofixture customization, autofixture customize, autofixture customization, specimen builder, ISpecimenBuilder, RandomDateTimeSequenceGenerator, NumericRangeBuilder, DataAnnotations autofixture, fixture.Customizations, Insert(0), custom builder, NoSpecimen, generic builder
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: 'autofixture, customization, test-data, specimen-builder, data-annotations'
  related_skills: 'autofixture-basics, autodata-xunit-integration, autofixture-bogus-integration'
claudecode: {}
opencode: {}
codexcli:
  short-description: '.NET skill guidance for dotnet-testing-autofixture-customization'
copilot: {}
geminicli: {}
antigravity: {}
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# AutoFixture Advanced: Custom Test Data Generation Strategies

## Applicable Scenarios

- autofixture customization
- autofixture customize
- ISpecimenBuilder
- specimen builder
- DataAnnotations autofixture
- property range control
- fixture.Customizations
- Insert(0)
- RandomDateTimeSequenceGenerator
- NumericRangeBuilder
- custom builder
- custom builder autofixture

## Overview

This skill covers AutoFixture's advanced customization features, allowing you to precisely control test data generation
logic based on business requirements. From DataAnnotations automatic integration to custom `ISpecimenBuilder`
implementation, mastering these techniques enables test data to better match actual business needs.

### Core Technologies

1. **DataAnnotations Integration**: AutoFixture automatically recognizes validation attributes like `[StringLength]`,
   `[Range]`, etc.
2. **Property Range Control**: Using `.With()` with `Random.Shared` to dynamically generate random values
3. **Custom ISpecimenBuilder**: Implementing precise control over specific property builders
4. **Priority Management**: Understanding the difference between `Insert(0)` vs `Add()`
5. **Generic Design**: Creating reusable builders supporting multiple numeric types

## Package Installation

````xml
<PackageReference Include="AutoFixture" Version="4.18.1" />
<PackageReference Include="AutoFixture.Xunit2" Version="4.18.1" />
```text

## DataAnnotations Automatic Integration

AutoFixture can automatically recognize validation attributes from `System.ComponentModel.DataAnnotations`:

```csharp
using System.ComponentModel.DataAnnotations;

public class Person
{
    public Guid Id { get; set; }

    [StringLength(10)]
    public string Name { get; set; } = string.Empty;

    [Range(10, 80)]
    public int Age { get; set; }

    public DateTime CreateTime { get; set; }
}

[Fact]
public void AutoFixture_Should_Recognize_DataAnnotations()
{
    var fixture = new Fixture();

    var person = fixture.Create<Person>();

    person.Name.Length.Should().Be(10);        // StringLength(10)
    person.Age.Should().BeInRange(10, 80);     // Range(10, 80)
}

[Fact]
public void AutoFixture_Batch_Generation_All_Meet_Constraints()
{
    var fixture = new Fixture();

    var persons = fixture.CreateMany<Person>(10).ToList();

    persons.Should().AllSatisfy(person =>
    {
        person.Name.Length.Should().Be(10);
        person.Age.Should().BeInRange(10, 80);
    });
}
```text

## Using .With() to Control Property Ranges

### Fixed Value vs Dynamic Value

```csharp
// ❌ Fixed value: Only executes once, all objects have same value
.With(x => x.Age, Random.Shared.Next(30, 50))

// ✅ Dynamic value: Each object recalculates
.With(x => x.Age, () => Random.Shared.Next(30, 50))
```text

### Complete Example

```csharp
[Fact]
public void With_Method_Fixed_Value_vs_Dynamic_Value_Difference()
{
    var fixture = new Fixture();

    // Fixed value: All objects have same age
    var fixedAgeMembers = fixture.Build<Member>()
        .With(x => x.Age, Random.Shared.Next(30, 50))
        .CreateMany(5)
        .ToList();

    // Dynamic value: Each object has different age
    var dynamicAgeMembers = fixture.Build<Member>()
        .With(x => x.Age, () => Random.Shared.Next(30, 50))
        .CreateMany(5)
        .ToList();

    // Fixed value: Only one age
    fixedAgeMembers.Select(m => m.Age).Distinct().Count().Should().Be(1);

    // Dynamic value: Usually has multiple ages
    dynamicAgeMembers.Select(m => m.Age).Distinct().Count().Should().BeGreaterThan(1);
}
```text

### Advantages of Random.Shared

| Feature       | `new Random()`              | `Random.Shared`       |
| ---------- | -------------------------- | -------------------- |
| Instantiation | Create new instance each time | Global shared single instance |
| Thread Safety | ❌ Not                     | ✅ Yes                |
| Performance | Multiple creations have overhead, possible duplicate values | Better performance, avoids duplicate values |
| Recommended Use | Single thread, short-term use | Multi-thread, global shared |

## Custom ISpecimenBuilder

### RandomRangedDateTimeBuilder: Precise Control of DateTime Properties

`RandomDateTimeSequenceGenerator` affects **all** DateTime properties. To control specific properties, you need custom builders:

```csharp
using AutoFixture.Kernel;
using System.Reflection;

public class RandomRangedDateTimeBuilder : ISpecimenBuilder
{
    private readonly DateTime _minDate;
    private readonly DateTime _maxDate;
    private readonly HashSet<string> _targetProperties;

    public RandomRangedDateTimeBuilder(
        DateTime minDate,
        DateTime maxDate,
        params string[] targetProperties)
    {
        _minDate = minDate;
        _maxDate = maxDate;
        _targetProperties = new HashSet<string>(targetProperties);
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            propertyInfo.PropertyType == typeof(DateTime) &&
            _targetProperties.Contains(propertyInfo.Name))
        {
            var range = _maxDate - _minDate;
            var randomTicks = (long)(Random.Shared.NextDouble() * range.Ticks);
            return _minDate.AddTicks(randomTicks);
        }

        return new NoSpecimen();
    }
}
```text

### Usage Example

```csharp
[Fact]
public void Control_Only_Specific_DateTime_Property()
{
    var fixture = new Fixture();

    var minDate = new DateTime(2025, 1, 1);
    var maxDate = new DateTime(2025, 12, 31);

    // Only control UpdateTime property
    fixture.Customizations.Add(
        new RandomRangedDateTimeBuilder(minDate, maxDate, "UpdateTime"));

    var member = fixture.Create<Member>();

    // UpdateTime within specified range
    member.UpdateTime.Should().BeOnOrAfter(minDate).And.BeOnOrBefore(maxDate);

    // CreateTime not affected
}
```text

### Importance of NoSpecimen

`NoSpecimen` indicates this builder cannot handle the request, passing it to the next builder in the chain:

```csharp
public object Create(object request, ISpecimenContext context)
{
    // Not our target → return NoSpecimen
    if (request is not PropertyInfo propertyInfo)
        return new NoSpecimen();

    if (propertyInfo.PropertyType != typeof(DateTime))
        return new NoSpecimen();

    if (!_targetProperties.Contains(propertyInfo.Name))
        return new NoSpecimen();

    // Is our target → generate value
    return GenerateRandomDateTime();
}
```text

## Priority Management: Insert(0) vs Add()

### Problem: Built-in Builders Have Higher Priority

AutoFixture built-in `RangeAttributeRelay`, `NumericSequenceGenerator` may have higher priority than custom builders:

```csharp
// ❌ May fail: Intercepted by built-in builders
fixture.Customizations.Add(new MyNumericBuilder(30, 50, "Age"));

// ✅ Correct: Ensure highest priority
fixture.Customizations.Insert(0, new MyNumericBuilder(30, 50, "Age"));
```text

### Improved Numeric Range Builder

```csharp
public class ImprovedRandomRangedNumericSequenceBuilder : ISpecimenBuilder
{
    private readonly int _min;
    private readonly int _max;
    private readonly Func<PropertyInfo, bool> _predicate;

    public ImprovedRandomRangedNumericSequenceBuilder(
        int min,
        int max,
        Func<PropertyInfo, bool> predicate)
    {
        _min = min;
        _max = max;
        _predicate = predicate;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            propertyInfo.PropertyType == typeof(int) &&
            _predicate(propertyInfo))
        {
            return Random.Shared.Next(_min, _max);
        }

        return new NoSpecimen();
    }
}
```text

### Using Insert(0) to Ensure Priority

```csharp
[Fact]
public void Using_Insert0_To_Ensure_Priority()
{
    var fixture = new Fixture();

    // Use Insert(0) to ensure highest priority
    fixture.Customizations.Insert(0,
        new ImprovedRandomRangedNumericSequenceBuilder(
            30, 50,
            prop => prop.Name == "Age" && prop.DeclaringType == typeof(Member)));

    var members = fixture.CreateMany<Member>(20).ToList();

    members.Should().AllSatisfy(m => m.Age.Should().BeInRange(30, 49));
}
```text

## Generic Numeric Range Builder

### NumericRangeBuilder<TValue>

```csharp
public class NumericRangeBuilder<TValue> : ISpecimenBuilder
    where TValue : struct, IComparable, IConvertible
{
    private readonly TValue _min;
    private readonly TValue _max;
    private readonly Func<PropertyInfo, bool> _predicate;

    public NumericRangeBuilder(
        TValue min,
        TValue max,
        Func<PropertyInfo, bool> predicate)
    {
        _min = min;
        _max = max;
        _predicate = predicate;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            propertyInfo.PropertyType == typeof(TValue) &&
            _predicate(propertyInfo))
        {
            return GenerateRandomValue();
        }

        return new NoSpecimen();
    }

    private TValue GenerateRandomValue()
    {
        var minDecimal = Convert.ToDecimal(_min);
        var maxDecimal = Convert.ToDecimal(_max);
        var range = maxDecimal - minDecimal;
        var randomValue = minDecimal + (decimal)Random.Shared.NextDouble() * range;

        return typeof(TValue).Name switch
        {
            nameof(Int32) => (TValue)(object)(int)randomValue,
            nameof(Int64) => (TValue)(object)(long)randomValue,
            nameof(Int16) => (TValue)(object)(short)randomValue,
            nameof(Byte) => (TValue)(object)(byte)randomValue,
            nameof(Single) => (TValue)(object)(float)randomValue,
            nameof(Double) => (TValue)(object)(double)randomValue,
            nameof(Decimal) => (TValue)(object)randomValue,
            _ => throw new NotSupportedException($"Type {typeof(TValue).Name} is not supported")
        };
    }
}
```text

### Fluent Interface Extension Methods

```csharp
public static class FixtureRangedNumericExtensions
{
    public static IFixture AddRandomRange<T, TValue>(
        this IFixture fixture,
        TValue min,
        TValue max,
        Func<PropertyInfo, bool> predicate)
        where TValue : struct, IComparable, IConvertible
    {
        fixture.Customizations.Insert(0,
            new NumericRangeBuilder<TValue>(min, max, predicate));
        return fixture;
    }
}
```text

### Complete Usage Example

```csharp
public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public double Rating { get; set; }
    public float Discount { get; set; }
}

[Fact]
public void Multiple_Numeric_Type_Range_Control()
{
    var fixture = new Fixture();

    fixture
        .AddRandomRange<Product, decimal>(
            50m, 500m,
            prop => prop.Name == "Price" && prop.DeclaringType == typeof(Product))
        .AddRandomRange<Product, int>(
            1, 50,
            prop => prop.Name == "Quantity" && prop.DeclaringType == typeof(Product))
        .AddRandomRange<Product, double>(
            1.0, 5.0,
            prop => prop.Name == "Rating" && prop.DeclaringType == typeof(Product))
        .AddRandomRange<Product, float>(
            0.0f, 0.5f,
            prop => prop.Name == "Discount" && prop.DeclaringType == typeof(Product));

    var products = fixture.CreateMany<Product>(10).ToList();

    products.Should().AllSatisfy(product =>
    {
        product.Price.Should().BeInRange(50m, 500m);
        product.Quantity.Should().BeInRange(1, 49);
        product.Rating.Should().BeInRange(1.0, 5.0);
        product.Discount.Should().BeInRange(0.0f, 0.5f);
    });
}
```text

## int vs DateTime Handling Differences

### Why DateTime Builders Work with Add()?

| Type       | Built-in Builders                                        | Priority Impact               |
| ---------- | ------------------------------------------------------- | ----------------------------- |
| `int`      | `RangeAttributeRelay`, `NumericSequenceGenerator`       | Gets intercepted, needs `Insert(0)` |
| `DateTime` | No specific builder                                     | Not intercepted, `Add()` works   |

## Best Practices

### Should Do

1. **Leverage DataAnnotations**
   - Fully utilize existing model validation rules
   - AutoFixture automatically generates data meeting constraints

2. **Use Random.Shared**
   - Avoid duplicate value issues
   - Thread-safe, better performance

3. **Insert(0) to Ensure Priority**
   - Custom numeric builders must use `Insert(0)`
   - Avoid being overridden by built-in builders

4. **Generic Design**
   - Create reusable generic builders
   - Use extension methods to provide fluent interface

### Should Avoid

1. **Ignoring Builder Priority**
   - Don't assume `Add()` will always work
   - Test and verify builders work correctly

2. **Overly Complex Logic**
   - Builders should maintain single responsibility
   - Complex business logic belongs in tests or service layer

3. **Using new Random()**
   - May produce duplicate values
   - Not thread-safe

## Code Templates

See example files in the [templates](./templates) folder:

- [dataannotations-integration.cs](./templates/dataannotations-integration.cs) - DataAnnotations automatic integration
- [custom-specimen-builders.cs](./templates/custom-specimen-builders.cs) - Custom ISpecimenBuilder implementations
- [numeric-range-extensions.cs](./templates/numeric-range-extensions.cs) - Generic numeric range builder and extension methods

## Relationship with Other Skills

- **autofixture-basics**: Prerequisite knowledge for this skill, need to master basic usage first
- **autodata-xunit-integration**: Next learning goal, integrate customization with xUnit
- **autofixture-nsubstitute-integration**: Advanced integration, combining Mock with custom data generation

## Reference Resources

### Original Articles

This skill content is distilled from the "Old School Software Engineer's Testing Practice - 30 Day Challenge" article series:

- **Day 11 - AutoFixture Advanced: Custom Test Data Generation Strategies**
  - Article: https://ithelp.ithome.com.tw/articles/10375153
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day11

### Official Documentation

- [AutoFixture GitHub](https://github.com/AutoFixture/AutoFixture)
- [AutoFixture Official Documentation](https://autofixture.github.io/)
- [ISpecimenBuilder Interface](https://autofixture.github.io/docs/fixture-customization/)
````
