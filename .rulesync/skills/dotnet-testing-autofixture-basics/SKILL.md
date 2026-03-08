---
name: dotnet-testing-autofixture-basics
category: testing
subcategory: test-data
description: |
  Using AutoFixture to automatically generate test data basic skill. Used when quickly generating test objects, reducing boilerplate code, and implementing anonymous testing. Covers Fixture.Create, CreateMany, circular reference handling, and xUnit integration.
  Keywords: autofixture, fixture, auto-generate test data, test data generation, anonymous testing, anonymous testing, fixture.Create, CreateMany, fixture.Build, Create<T>, AutoFixture.Xunit2, OmitOnRecursionBehavior, IFixture, generate test data, generate test data
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: '.NET, testing, AutoFixture, test data, anonymous testing'
  related_skills: 'autodata-xunit-integration, autofixture-customization, autofixture-bogus-integration'
claudecode: {}
opencode: {}
codexcli:
  short-description: '.NET skill guidance for dotnet-testing-autofixture-basics'
copilot: {}
geminicli: {}
antigravity: {}
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# AutoFixture Basics: Automatically Generate Test Data

## Applicable Scenarios

AutoFixture is a test data auto-generation tool designed for the .NET platform. Its core concept is "Anonymous Testing".
This concept believes that most tests should not depend on specific data values, but should focus on verifying program
logic correctness.

### Why Do We Need AutoFixture?

Traditional test data preparation pain points:

1. **Too much boilerplate code**: 90% of code is preparing data, real test logic is buried
2. **Unclear test focus**: Hard to quickly understand what this test is validating
3. **Difficult maintenance**: When object structure changes, all related tests need modification
4. **Data dependency**: Tests may accidentally depend on specific data values
5. **Repeated code**: Same data preparation logic repeated in multiple tests

AutoFixture can be seen as the **automated evolution version of Test Data Builder Pattern**, automatically generating
complex test data, letting us focus on test logic itself.

## Install Packages

````xml
<PackageReference Include="AutoFixture" Version="4.18.1" />
<PackageReference Include="AutoFixture.Xunit2" Version="4.18.1" />
```text

Or install via command line:

```powershell
dotnet add package AutoFixture
dotnet add package AutoFixture.Xunit2
```text

## Basic Usage

### Fixture Class and Create<T>()

`Fixture` is AutoFixture's core class, providing automatic test data generation capability.

### CreateMany<T>() Generate Collections

Default generates 3 elements, or specify quantity with CreateMany<Product>(10).

### Complex Object Automatic Construction

AutoFixture can automatically construct complex object structures with nested objects and collections.

## Build<T>() Mode: Precise Control

When needing to control specific properties, use `Build<T>()` mode with `.With()` and `.Without()`.

## Circular Reference Handling

When objects contain circular references, use `OmitOnRecursionBehavior` to ignore them.

### Shared Base Class

Recommend creating base class to unify circular reference handling:

```csharp
public abstract class AutoFixtureTestBase
{
    protected Fixture CreateFixture()
    {
        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        return fixture;
    }
}
```text

## xUnit Integration

### Using Fixture to Share Customization

Setup common customization in constructor for reuse across tests.

### Combined with Theory Tests

AutoFixture works well with xUnit Theory tests for parameterized testing.

## Anonymous Testing Principles

### Core Concept

Tests should focus on "behavior" rather than "data".

## Evolution Comparison: Test Data Builder vs AutoFixture

### Traditional Test Data Builder

Requires manual Builder class creation (40+ lines).

### AutoFixture Way

Zero setup cost, focus on test logic (5 lines).

### Comparison Summary

| Aspect       | Test Data Builder      | AutoFixture        |
| ---------- | ---------------------- | ------------------ |
| Code lines | 40+ lines Builder + test  | 5 lines test           |
| Maintenance cost   | Object changes need Builder update | Auto-adapt to changes       |
| Development time   | Write Builder first then test  | Write test directly         |
| Bulk data   | Need loop               | `CreateMany(100)`  |
| Readability   | Business semantics clear           | Need to understand AutoFixture |

## Practical Application Scenarios

AutoFixture is commonly used for Entity testing (with Theory), DTO validation, and bulk data testing.

## Best Practices

### Should Do

1. Use anonymous testing concept - focus on test logic not specific data
2. Only fix specific values when necessary - use `Build<T>().With()` for key properties
3. Create shared base class - unify handling of circular references
4. Reasonable collection sizes - adjust `CreateMany()` quantity based on test purpose

### Should Avoid

1. Over-reliance on random values - don't assume specific content of random values
2. Ignore boundary values - still need explicit boundary case testing
3. Abuse auto-generation - simple tests may be clearer with fixed values

## Hybrid Strategy Recommendation

Combine advantages of both approaches using TestDataFactory.

## Code Templates

Please refer to [templates](./templates) folder for example files.

## Reference Resources

### Original Articles

This skill content is extracted from "Old School Software Engineer's Testing Practice - 30 Day Challenge" series:

- **Day 10 - AutoFixture Basics: Automatically Generate Test Data**
  - Ironman Article: https://ithelp.ithome.com.tw/articles/10375018
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day10

### Official Documentation

- [AutoFixture GitHub](https://github.com/AutoFixture/AutoFixture)
- [AutoFixture Official Website](https://autofixture.github.io/)
- [AutoFixture Quick Start](https://autofixture.github.io/docs/quick-start/)
- [AutoFixture NuGet](https://www.nuget.org/packages/autofixture)
````
