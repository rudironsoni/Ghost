---
name: dotnet-testing-test-data-builder-pattern
description: |
  Test Data Builder Pattern complete implementation guide. Used when using builder pattern to create maintainable test data or simplify complex object test preparation. Covers fluent interface, semantic methods, default value design, and Builder composition patterns.
  Keywords: test data builder, builder pattern test, test data builder, object mother, fluent interface, fluent interface, UserBuilder, ProductBuilder, .With(), .Build(), AUser(), test data preparation, complex object creation, semantic testing
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: 'test-data-builder, builder-pattern, fluent-interface, test-readability'
  related_skills: 'autofixture-basics, bogus-fake-data, autofixture-bogus-integration'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# Test Data Builder Pattern

## Applicable Scenarios

Test Data Builder Pattern is a Builder Pattern variant specifically designed for testing, used to create clear,
maintainable, and semantically explicit test data. This pattern is especially suitable for handling complex objects with
multiple attributes, making test code more readable and reducing maintenance costs.

## Core Concepts

### What is Test Data Builder Pattern?

Test Data Builder Pattern is an improved version of Object Mother Pattern, mainly solving the following problems:

1. **Fixed test data problem**: Object Mother provides fixed test objects, difficult to adjust for specific test
   scenarios
2. **Unclear test intent**: When creating objects directly, test focus is easily obscured by large amounts of attribute
   settings
3. **Repeated code**: Similar object creation logic repeated in multiple tests

### Why Need Builder Pattern?

### Traditional test data creation problems

Too many parameter settings, unclear test intent.

### Using Builder Pattern improvement

Intent is explicit, only set properties test cares about.

## Implementation Guide

### Basic Builder Structure

A standard Test Data Builder should contain:

1. **Default values**: Provide reasonable defaults for all necessary properties
2. **Fluent interface**: Use `With*` method chain to set properties
3. **Semantic methods**: Provide meaningful default creators (like `AnAdminUser()`, `ARegularUser()`)
4. **Build method**: Finally create and return target object

### Complete Builder Example

````csharp
public class UserBuilder
{
    // Default values: provide reasonable defaults for all properties
    private string _name = "Default User";
    private string _email = "default@example.com";
    private int _age = 25;
    private List<string> _roles = new();
    private UserSettings _settings = new()
    {
        Theme = "Light",
        Language = "en-US"
    };
    private bool _isActive = true;
    private DateTime _createdAt = DateTime.UtcNow;

    // With* methods: fluent interface to set individual properties
    public UserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithAge(int age)
    {
        _age = age;
        return this;
    }

    public UserBuilder WithRole(string role)
    {
        _roles.Add(role);
        return this;
    }

    public UserBuilder WithRoles(params string[] roles)
    {
        _roles.AddRange(roles);
        return this;
    }

    public UserBuilder IsInactive()
    {
        _isActive = false;
        return this;
    }

    // Semantic default creators: provide quick creation methods for common scenarios
    public static UserBuilder AUser() => new();

    public static UserBuilder AnAdminUser() => new UserBuilder()
        .WithRoles("Admin", "User");

    public static UserBuilder ARegularUser() => new UserBuilder()
        .WithRole("User");

    // Build method: create final object
    public User Build()
    {
        return new User
        {
            Name = _name,
            Email = _email,
            Age = _age,
            Roles = _roles.ToArray(),
            Settings = _settings,
            IsActive = _isActive,
            CreatedAt = _createdAt
        };
    }
}
```text

## Using Builder in Tests

### Single Test Scenario

```csharp
[Fact]
public void CreateUser_ValidAdminUser_ShouldCreateSuccessfully()
{
    // Arrange - use Builder to create test data
    var adminUser = UserBuilder
        .AnAdminUser()
        .WithName("John Admin")
        .WithEmail("john.admin@company.com")
        .WithAge(35)
        .Build();

    var userService = new UserService();

    // Act
    var result = userService.CreateUser(adminUser);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("John Admin", result.Name);
    Assert.Contains("Admin", result.Roles);
}
```text

### Using with Theory

```csharp
public class UserValidationTests
{
    [Theory]
    [MemberData(nameof(GetUserScenarios))]
    public void ValidateUser_DifferentUserScenarios_ShouldReturnCorrectValidationResult(User user, bool expected)
    {
        var validator = new UserValidator();
        var result = validator.IsValid(user);
        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> GetUserScenarios()
    {
        // Valid user scenario
        yield return new object[]
        {
            UserBuilder.AUser()
                .WithName("Valid User")
                .WithEmail("valid@example.com")
                .WithAge(25)
                .Build(),
            true
        };

        // Invalid user scenario - empty name
        yield return new object[]
        {
            UserBuilder.AUser()
                .WithName("")
                .Build(),
            false
        };
    }
}
```text

## Best Practices

### 1. Provide Reasonable Default Values

Good practice: defaults make object in valid state.

### 2. Use Semantic Naming

Good practice: method names express test intent.

### 3. Composition Between Builders

Good practice: Builders can be combined.

### 4. Avoid Over-complication

Keep Builder simple, don't include complex business logic.

### 5. Unified Test Data Management

Good practice: create shared test data class.

## Comparison with Other Patterns

### Test Data Builder vs. Object Mother

| Characteristic     | Test Data Builder           | Object Mother         |
| -------- | --------------------------- | --------------------- |
| Flexibility     | Highly flexible, adjustable per test | Fixed test data     |
| Readability   | Fluent interface, explicit intent       | Need to view method implementation |
| Maintainability   | Centralized management, easy to modify       | Changes affect all tests   |
| Usage scenarios | Unit tests, scenario tests          | Simple integration tests        |

### Test Data Builder vs. AutoFixture

| Characteristic       | Test Data Builder       | AutoFixture               |
| ---------- | ----------------------- | ------------------------- |
| Control degree     | Full control over object creation     | Auto-generate, lower control |
| Setup complexity | Need to manually create Builder | Almost zero setup             |
| Test intent   | Very explicit             | Need additional explanation          |
| Suitable timing   | Tests needing precise control      | Bulk data generation, anonymous testing    |

## Practical Examples

Please refer to `templates/` directory for complete implementation examples.

## Reference Resources

### Original Articles

This skill content is extracted from "Old School Software Engineer's Testing Practice - 30 Day Challenge" series.

### Extended Reading

- **Test Data Builder Original Article**: [Test Data Builders: an alternative to the Object Mother pattern](http://www.natpryce.com/articles/000714.html) by Nat Pryce

### Related Skills

- `autofixture-basics` - Using AutoFixture to auto-generate test data
- `xunit-project-setup` - xUnit test project basic setup
- `test-naming-conventions` - Test naming conventions

## Summary

Test Data Builder Pattern is an important technique for writing maintainable tests:

- **When to use**: Test objects have multiple attributes, need to reuse test data, want to express clear intent
- **Core advantages**: Improve readability, reduce maintenance costs, enhance expressiveness
- **Notes**: Keep Builder simple, provide reasonable defaults, use semantic method names
````
