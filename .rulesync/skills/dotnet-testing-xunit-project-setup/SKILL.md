---
name: dotnet-testing-xunit-project-setup
description: |
  xUnit test project creation and setup specialized skill. Used when creating test projects, setting up project structure, configuring NuGet packages, and organizing test folders. Covers csproj setup, package management, project structure, xunit.runner.json configuration, etc.
  Keywords: xunit project, xunit setup, create test project, test project setup, create test project, project structure, project structure, folder structure, xunit package, nuget packages, testing packages, how to create test project, xunit configuration
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: '.NET, testing, xUnit, project setup, configuration'
  related_skills: 'unit-test-fundamentals, test-naming-conventions, awesome-assertions-guide'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# xUnit Test Project Setup Guide

## Applicable Scenarios

Use this skill when asked to perform the following tasks:

- Create new xUnit test projects
- Set up .NET test project structure
- Configure xUnit dependencies and NuGet packages
- Plan test project folder organization
- Set up code coverage collection tools
- Understand test project csproj settings

## Project Structure Best Practices

### Recommended Solution Structure

````text
MyProject/
├── src/                          # Main code directory
│   └── MyProject.Core/
│       ├── MyProject.Core.csproj
│       ├── Calculator.cs
│       ├── Services/
│       └── Models/
├── tests/                        # Test code directory
│   └── MyProject.Core.Tests/
│       ├── MyProject.Core.Tests.csproj
│       ├── CalculatorTests.cs
│       ├── Services/
│       └── Models/
└── MyProject.sln
```text

### Structure Principles:

1. **Separate src and tests**: Clearly distinguish production code from test code
2. **Naming convention**: Test project names are `{MainProjectName}.Tests`
3. **Directory mapping**: Test project folder structure should map to main project structure
4. **One-to-one mapping**: Each main project should have a corresponding test project

## Creating xUnit Test Projects

### Method 1: Using .NET CLI (Recommended)

#### Step 1: Create Solution and Projects

```powershell
# Create solution
dotnet new sln -n MyProject

# Create main project (class library)
dotnet new classlib -n MyProject.Core -o src/MyProject.Core

# Create test project (xUnit template)
dotnet new xunit -n MyProject.Core.Tests -o tests/MyProject.Core.Tests

# Add projects to solution
dotnet sln add src/MyProject.Core/MyProject.Core.csproj
dotnet sln add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj

# Create project reference (test project references main project)
dotnet add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj reference src/MyProject.Core/MyProject.Core.csproj
```text

#### Step 2: Install Code Coverage Tools

```powershell
# Switch to test project directory
cd tests/MyProject.Core.Tests

# Install coverlet.collector (for collecting code coverage)
dotnet add package coverlet.collector
```text

### Method 2: Using Visual Studio

1. **Create Solution**
   - File → New → Project
   - Select "Blank Solution"
   - Name as project name

2. **Add Main Project**
   - Right-click solution → Add → New Project
   - Select "Class Library"
   - Name as `MyProject.Core`

3. **Add Test Project**
   - Right-click solution → Add → New Project
   - Search and select "xUnit Test Project"
   - Name as `MyProject.Core.Tests`

4. **Set Project Reference**
   - Right-click test project → Add → Project Reference
   - Check main project

## xUnit Test Project csproj Settings

### Standard xUnit Test Project csproj

Please refer to the `templates/xunit-test-project.csproj` template file in the same directory.

### Core Dependency Packages:

1. **xunit** (2.9.3+)
   - Core package of xUnit testing framework
   - Provides `[Fact]`, `[Theory]` and other test attributes
   - Includes `Assert` class and assertion methods

2. **xunit.runner.visualstudio** (3.0.0+)
   - Visual Studio Test Explorer integration
   - Allows tests to be discovered and executed in VS Code, Visual Studio, Rider
   - Supports real-time test result display

3. **Microsoft.NET.Test.Sdk** (17.12.0+)
   - SDK for .NET testing platform
   - Allows `dotnet test` command to execute tests
   - Supports test result reporting and test discovery

4. **coverlet.collector** (6.0.3+)
   - Code coverage collection tool
   - Integrates with `dotnet test`
   - Generates coverage reports (supports Cobertura, OpenCover, etc.)

### Important csproj Settings

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
```text

