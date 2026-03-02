---
name: dotnet-testing-bogus-fake-data
description: |
  Using Bogus to generate realistic fake data specialized skill. Used when generating realistic names, addresses, phone numbers, emails, company info and other test data. Covers Faker class, multi-language support, custom rules, bulk data generation, etc.
  Keywords: bogus, faker, fake data, fake data, realistic data, realistic data, fake name, fake address, fake email, Faker<T>, RuleFor, Generate, faker.Name, faker.Address, faker.Internet, generate fake data, generate fake data, seed data
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: 'bogus, fake-data, test-data, realistic-data, faker, testing'
  related_skills: 'autofixture-basics, autofixture-bogus-integration, test-data-builder-pattern'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# Bogus Fake Data Generator

## Applicable Scenarios

Bogus is a .NET platform fake data generation library, ported from the famous JavaScript library faker.js. It
specializes in generating realistic fake data such as names, addresses, phone numbers, emails, etc., especially suitable
for test scenarios requiring simulation of real-world data.

### Applicable Scenarios

Use this skill when asked to perform the following tasks:

- Generate realistic test data (names, addresses, company names, etc.)
- Need multi-language or multi-region format test data
- Integration tests or UI prototypes need realistic data
- Performance tests need large amounts of real format data
- Database seed (Seed) needs to initialize development or test environments

### Core Value

- **Realistic Data Generation**: Provides meaningful fake data, like real names, addresses, company names
- **Multi-language Support**: Supports over 40 languages and regional formats (including Traditional Chinese `zh_TW`)
- **Reproducibility**: Through seed control, ensures consistency of test data
- **Rich Data Types**: Built-in multiple DataSets, covering various real-world data types
- **Concise Fluent API**: Intuitive and easy-to-use configuration syntax

---

## Package Installation and Setup

### Install Bogus

````bash
dotnet add package Bogus
```text

### NuGet Package Information

| Package Name | Purpose             | NuGet Link                                         |
| -------- | ---------------- | -------------------------------------------------- |
| `Bogus`  | Fake data generation library | [nuget.org](https://www.nuget.org/packages/Bogus/) |

**GitHub Repository**: [bchavez/Bogus](https://github.com/bchavez/Bogus)

---

## Core Concepts

### Basic Syntax Structure

Bogus's core is the `Faker<T>` class, using `RuleFor` method to define property generation rules:

```csharp
using Bogus;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

// Create Product data Faker
var productFaker = new Faker<Product>()
    .RuleFor(p => p.Id, f => f.IndexFaker)
    .RuleFor(p => p.Name, f => f.Commerce.ProductName())
    .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
    .RuleFor(p => p.Description, f => f.Lorem.Sentence())
    .RuleFor(p => p.CreatedDate, f => f.Date.Past());

// Generate single data
var product = productFaker.Generate();

// Generate multiple data
var products = productFaker.Generate(10);
```text

### Built-in DataSet Overview

Bogus provides rich built-in DataSets, each focusing on specific domain data generation:

#### Personal Info (Person DataSet)

```csharp
var faker = new Faker();

