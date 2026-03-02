---
name: dotnet-testing-advanced-aspnet-integration-testing
description: |
  Specialized skill for ASP.NET Core integration testing. Use when testing Web API endpoints, HTTP request/response, middleware, and dependency injection. Covers WebApplicationFactory, TestServer, HttpClient testing, in-memory database configuration.
  Keywords: integration testing, web api testing, WebApplicationFactory, TestServer, HttpClient testing, controller testing, endpoint testing, RESTful API testing, Microsoft.AspNetCore.Mvc.Testing, CreateClient, ConfigureWebHost, AwesomeAssertions.Web, Be200Ok, Be404NotFound, middleware testing, dependency injection testing
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: '.NET, testing, ASP.NET Core, integration testing, WebApplicationFactory'
  related_skills: 'advanced-webapi-integration-testing, advanced-testcontainers-database, advanced-aspire-testing'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# ASP.NET Core Integration Testing Guide

## Applicable Scenarios

This skill guides how to create effective integration tests in ASP.NET Core, using `WebApplicationFactory<T>` and
`TestServer` to test complete HTTP request/response flows.

### Applicable Scenarios

- **Web API Endpoint Testing**: Validate RESTful API CRUD operations
- **HTTP Request/Response Validation**: Test complete request processing pipeline
- **Middleware Testing**: Validate Authentication, Authorization, Logging, etc.
- **Dependency Injection Validation**: Ensure DI container configuration is correct
- **Route Configuration Validation**: Ensure URL routes correctly map to controller actions
- **Model Binding Testing**: Validate request content correctly binds to models

### Required Packages

````xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="AwesomeAssertions" Version="9.1.0" />
<PackageReference Include="AwesomeAssertions.Web" Version="1.9.6" />
<PackageReference Include="System.Net.Http.Json" Version="9.0.8" />
```text

> ⚠️ **Important Reminder**: When using `AwesomeAssertions`, must install `AwesomeAssertions.Web`, not `FluentAssertions.Web`.

---

## Core Concepts

### Two Definitions of Integration Testing

### Definition One: Multi-Object Collaboration Testing

> Integrate two or more classes and test whether they work correctly together. Test cases must span multiple class objects.

### Definition Two: External Resource Integration Testing

> Uses external resources such as databases, external services, files, requires special handling of test environment, etc.

### Why Integration Testing?

- **Ensure multiple modules work correctly after integration**
- **Integration points not covered by unit tests**: Routing, Middleware, Request/Response Pipeline
- **WebApplication does too much integration and configuration, unit tests cannot cover everything**
- **Confirm proper exception handling to reduce more problems**

### Testing Pyramid Position

| Test Type | Test Scope | Execution Speed | Maintenance Cost | Recommended Ratio |
| --------- | ---------- | --------------- | ---------------- | ----------------- |
| Unit Test | Single class/method | Very fast | Low | 70% |
| Integration Test | Multiple components | Medium | Medium | 20% |
| End-to-End Test | Complete flow | Slow | High | 10% |

---

## Principle One: Use WebApplicationFactory to Create Test Environment

### Basic Usage

```csharp
public class BasicIntegrationTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BasicIntegrationTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_HomePage_ShouldReturnSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
```text

### Custom WebApplicationFactory

```csharp
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove original database configuration
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));

            // Add in-memory database
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Replace external services with test versions
            services.Replace(ServiceDescriptor.Scoped<IEmailService, TestEmailService>());
        });

        // Set test environment
        builder.UseEnvironment("Testing");
    }
}
```text

---

## Principle Two: Use AwesomeAssertions.Web to Validate HTTP Responses

### HTTP Status Code Assertions

```csharp
response.Should().Be200Ok();          // HTTP 200
response.Should().Be201Created();     // HTTP 201
response.Should().Be204NoContent();   // HTTP 204
response.Should().Be400BadRequest();  // HTTP 400
response.Should().Be404NotFound();    // HTTP 404
response.Should().Be500InternalServerError();  // HTTP 500
```text

### Satisfy<T> Strongly-Typed Validation

```csharp
[Fact]
public async Task GetShipper_WhenShipperExists_ShouldReturnSuccessResult()
{
    // Arrange
    await CleanupDatabaseAsync();
    var shipperId = await SeedShipperAsync("SF Express", "02-2345-6789");

    // Act
    var response = await Client.GetAsync($"/api/shippers/{shipperId}");

    // Assert
    response.Should().Be200Ok()
            .And
            .Satisfy<SuccessResultOutputModel<ShipperOutputModel>>(result =>
            {
                result.Status.Should().Be("Success");
                result.Data.Should().NotBeNull();
                result.Data!.ShipperId.Should().Be(shipperId);
                result.Data.CompanyName.Should().Be("SF Express");
                result.Data.Phone.Should().Be("02-2345-6789");
            });
}
```text

### Comparison with Traditional Approach

```csharp
// ❌ Traditional approach - verbose and error-prone
response.IsSuccessStatusCode.Should().BeTrue();
var content = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<SuccessResultOutputModel<ShipperOutputModel>>(content,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
result.Should().NotBeNull();
result!.Status.Should().Be("Success");

// ✅ Using Satisfy<T> - concise and intuitive
response.Should().Be200Ok()
        .And
        .Satisfy<SuccessResultOutputModel<ShipperOutputModel>>(result =>
        {
            result.Status.Should().Be("Success");
            result.Data!.CompanyName.Should().Be("Test Company");
        });
```text

---

## Principle Three: Use System.Net.Http.Json to Simplify JSON Operations

### PostAsJsonAsync Simplifies POST Requests

```csharp
// ❌ Traditional approach
var createParameter = new ShipperCreateParameter { CompanyName = "Test Company", Phone = "02-1234-5678" };
var jsonContent = JsonSerializer.Serialize(createParameter);
var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
var response = await client.PostAsync("/api/shippers", content);

// ✅ Modern approach
var createParameter = new ShipperCreateParameter { CompanyName = "Test Company", Phone = "02-1234-5678" };
var response = await client.PostAsJsonAsync("/api/shippers", createParameter);
```text

### ReadFromJsonAsync Simplifies Response Reading

```csharp
// ❌ Traditional approach
var responseContent = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<SuccessResultOutputModel<ShipperOutputModel>>(responseContent,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

// ✅ Modern approach
var result = await response.Content.ReadFromJsonAsync<SuccessResultOutputModel<ShipperOutputModel>>();
```text

---

## Three Levels of Integration Testing Strategy

### Level 1: Simple WebApi Project

**Characteristics**:

- No database, Service, or Repository dependencies
- Simplest, basic WebApi website project
- Directly use `WebApplicationFactory<Program>` for testing

**Testing Focus**:

- Input/output validation for each API
- HTTP verbs and routing correctness
- Model binding and serialization
- Status code and response format validation

```csharp
public class BasicApiControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BasicApiControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatus_ShouldReturnOK()
    {
        // Act
        var response = await _client.GetAsync("/api/status");

        // Assert
        response.Should().Be200Ok();
    }
}
```text

### Level 2: WebApi Project with Service Dependencies

**Characteristics**:

- No database, but has Service dependencies
- Use NSubstitute to create Service stubs
- Configure dependency injection in tests

```csharp
public class ServiceStubWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IExampleService _serviceStub;

    public ServiceStubWebApplicationFactory(IExampleService serviceStub)
    {
        _serviceStub = serviceStub;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IExampleService>();
            services.AddScoped(_ => _serviceStub);
        });
    }
}

