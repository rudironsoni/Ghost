---
name: dotnet-testing-unit-test-fundamentals
description: |
  .NET unit test fundamentals and FIRST principles specialized skill. Used when creating unit tests, understanding testing basics, learning 3A Pattern, and mastering testing best practices. Covers FIRST principles, AAA Pattern, Fact/Theory, testing pyramid, etc.
  Keywords: unit test, unit test, unit testing, test fundamentals, testing fundamentals, FIRST principle, FIRST principle, 3A pattern, AAA pattern, Arrange Act Assert, Fact, Theory, InlineData, how to write tests, testing best practices, create unit test
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: '.NET, testing, unit test, FIRST, AAA pattern, xUnit'
  related_skills: 'xunit-project-setup, test-naming-conventions, awesome-assertions-guide'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# .NET Unit Test Fundamentals Guide

## Applicable Scenarios

Use this skill when asked to perform the following tasks:

- Create unit tests for .NET classes or methods
- Review or improve existing test quality
- Design test cases following FIRST principles
- Explain test naming conventions and best practices
- Write tests using xUnit

## FIRST Principles

Every unit test **must** conform to the following principles:

### F - Fast

Test execution time should be in milliseconds, not dependent on external resources.

````csharp
[Fact] // Fast: no external dependencies, executes quickly
public void Add_WithInput1And2_ShouldReturn3()
{
    // Pure memory operations, no I/O or network latency
    var calculator = new Calculator();
    var result = calculator.Add(1, 2);
    Assert.Equal(3, result);
}
```text

### I - Independent

Tests should not have dependencies, each test creates a new instance.

```csharp
[Fact] // Independent: each test creates a new instance
public void Increment_StartingFrom0_ShouldReturn1()
{
    var counter = new Counter(); // Each test creates a new instance, not affected by other tests
    counter.Increment();
    Assert.Equal(1, counter.Value);
}
```text

### R - Repeatable

Should produce the same results in any environment, not dependent on external state.

```csharp
[Fact] // Repeatable: same results every execution
public void Increment_ExecutedMultipleTimes_ShouldProduceConsistentResults()
{
    var counter = new Counter();
    counter.Increment();
    counter.Increment();
    counter.Increment();

    // This test produces the same result every execution
    Assert.Equal(3, counter.Value);
}
```text

### S - Self-Validating

Test results should be clearly pass or fail, using clear assertions.

```csharp
[Fact] // Self-Validating: clear validation
public void IsValidEmail_WithValidInput_ShouldReturnTrue()
{
    var emailHelper = new EmailHelper();
    var result = emailHelper.IsValidEmail("test@example.com");

    Assert.True(result); // Clear pass or fail
}
```text

### T - Timely

Tests should be written before or simultaneously with production code, ensuring code testability.

## 3A Pattern Structure

Every test method **must** follow the Arrange-Act-Assert pattern:

```csharp
[Fact]
public void Add_WithNegativeAndPositiveNumbers_ShouldReturnCorrectResult()
{
    // Arrange - prepare test data and dependencies
    var calculator = new Calculator();
    const int a = -5;
    const int b = 3;
    const int expected = -2;

    // Act - execute the method under test
    var result = calculator.Add(a, b);

    // Assert - verify results match expectations
    Assert.Equal(expected, result);
}
```text

### Block Responsibilities

| Block        | Responsibility                           | Notes                            |
| ----------- | ------------------------------ | ----------------------------------- |
| **Arrange** | Prepare objects, data, and Mocks needed for testing | Use `const` to declare constant values, improve readability |
| **Act**     | Execute the method under test               | Usually only one line, calling the method under test          |
| **Assert**  | Verify results                       | Each test only validates one behavior              |

## Test Naming Conventions

Use the following format to name test methods:

```text
[MethodUnderTest]_[TestScenario]_[ExpectedBehavior]
```text

### Naming Examples

| Method Name                                       | Description         |
| ---------------------------------------------- | ------------ |
| `Add_WithInput1And2_ShouldReturn3`                         | Test normal input |
| `Add_WithNegativeAndPositiveNumbers_ShouldReturnCorrectResult`            | Test boundary conditions |
| `Divide_WithInput10And0_ShouldThrowDivideByZeroException` | Test exception case |
| `IsValidEmail_WithNullInput_ShouldReturnFalse`          | Test invalid input |
| `GetDomain_WithValidEmail_ShouldReturnDomainName`       | Test return value   |

> 💡 **Tip**: Using Chinese naming can make test reports more readable, especially during team communication.

## xUnit Test Attributes

### [Fact] - Single Test Case

Used for testing a single scenario:

```csharp
[Fact]
public void Add_WithInput0And0_ShouldReturn0()
{
    var calculator = new Calculator();
    var result = calculator.Add(0, 0);
    Assert.Equal(0, result);
}
```text

### [Theory] + [InlineData] - Parameterized Tests

Used for testing multiple input combinations:

```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(-1, 1, 0)]
[InlineData(0, 0, 0)]
[InlineData(100, -50, 50)]
public void Add_WithVariousNumberCombinations_ShouldReturnCorrectResult(int a, int b, int expected)
{
    var calculator = new Calculator();
    var result = calculator.Add(a, b);
    Assert.Equal(expected, result);
}
```text

### Testing Multiple Invalid Inputs

```csharp
[Theory]
[InlineData("invalid-email")]
[InlineData("@example.com")]
[InlineData("test@")]
[InlineData("test.example.com")]
public void IsValidEmail_WithInvalidEmailFormats_ShouldReturnFalse(string invalidEmail)
{
    var emailHelper = new EmailHelper();
    var result = emailHelper.IsValidEmail(invalidEmail);
    Assert.False(result);
}
```text

## Exception Testing

Test expected exception throwing scenarios:

```csharp
[Fact]
public void Divide_WithInput10And0_ShouldThrowDivideByZeroException()
{
    // Arrange
    var calculator = new Calculator();
    const decimal dividend = 10m;
    const decimal divisor = 0m;

    // Act & Assert
    var exception = Assert.Throws<DivideByZeroException>(
        () => calculator.Divide(dividend, divisor)
    );

    // Verify exception message
    Assert.Equal("Divisor cannot be zero", exception.Message);
}
```text

## Test Project Structure

Recommended project structure:

```text
Solution/
├── src/
│   └── MyProject/
│       ├── Calculator.cs
│       └── MyProject.csproj
└── tests/
    └── MyProject.Tests/
        ├── CalculatorTests.cs
        └── MyProject.Tests.csproj