var fullName = faker.Person.FullName;        // Full name
var firstName = faker.Person.FirstName;      // First name
var lastName = faker.Person.LastName;        // Last name
var email = faker.Person.Email;              // Email
var gender = faker.Person.Gender;            // Gender
var dateOfBirth = faker.Person.DateOfBirth;  // Date of birth
```text

#### Address Info (Address DataSet)

```csharp
var fullAddress = faker.Address.FullAddress();    // Full address
var streetAddress = faker.Address.StreetAddress(); // Street address
var city = faker.Address.City();                   // City
var state = faker.Address.State();                 // State/Province
var zipCode = faker.Address.ZipCode();             // Zip code
var country = faker.Address.Country();             // Country
var latitude = faker.Address.Latitude();           // Latitude
var longitude = faker.Address.Longitude();         // Longitude
```text

#### Business Info (Company & Commerce DataSet)

```csharp
var companyName = faker.Company.CompanyName();     // Company name
var catchPhrase = faker.Company.CatchPhrase();     // Tagline
var department = faker.Commerce.Department();      // Department
var productName = faker.Commerce.ProductName();    // Product name
var price = faker.Commerce.Price(1, 1000, 2);      // Price (string format)
var ean13 = faker.Commerce.Ean13();                // EAN-13 barcode
```text

#### Internet Info (Internet DataSet)

```csharp
var url = faker.Internet.Url();               // URL
var domainName = faker.Internet.DomainName(); // Domain name
var ipAddress = faker.Internet.Ip();          // IPv4 address
var ipv6 = faker.Internet.Ipv6();             // IPv6 address
var userName = faker.Internet.UserName();     // Username
var password = <DB_PASSWORD_PLACEHOLDER>;     // Password
var email = faker.Internet.Email();           // Email
```text

#### Finance Info (Finance DataSet)

```csharp
var creditCardNumber = faker.Finance.CreditCardNumber();  // Credit card number
var creditCardCvv = faker.Finance.CreditCardCvv();        // CVV
var account = faker.Finance.Account();                     // Account number
var amount = faker.Finance.Amount(100, 10000, 2);          // Amount
var currency = faker.Finance.Currency();                   // Currency
var iban = faker.Finance.Iban();                          // IBAN
var bic = faker.Finance.Bic();                            // BIC/SWIFT
```text

#### Time Info (Date DataSet)

```csharp
var pastDate = faker.Date.Past();                        // Past date
var futureDate = faker.Date.Future();                    // Future date
var recentDate = faker.Date.Recent();                    // Recent date
var soonDate = faker.Date.Soon();                        // Soon date
var between = faker.Date.Between(start, end);            // Date within range
var weekday = faker.Date.Weekday();                      // Day of week
```text

#### Random Data (Random DataSet)

```csharp
var randomInt = faker.Random.Int(1, 100);                  // Integer
var randomDecimal = faker.Random.Decimal(0, 1000);         // Decimal
var randomBool = faker.Random.Bool();                      // Boolean
var randomGuid = faker.Random.Guid();                      // GUID
var randomEnum = faker.Random.Enum<DayOfWeek>();           // Random enum
var randomElement = faker.Random.ArrayElement(array);      // Array random element
var shuffled = faker.Random.Shuffle(collection);           // Shuffle
```text

#### Text Content (Lorem DataSet)

```csharp
var word = faker.Lorem.Word();             // Word
var words = faker.Lorem.Words(5);          // Multiple words
var sentence = faker.Lorem.Sentence();     // Sentence
var paragraph = faker.Lorem.Paragraph();   // Paragraph
var text = faker.Lorem.Text();             // Text block
```text

---

## Multi-language Support

A major feature of Bogus is supporting multiple languages and cultures, making generated data more consistent with local habits:

```csharp
// Traditional Chinese
var chineseFaker = new Faker<Person>("zh_TW")
    .RuleFor(p => p.Name, f => f.Person.FullName)
    .RuleFor(p => p.Address, f => f.Address.FullAddress());

// Japanese
var japaneseFaker = new Faker<Person>("ja")
    .RuleFor(p => p.Name, f => f.Person.FullName)
    .RuleFor(p => p.Phone, f => f.Phone.PhoneNumber());

// French
var frenchFaker = new Faker<Person>("fr")
    .RuleFor(p => p.Name, f => f.Person.FullName)
    .RuleFor(p => p.Company, f => f.Company.CompanyName());
```text

### Supported Language Codes

| Language         | Code    | Language     | Code    |
| ------------ | ------- | -------- | ------- |
| English (US) | `en_US` | Simplified Chinese | `zh_CN` |
| Traditional Chinese     | `zh_TW` | Japanese     | `ja`    |
| Korean         | `ko`    | French     | `fr`    |
| German         | `de`    | Spanish | `es`    |
| Russian         | `ru`    | Portuguese | `pt_BR` |

---

## Advanced Features

Covers Seed reproducibility control, conditional generation and probability control (`OrNull`, `PickRandomWeighted`), related data and nested objects, complex business logic constraints, and custom DataSet extensions (like Taiwan local data generator).

> Full content please refer to [references/advanced-features.md](references/advanced-features.md)

---

## Bogus vs AutoFixture Comparison

### Design Philosophy Differences

| Item         | AutoFixture               | Bogus                           |
| ------------ | ------------------------- | ------------------------------- |
| **Core Philosophy** | Anonymous Test | Realistic Simulation |
| **Data Quality** | Random fill, focus on test logic    | Meaningful data, simulate real scenarios        |
| **Learning Cost** | Auto-infer, zero configuration          | Explicit definition, need to learn DataSet      |
| **Readability**   | Abstract, reduce data noise      | Concrete, data is meaningful              |

### Applicable Scenario Analysis

| Scenario           | Recommended Tool    | Reason                           |
| -------------- | ----------- | ------------------------------ |
| **Unit Test**   | AutoFixture | Focus on logic validation, don't care about data content |
| **Integration Test**   | Bogus       | Need realistic data for end-to-end testing |
| **UI Prototype**    | Bogus       | Demonstration realistic data               |
| **Performance Test**   | Bogus       | Large amounts of real format data             |
| **Database Seed** | Bogus       | Initialize dev/test environment            |
| **Complex Dependencies** | AutoFixture | Auto-handle circular references and nested objects     |

### Code Comparison

```csharp
// AutoFixture: simple and direct, auto-infer
var fixture = new Fixture();
var user = fixture.Create<User>(); // One line done, but data is meaningless