public class ServiceDependentControllerTests
{
    [Fact]
    public async Task GetData_ShouldReturnServiceData()
    {
        // Arrange
        var serviceStub = Substitute.For<IExampleService>();
        serviceStub.GetDataAsync().Returns("Test Data");

        var factory = new ServiceStubWebApplicationFactory(serviceStub);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/data");

        // Assert
        response.Should().Be200Ok();
    }
}
```text

### Level 3: Full WebApi Project

**Characteristics**:

- Complete Solution architecture
- Contains real database operations
- Use InMemory or real test database

```csharp
public class FullDatabaseWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove original database configuration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Create database and add test data
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.Database.EnsureCreated();
        });
    }
}
```text

---

## Test Base Class Pattern

### Create Reusable Test Base Class

```csharp
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase()
    {
        Factory = new CustomWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    protected async Task<int> SeedShipperAsync(string companyName, string phone = "02-12345678")
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var shipper = new Shipper
        {
            CompanyName = companyName,
            Phone = phone,
            CreatedAt = DateTime.UtcNow
        };

        context.Shippers.Add(shipper);
        await context.SaveChangesAsync();

        return shipper.ShipperId;
    }

    protected async Task CleanupDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Shippers.RemoveRange(context.Shippers);
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
```text

---

## CRUD Operation Testing Examples

Complete CRUD operation testing code (GET, POST, validation errors, collection queries) please refer to 📄 **[CRUD Operation Testing Complete Examples](references/crud-test-examples.md)**

---

## Project Structure Recommendations

```text
tests/
├── Sample.WebApplication.UnitTests/           # Unit tests
├── Sample.WebApplication.Integration.Tests/   # Integration tests
│   ├── Controllers/                           # Controller integration tests
│   │   └── ShippersControllerTests.cs
│   ├── Infrastructure/                        # Test infrastructure
│   │   └── CustomWebApplicationFactory.cs
│   ├── IntegrationTestBase.cs                 # Test base class
│   └── GlobalUsings.cs
└── Sample.WebApplication.E2ETests/            # End-to-end tests
```text

---

## Package Compatibility Troubleshooting

### Common Errors

```text
error CS1061: 'ObjectAssertions' does not contain a definition for 'Be200Ok'
```text

### Solutions

| Base Assertion Library | Correct Package |
| ---------------------- | --------------- |
| FluentAssertions < 8.0.0 | FluentAssertions.Web |
| FluentAssertions >= 8.0.0 | FluentAssertions.Web.v8 |
| AwesomeAssertions >= 8.0.0 | AwesomeAssertions.Web |

```xml
<!-- Correct: using AwesomeAssertions should install AwesomeAssertions.Web -->
<PackageReference Include="AwesomeAssertions" Version="9.1.0" />
<PackageReference Include="AwesomeAssertions.Web" Version="1.9.6" />
```text

---

## Best Practices

### Should Do ✅

1. **Separate Test Projects**: Integration test projects should be separate from unit tests
2. **Test Data Isolation**: Each test case has independent data preparation and cleanup
3. **Use Base Classes**: Shared configuration and helper methods in base classes
4. **Explicit Naming**: Use three-part naming (Method_Scenario_Expected)
5. **Appropriate Test Scope**: Focus on integration points, don't over-test

### Should Avoid ❌

1. **Mixed Test Types**: Don't put unit tests and integration tests in the same project
2. **Test Dependencies**: Each test should be independent, not dependent on other tests' execution order
3. **Over-mocking**: Integration tests should use real components as much as possible
4. **Ignoring Cleanup**: Clean up test data after tests complete
5. **Hard-coded Data**: Use factory methods or Builder pattern to create test data

---

## Related Skills

- `unit-test-fundamentals` - Unit testing fundamentals
- `nsubstitute-mocking` - Mocking with NSubstitute
- `awesome-assertions-guide` - AwesomeAssertions fluent assertions
- `testcontainers-database` - Containerized database testing with Testcontainers

---

## Reference Resources

### Original Articles

This skill content is distilled from the "Old School Software Engineer's Testing Practice - 30 Day Challenge" article series:

- **Day 19 - Integration Testing Introduction: Basic Architecture and Application Scenarios**
  - Article: https://ithelp.ithome.com.tw/articles/10376335
  - Sample code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day19

### Official Documentation

- [ASP.NET Core Integration Testing Documentation](https://docs.microsoft.com/aspnet/core/test/integration-tests)
- [WebApplicationFactory Usage Guide](https://docs.microsoft.com/aspnet/core/test/integration-tests#basic-tests-with-the-default-webapplicationfactory)
- [AwesomeAssertions.Web GitHub](https://github.com/AwesomeAssertions/AwesomeAssertions.Web)
- [AwesomeAssertions.Web NuGet](https://www.nuget.org/packages/AwesomeAssertions.Web)
````
