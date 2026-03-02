---
name: dotnet-testing-advanced-tunit-fundamentals
description: |
  Complete guide for TUnit new-generation testing framework. Use when creating test projects with TUnit or migrating from xUnit to TUnit. Covers Source Generator driven test discovery, AOT compilation support, fluent async assertions. Includes project creation, [Test] attribute, lifecycle management, parallel control, and xUnit syntax comparison.
  Keywords: TUnit, tunit testing, source generator testing, AOT testing, new generation testing framework, [Test], [Arguments], TUnit.Assertions, Assert.That, Before(Test), After(Test), NotInParallel, TUnit.Templates, Microsoft.Testing.Platform, TUnit vs xUnit, parallel execution
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: 'tunit, testing-framework, source-generator, aot, modern-testing, performance'
  related_skills: 'advanced-tunit-advanced, xunit-project-setup, unit-test-fundamentals'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# TUnit New Generation Testing Framework Introduction

## Applicable Scenarios

This skill covers TUnit new-generation .NET testing framework introduction basics, from framework features to actual
project creation and test writing.

### Core Topics

- TUnit framework features and design philosophy
- Source Generator driven test discovery
- AOT (Ahead-of-Time) compilation support
- Fluent async assertion system
- Project creation and package configuration
- Syntax differences compared to xUnit

---

## TUnit Framework Core Features

### 1. Source Generator Driven Test Discovery

TUnit's biggest difference from traditional testing frameworks is using Source Generator to complete test discovery at
**compile time**:

### Traditional Framework Approach (xUnit)

````csharp
// xUnit discovers all methods through reflection at runtime
public class TraditionalTests
{
    [Fact] // Only discovered at runtime
    public void TestMethod() { }
}
```text

### TUnit's Innovative Approach

```csharp
// TUnit generates test registration code at compile time through Source Generator
public class ModernTests
{
    [Test] // Processed and optimized at compile time
    public async Task TestMethod()
    {
        await Assert.That(true).IsTrue();
    }
}
```text

### Advantages

1. Avoid reflection cost: All test discovery completed at compile time
2. AOT compatible: Fully supports Native AOT compilation
3. Faster startup time: Especially in large test projects

### 2. AOT (Ahead-of-Time) Compilation Support

### JIT vs AOT Compilation Flow

```text
Traditional JIT: C# source → IL bytecode → JIT compile at runtime → machine code → execute
AOT:           C# source → directly generate at compile time → machine code → execute directly
```text

### AOT Compilation Advantages

- Ultra-fast startup time (no waiting for JIT compilation)
- Smaller memory footprint
- Predictable performance
- More suitable for containerized deployment

### Enable AOT Support

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```text

### Actual Performance Differences

```text
Traditional JIT compilation test startup time: ~1-2 seconds
TUnit AOT compilation test startup time: ~50-100 milliseconds
(Large projects can achieve 10-30x startup time improvement)
```text

### 3. Microsoft.Testing.Platform Adoption

TUnit is built on Microsoft's latest Microsoft.Testing.Platform, not the traditional VSTest platform:

- Lighter test runner
- Better parallel control mechanism
- Native support for latest IDE integration

### Important Notes

TUnit projects **do not need** and **should not** install `Microsoft.NET.Test.Sdk` package.

### 4. Default Parallel Execution

TUnit sets parallel execution as default and provides fine-grained control:

```csharp
// Default all tests execute in parallel
[Test]
public async Task ParallelTest1() { }

[Test]
public async Task ParallelTest2() { }

// Can control parallel behavior when needed
[Test]
[NotInParallel("DatabaseTests")]
public async Task DatabaseTest() { }
```text

---

## TUnit Project Creation

### Method One: Manual Creation (Understanding Underlying Architecture)

```bash
# Create project directory
mkdir TUnitDemo
cd TUnitDemo

# Create solution
dotnet new sln -n MyApp

# Create main project
dotnet new classlib -n MyApp.Core -o src/MyApp.Core

# Create test project (use console template)
dotnet new console -n MyApp.Tests -o tests/MyApp.Tests

# Add to solution
dotnet sln add src/MyApp.Core/MyApp.Core.csproj
dotnet sln add tests/MyApp.Tests/MyApp.Tests.csproj

# Add project reference
dotnet add tests/MyApp.Tests/MyApp.Tests.csproj reference src/MyApp.Core/MyApp.Core.csproj
```text

### Method Two: Using TUnit Template (Recommended)