### Settings Explanation:

- `IsPackable=false`: Test projects should not be packaged as NuGet packages
- `IsTestProject=true`: Explicitly marked as test project, for tool recognition
- `Nullable=enable`: Enable nullable reference type checking

## Test Class Basic Structure

### Standard Test Class Template

```csharp
namespace MyProject.Core.Tests;

public class CalculatorTests
{
    private readonly Calculator _calculator;

    // Constructor: called before each test executes
    public CalculatorTests()
    {
        _calculator = new Calculator();
    }
}
```text

### Test Lifecycle (Important Concept)

xUnit's test isolation mechanism:

1. **Each test method creates a new test class instance**
2. **Constructor**: Called before each test method executes
3. **Test method**: Executes test logic
4. **Dispose()**: If implementing `IDisposable`, called after each test method executes

### Execution Order Example:

```text
Execute Test1:
  → Constructor → Test1 method → Dispose()

Execute Test2:
  → Constructor → Test2 method → Dispose()
```text

This ensures **test isolation**, conforming to FIRST principle's **I (Independent)**.

## Running Tests

### Using .NET CLI

```powershell
# Build project
dotnet build

# Run all tests
dotnet test

# Run tests and collect code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj

# Run tests with verbose output
dotnet test --verbosity detailed
```text

### Running in IDEs

#### VS Code (Requires C# Dev Kit)