```text

## Test Project Template (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="coverlet.collector" Version="6.0.4">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
        <PackageReference Include="xunit" Version="2.9.3" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <Using Include="Xunit" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\MyProject\MyProject.csproj" />
    </ItemGroup>

</Project>
```text

## Common Assertion Methods

| Assertion Method                            | Purpose             |
| ----------------------------------- | ---------------- |
| `Assert.Equal(expected, actual)`    | Verify equality         |
| `Assert.NotEqual(expected, actual)` | Verify inequality       |
| `Assert.True(condition)`            | Verify condition is true     |
| `Assert.False(condition)`           | Verify condition is false     |
| `Assert.Null(object)`               | Verify is null      |
| `Assert.NotNull(object)`            | Verify is not null    |
| `Assert.Throws<T>(action)`          | Verify throws specific exception |
| `Assert.Empty(collection)`          | Verify collection is empty     |
| `Assert.Contains(item, collection)` | Verify collection contains item |

## Test Generation Checklist

When generating tests for a method, ensure coverage of:

- [ ] **Happy Path** - Standard input produces expected output
- [ ] **Boundary Conditions** - Minimum, maximum values, zero, empty strings
- [ ] **Invalid Input** - null, negative numbers, wrong formats
- [ ] **Exception Cases** - Scenarios expected to throw exceptions

## Reference Resources

### Original Articles

This skill content is extracted from "Old School Software Engineer's Testing Practice - 30 Day Challenge" series:

- **Day 01 - Old School Engineer's Testing Enlightenment**
  - Ironman Article: https://ithelp.ithome.com.tw/articles/10373888
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day01
````