// Bogus: needs setup, but data is meaningful
var userFaker = new Faker<User>()
    .RuleFor(u => u.Name, f => f.Person.FullName)    // Real name format
    .RuleFor(u => u.Email, f => f.Internet.Email()); // Real email format
var user = userFaker.Generate();
```text

---

## Performance Optimization

### Reuse Faker Instances

```csharp
public class OptimizedDataGenerator
{
    // Pre-compile Faker to improve performance (static field, only initialize once)
    private static readonly Faker<User> _userFaker = new Faker<User>()
        .RuleFor(u => u.Id, f => f.Random.Guid())
        .RuleFor(u => u.Name, f => f.Person.FullName)
        .RuleFor(u => u.Email, f => f.Internet.Email());

    public static List<User> GenerateUsers(int count)
        => _userFaker.Generate(count);
}
```text

### Batch Generation

```csharp
// Batch generation to reduce memory allocation
public static IEnumerable<User> GenerateUsersBatch(int totalCount, int batchSize = 1000)
{
    var generated = 0;
    while (generated < totalCount)
    {
        var currentBatchSize = Math.Min(batchSize, totalCount - generated);
        var batch = _userFaker.Generate(currentBatchSize);

        foreach (var user in batch)
        {
            yield return user;
        }

        generated += currentBatchSize;
    }
}
```text

### Lazy Initialization

```csharp
// Use Lazy to delay initialize complex Fakers
private static readonly Lazy<Faker<ComplexEntity>> _complexFaker =
    new(() => new Faker<ComplexEntity>()
        .RuleFor(e => e.Id, f => f.Random.Guid())
        .RuleFor(e => e.Data, f => GenerateComplexData(f)));

public static ComplexEntity Generate() => _complexFaker.Value.Generate();
```text

---

## Test Implementation Examples

### Email Service Test

```csharp
[Fact]
public void EmailService_SendWelcomeEmail_ShouldFormatCorrectly()
{
    // Arrange - need realistic user data to test email format
    var userFaker = new Faker<User>()
        .RuleFor(u => u.Name, f => f.Person.FullName)
        .RuleFor(u => u.Email, f => f.Internet.Email());

    var user = userFaker.Generate();
    var emailService = new EmailService();

    // Act
    var emailContent = emailService.GenerateWelcomeEmail(user);

    // Assert
    emailContent.Should().Contain(user.Name);
    emailContent.Should().Contain(user.Email);
}
```text

### Database Seed

```csharp
public static class DatabaseSeeder
{
    public static void SeedDatabase(AppDbContext context)
    {
        // Set seed to ensure reproducibility
        Randomizer.Seed = new Random(42);

        var customerFaker = new Faker<Customer>("zh_TW")
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Email, f => f.Internet.Email())
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(c => c.Address, f => f.Address.FullAddress());

        var customers = customerFaker.Generate(100);
        context.Customers.AddRange(customers);
        context.SaveChanges();
    }
}
```text

---

## Best Practices

### Naming and Organization

1. **Faker Naming Convention**: Use `{EntityName}Faker` format naming
2. **Centralized Management**: Put Faker definitions in `TestDataGenerators` or `Fakers` folder
3. **Reuse Static Instances**: Avoid repeatedly creating Faker instances

### Code Organization

```text
MyProject.Tests/
├── Fakers/
│   ├── CustomerFaker.cs
│   ├── OrderFaker.cs
│   └── TaiwanDataSetExtensions.cs
├── Services/
│   └── CustomerServiceTests.cs
└── ...
```text

### Common Pitfalls

1. **Avoid over-configuration**: Only set properties needed for testing
2. **Watch for randomness**: Use seed to ensure test reproducibility
3. **Performance considerations**: Use batch generation for large data

---

## Related Skills

| Skill Name                        | Relationship Description                                     |
| ------------------------------- | -------------------------------------------- |
| `autofixture-basics`            | AutoFixture basic usage, suitable for anonymous data in unit tests |
| `autofixture-bogus-integration` | AutoFixture and Bogus mixed usage strategy            |
| `test-data-builder-pattern`     | Manual Builder Pattern, suitable for simple scenarios           |

---

## Reference Resources

### Original Articles

This skill content is extracted from "Old School Software Engineer's Testing Practice - 30 Day Challenge" series:

- **Day 14 - Using Bogus to Generate Fake Data**
  - Ironman Article: https://ithelp.ithome.com.tw/articles/10375501
  - Sample Code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day14

### Official Documentation

- [Bogus NuGet Package](https://www.nuget.org/packages/Bogus/)
- [Bogus GitHub Repository](https://github.com/bchavez/Bogus)
- [Bogus Official Documentation](https://github.com/bchavez/Bogus#readme)

### Extended Reading

- [Bogus and AutoFixture Applications in Testing | mrkt's Programming Learning Notes](https://www.dotblogs.com.tw/mrkt/2024/09/29/191300)

````