1. Install [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
2. Open Test Explorer
3. Click the play button next to tests to execute

#### Visual Studio

1. Open Test Explorer (Test → Test Explorer)
2. Click "Run All" to execute all tests
3. Can right-click single test to execute or debug

#### JetBrains Rider

1. Green run icon appears next to test methods
2. Click to execute or debug tests
3. Use Unit Tests window to manage tests

## Project Reference Setup Principles

### Reference Direction Rules

```text
Test Project → Main Project   ✅ Correct
Main Project → Test Project   ❌ Wrong
```text

### Test projects should reference main projects, but main projects should never reference test projects.

### Set Project Reference

```powershell
# Let test project reference main project
dotnet add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj reference src/MyProject.Core/MyProject.Core.csproj
```text

In csproj this will produce:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\MyProject.Core\MyProject.Core.csproj" />
</ItemGroup>
```text

## Advanced: Organizing Multiple Test Projects

When projects grow, you may need multiple test projects:

```text
MyProject/
├── src/
│   ├── MyProject.Core/
│   ├── MyProject.Web/
│   └── MyProject.Infrastructure/
├── tests/
│   ├── MyProject.Core.Tests/           # Unit tests
│   ├── MyProject.Web.Tests/            # Web layer tests
│   ├── MyProject.Infrastructure.Tests/ # Infrastructure tests
│   └── MyProject.Integration.Tests/    # Integration tests
└── MyProject.sln
```text

### Naming Convention Suggestions:

- `*.Tests` - Unit tests
- `*.Integration.Tests` - Integration tests
- `*.Acceptance.Tests` - Acceptance tests
- `*.Performance.Tests` - Performance tests

### Actual Project Naming Conventions

In actual work projects, it is recommended to use more explicit naming formats to distinguish test types:

### Recommended Naming Formats:

```text
MyProject/
├── src/
│   ├── MyProject.Core/
│   └── MyProject.WebApi/
├── tests/
│   ├── MyProject.Core.Test.Unit/              # Unit tests (explicitly labeled)
│   ├── MyProject.WebApi.Test.Unit/            # WebApi unit tests
│   └── MyProject.WebApi.Test.Integration/     # WebApi integration tests
└── MyProject.sln
```text

### Naming Rules:

- **Unit Tests**: `{ProjectName}.Test.Unit`
  - Example: `MyProject.Core.Test.Unit`
  - Characteristics: No external dependencies (database, API, file system, etc.)
  - Execution speed: Fast (millisecond level)

- **Integration Tests**: `{ProjectName}.Test.Integration`
  - Example: `MyProject.WebApi.Test.Integration`
  - Characteristics: Test integration of multiple components, may depend on external resources
  - Execution speed: Slower (second level)

### Advantages of This Naming:

1. **Clarity**: Can distinguish test types at a glance
2. **Execution Strategy**: Can execute in phases in CI/CD

   ```powershell
   # Quick feedback: only run unit tests
   dotnet test --filter "FullyQualifiedName~.Test.Unit"

   # Complete validation: run integration tests
   dotnet test --filter "FullyQualifiedName~.Test.Integration"
```text

3. **Dependency Management**: Integration tests can have different package dependencies (like Testcontainers)
4. **Team Collaboration**: New members can quickly understand project structure

### CLI Creation Example:

```powershell
# Create unit test project
dotnet new xunit -n MyProject.Core.Test.Unit -o tests/MyProject.Core.Test.Unit
dotnet add tests/MyProject.Core.Test.Unit reference src/MyProject.Core

# Create integration test project
dotnet new xunit -n MyProject.WebApi.Test.Integration -o tests/MyProject.WebApi.Test.Integration
dotnet add tests/MyProject.WebApi.Test.Integration reference src/MyProject.WebApi
```text

> **💡 Tip**: Although this example uses `.Tests` format for simplicity, in actual projects it is strongly recommended to use the more explicit `.Test.Unit` and `.Test.Integration` format.

## Common Issues and Solutions

### Q1: Test discovery fails, Test Explorer cannot see tests?

### Checklist:

1. Confirm `xunit.runner.visualstudio` package is installed
2. Confirm `Microsoft.NET.Test.Sdk` package is installed
3. Execute `dotnet build` to rebuild
4. Restart IDE or reload Test Explorer

### Q2: Tests can run in CLI but not in IDE?

### Solutions:

- Confirm IDE has relevant extensions installed (VS Code needs C# Dev Kit)
- Clear cache: delete `bin/` and `obj/` folders then rebuild
- Check `.csproj` `IsTestProject` property is `true`

### Q3: How to use Internal classes in test projects?

Add to main project's `.csproj` or `AssemblyInfo.cs`:

```csharp
[assembly: InternalsVisibleTo("MyProject.Core.Tests")]
```text

Or in csproj:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="MyProject.Core.Tests" />
</ItemGroup>
```text

## Template Files

Please refer to template files in the same directory for quick project creation:

- `templates/project-structure.md` - Complete project structure example
- `templates/xunit-test-project.csproj` - xUnit test project csproj template

## Checklist

When creating xUnit test projects, please confirm the following items:

- [ ] Test project named `{MainProjectName}.Tests`
- [ ] Test project located under `tests/` directory
- [ ] Installed `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` packages
- [ ] Installed `coverlet.collector` for code coverage
- [ ] Test project references main project
- [ ] `IsPackable` set to `false`
- [ ] `IsTestProject` set to `true`
- [ ] Can execute `dotnet test` successfully
- [ ] IDE's Test Explorer can discover tests

## Reference Resources

### Original Articles

This skill content is extracted from "Old School Software Engineer's Testing Practice - 30 Day Challenge" series:

- **Day 02 - xUnit Framework Deep Dive**
  - Ironman Article: https://ithelp.ithome.com.tw/articles/10373952
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day02

- **Day 03 - xUnit Advanced Features and Test Data Management**
  - Ironman Article: https://ithelp.ithome.com.tw/articles/10374064
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day03

### Official Documentation

- [xUnit Official Documentation](https://xunit.net/)
- [.NET Testing Best Practices](https://learn.microsoft.com/dotnet/core/testing/)

### Related Skills

- `unit-test-fundamentals` - Unit test fundamentals and FIRST principles
- `test-naming-conventions` - Test naming conventions
- `code-coverage-analysis` - Code coverage analysis
````