```bash
# Install TUnit project templates
dotnet new install TUnit.Templates

# Create test project using TUnit template
dotnet new tunit -n MyApp.Tests -o tests/MyApp.Tests
```text

### Test Project csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- TUnit core packages -->
    <PackageReference Include="TUnit" Version="0.57.24" />
    <!-- Code coverage support -->
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.12.4" />
    <!-- TRX report support -->
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="1.4.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MyApp.Core\MyApp.Core.csproj" />
  </ItemGroup>

</Project>
```text

### GlobalUsings Configuration

```csharp
// GlobalUsings.cs
global using TUnit.Core;
global using TUnit.Assertions;
global using MyApp.Core;
```text

---

## Async Test Methods (Required)

TUnit **requires all test methods to be async**, this is a framework technical requirement:

```csharp
// ❌ Error: won't compile
[Test]
public void WrongTest()
{
    Assert.That(1 + 1).IsEqualTo(2);
}

// ✅ Correct: use async Task
[Test]
public async Task CorrectTest()
{
    await Assert.That(1 + 1).IsEqualTo(2);
}
```text

---

## Test Attributes and Parameterization

### Basic Test [Test]

TUnit uniformly uses `[Test]` attribute, unlike xUnit which distinguishes `[Fact]` and `[Theory]`:

```csharp
// TUnit: uniformly use [Test]
[Test]
public async Task Add_Input1And2_ShouldReturn3()
{
    var calculator = new Calculator();
    var result = calculator.Add(1, 2);
    await Assert.That(result).IsEqualTo(3);
}
```text

### Parameterized Tests [Arguments]

```csharp
// TUnit: use [Arguments] (equivalent to xUnit's [InlineData])
[Test]
[Arguments(1, 2, 3)]
[Arguments(-1, 1, 0)]
[Arguments(0, 0, 0)]
[Arguments(100, -50, 50)]
public async Task Add_MultipleInputs_ShouldReturnCorrectResult(int a, int b, int expected)
{
    var calculator = new Calculator();
    var result = calculator.Add(a, b);
    await Assert.That(result).IsEqualTo(expected);
}
```text

---

## TUnit.Assertions Assertion System

TUnit adopts fluent assertion design, all assertions are async. Supports equality, boolean, numeric comparison, string, collection, exception, and other assertions, and can combine conditions through `And` / `Or`.

```csharp
// Basic usage examples
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(email).Contains("@").And.EndsWith(".com");
await Assert.That(() => action()).Throws<InvalidOperationException>();
```text

> 📖 Complete assertion types and examples please refer to [TUnit Assertion System Detailed Description](references/tunit-assertions-detail.md)

---

## Test Lifecycle Management

TUnit supports constructor / `Dispose` pattern, and `[Before(Test)]`, `[Before(Class)]`, `[After(Test)]`, `[After(Class)]` and other attributes, providing more refined lifecycle control than xUnit.

```text
Execution order: Before(Class) → Constructor → Before(Test) → Test Method → After(Test) → Dispose → After(Class)
```text

> 📖 Complete lifecycle examples and attribute comparison table please refer to [Lifecycle Management Detailed Description](references/lifecycle-management.md)

---

## Parallel Execution Control

### NotInParallel Attribute

```csharp
// Default parallel execution
[Test]
public async Task ParallelTest1() { }

[Test]
public async Task ParallelTest2() { }

// Control specific tests not to run in parallel
[Test]
[NotInParallel("DatabaseTests")]
public async Task DatabaseTest1_NotInParallel()
{
    // This test won't run in parallel with other "DatabaseTests" group
}

[Test]
[NotInParallel("DatabaseTests")]
public async Task DatabaseTest2_NotInParallel()
{
    // Runs sequentially with DatabaseTest1
}
```text

---

## xUnit to TUnit Syntax Comparison

| Feature | xUnit | TUnit |
| ------- | ----- | ----- |
| **Basic Test** | `[Fact]` | `[Test]` |
| **Parameterized Test** | `[Theory]` + `[InlineData]` | `[Test]` + `[Arguments]` |
| **Basic Assertion** | `Assert.Equal(expected, actual)` | `await Assert.That(actual).IsEqualTo(expected)` |
| **Boolean Assertion** | `Assert.True(condition)` | `await Assert.That(condition).IsTrue()` |
| **Exception Test** | `Assert.Throws<T>(() => action())` | `await Assert.That(() => action()).Throws<T>()` |
| **Null Check** | `Assert.Null(value)` | `await Assert.That(value).IsNull()` |
| **String Check** | `Assert.Contains("text", fullString)` | `await Assert.That(fullString).Contains("text")` |

### Migration Example

### xUnit Original Code

```csharp
[Theory]
[InlineData("test@example.com", true)]
[InlineData("invalid", false)]
public void IsValidEmail_VariousInputs_ShouldReturnCorrectValidationResult(string email, bool expected)
{
    var result = _validator.IsValidEmail(email);
    Assert.Equal(expected, result);
}
```text

### TUnit Converted

```csharp
[Test]
[Arguments("test@example.com", true)]
[Arguments("invalid", false)]
public async Task IsValidEmail_VariousInputs_ShouldReturnCorrectValidationResult(string email, bool expected)
{
    var result = _validator.IsValidEmail(email);
    await Assert.That(result).IsEqualTo(expected);
}
```text

### Main Changes

1. `[Theory]` → `[Test]`
2. `[InlineData]` → `[Arguments]`
3. Method changed to `async Task`
4. All assertions prefixed with `await`
5. Fluent assertion syntax

---

## Execution and Debugging

### CLI Execution

```bash
# Build project
dotnet build

# Run all tests
dotnet test

# Verbose output
dotnet test --verbosity normal

# Generate coverage report
dotnet test --coverage

# Filter specific tests
dotnet test --filter "ClassName=CalculatorTests"
dotnet test --filter "TestName~Add"
```text

### AOT Compilation Execution

```bash
# Publish as AOT compiled version
dotnet publish -c Release -p:PublishAot=true

# Execute AOT compiled tests
.\bin\Release\net9.0\publish\MyApp.Tests.exe
```text

### IDE Integration

### Visual Studio 2022

- Version 17.13+ required
- Enable "Use testing platform server mode"

### VS Code

- Install C# Dev Kit extension
- Enable "Use Testing Platform Protocol"

### JetBrains Rider

- Enable "Testing Platform support"

---

## Performance Comparison

| Scenario | xUnit | TUnit | TUnit AOT | Performance Gain |
| -------- | ----- | ----- | --------- | ---------------- |
| **Simple Test Execution** | 1,400ms | 1,000ms | 60ms | 23x (AOT) |
| **Async Test** | 1,400ms | 930ms | 26ms | 54x (AOT) |
| **Parallel Test** | 1,425ms | 999ms | 54ms | 26x (AOT) |

---

## Common Issues and Solutions

### Issue 1: Package Compatibility

**Error:** Installed `Microsoft.NET.Test.Sdk` causing tests not discoverable

**Solution:** Remove `Microsoft.NET.Test.Sdk`, TUnit uses new testing platform

### Issue 2: IDE Integration Issues

**Symptom:** Tests not displaying or executing in IDE

### Solution

1. Confirm IDE version supports Microsoft.Testing.Platform
2. Enable relevant preview features
3. Reload project or restart IDE

### Issue 3: Async Assertion Forgotten

**Symptom:** Compilation errors or assertions not executing properly

**Solution:** All assertions need `await`, test methods must be `async Task`

---

## Applicable Scenario Assessment

### Suitable for TUnit

1. **New Projects**: No legacy baggage
2. **High Performance Requirements**: Large test suites (1000+ tests)
3. **Advanced Tech Stack**: Using .NET 8+, planning AOT adoption
4. **Heavy CI/CD Usage**: Test execution time directly impacts deployment frequency
5. **Containerized Deployment**: Fast startup time is important

### Not Recommended for Now

1. **Legacy Projects**: Already have large amounts of xUnit tests
2. **Conservative Teams**: Need stability over innovation
3. **Complex Test Ecosystem**: Heavy use of xUnit specific packages
4. **Old .NET Versions**: Still on .NET 6/7

---

## Reference Resources

### Original Articles

This skill content is distilled from the "Old School Software Engineer's Testing Practice - 30 Day Challenge" article series:

- **Day 28 - TUnit Introduction - Next Generation .NET Testing Framework Exploration**
  - Article: https://ithelp.ithome.com.tw/articles/10377828
  - Sample code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day28

### Official Resources

- [TUnit Official Website](https://tunit.dev/)
- [TUnit GitHub](https://github.com/thomhurst/TUnit)
- [Migration from xUnit Guide](https://tunit.dev/docs/migration/xunit)

### Microsoft Official Documentation

- [Microsoft.Testing.Platform Introduction](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
- [Native AOT Deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot)
````
