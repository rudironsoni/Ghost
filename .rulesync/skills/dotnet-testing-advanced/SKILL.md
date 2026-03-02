---
name: dotnet-testing-advanced
description: |
  .NET advanced testing skills overview and navigation center. Triggered when users ask about "integration testing", "API testing", "containerized testing", "microservice testing", "test framework migration", "Testcontainers", "Aspire testing", and other advanced testing needs. Recommends appropriate sub-skills based on specific requirements, covering integration testing, Testcontainers, Aspire testing, framework upgrades, and other 8 advanced skills.
  Keywords: integration testing, integration testing, API testing, advanced testing, advanced testing, testcontainers, aspire testing, WebApplicationFactory, TestServer, database test, database testing, EF Core test, MongoDB test, Redis test, Docker test, container testing, microservice test, microservice testing, .NET Aspire, xUnit upgrade, TUnit, framework migration
targets: ['*']
license: MIT
metadata:
  author: Kevin Tseng
  version: '1.0.0'
  tags: '.NET, testing, integration, advanced, testcontainers, aspire'
  related_skills: 'dotnet-testing'
  skill_count: 8
  skill_type: 'overview'
---

Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# .NET Advanced Testing Skills Overview

---

## 🤖 AI Agent Important Notes

**When you (AI Agent) are loaded with this advanced entry skill, please read the following guidelines first**:

### 📋 Positioning of This Skill

This file is an "advanced testing navigation center" used to help find the correct **advanced sub-skills**.

#### Your Tasks Are

1. ✅ Match corresponding advanced sub-skills based on user needs
2. ✅ Use the `Skill` tool to load specific sub-skills
3. ✅ Let sub-skills provide professional integration testing guidance

#### Prohibited Behaviors

- ❌ Do not provide integration test code directly in this entry skill
- ❌ Do not start implementing tests without loading sub-skills
- ❌ Do not skip sub-skills to provide "general" integration testing advice

---

## 🎯 Quick Skill Reference Table (AI Agent Must Read)

### Keywords mentioned by users → Advanced sub-skills to load

### Integration Testing Skills

| User Says...                                            | Load Command                                                | Purpose                       |
| ------------------------------------------------------- | ----------------------------------------------------------- | ----------------------------- |
| **API testing**, Controller testing, endpoint testing   | `/skill dotnet-testing-advanced-aspnet-integration-testing` | Basic API integration testing |
| **Full CRUD**, WebAPI testing, business process testing | `/skill dotnet-testing-advanced-webapi-integration-testing` | Complete API flow testing     |
| **WebApplicationFactory**, TestServer                   | `/skill dotnet-testing-advanced-aspnet-integration-testing` | WebApplicationFactory usage   |

### Containerized Testing Skills

| User Says...                                          | Load Command                                             | Purpose                               |
| ----------------------------------------------------- | -------------------------------------------------------- | ------------------------------------- |
| **SQL Server container**, PostgreSQL, MySQL           | `/skill dotnet-testing-advanced-testcontainers-database` | Relational database container testing |
| **MongoDB**, Redis, Elasticsearch                     | `/skill dotnet-testing-advanced-testcontainers-nosql`    | NoSQL database container testing      |
| **Real database**, EF Core testing, Dapper testing    | `/skill dotnet-testing-advanced-testcontainers-database` | Real database behavior testing        |
| **Testcontainers**, container testing, Docker testing | `/skill dotnet-testing-advanced-testcontainers-database` | Testcontainers fundamentals           |

### Microservice Testing Skills

| User Says...                                               | Load Command                                    | Purpose                     |
| ---------------------------------------------------------- | ----------------------------------------------- | --------------------------- |
| **.NET Aspire**, microservice testing, distributed testing | `/skill dotnet-testing-advanced-aspire-testing` | Aspire microservice testing |
| **DistributedApplication**, inter-service communication    | `/skill dotnet-testing-advanced-aspire-testing` | Aspire application testing  |

### Framework Upgrade Skills

| User Says...                                      | Load Command                                         | Purpose                          |
| ------------------------------------------------- | ---------------------------------------------------- | -------------------------------- |
| **xUnit upgrade**, xUnit 3.x, version upgrade     | `/skill dotnet-testing-advanced-xunit-upgrade-guide` | xUnit 2.x → 3.x upgrade          |
| **TUnit**, new test framework, TUnit fundamentals | `/skill dotnet-testing-advanced-tunit-fundamentals`  | TUnit fundamentals and migration |
| **TUnit advanced**, TUnit DI, parallel execution  | `/skill dotnet-testing-advanced-tunit-advanced`      | TUnit advanced features          |

---

## ⚠️ Usage Flow Examples

### ✅ Correct Flow

````text
User: Please help me create API integration tests for ProductsController

AI: I notice you need API integration testing. According to the quick reference table,
    I should load the dotnet-testing-advanced-aspnet-integration-testing skill.

    [Use Skill tool to load sub-skill]

AI: Now following the ASP.NET Core Integration Testing skill guidelines to create tests for you...
```text

### ❌ Incorrect Flow

```text
User: Please help me create API integration tests for ProductsController

AI: Okay, let me write the integration tests... (start writing code directly without loading sub-skills)
```text

---

## 📚 Complete Skill List

To view the complete list of 8 advanced skills, detailed decision trees, and learning path recommendations, please continue reading the subsequent content of this file.

**Human Developer Reference**: For quick lookup, please check [SKILLS_QUICK_INDEX.md](/SKILLS_QUICK_INDEX.md)

---

## Applicable Scenarios

When you need the following advanced testing capabilities, I will help you find the correct skills:

- Test complete Web API endpoints and HTTP flows
- Use real databases in tests (containerized)
- Test NoSQL databases (MongoDB, Redis, etc.)
- Test microservice architectures and distributed systems
- Upgrade test framework versions (xUnit 2.x → 3.x)
- Migrate to new test frameworks (TUnit)
- Create end-to-end integration tests

## Quick Decision Tree

Quickly find corresponding advanced sub-skills based on testing scenarios (API testing, real database, microservices, framework migration). Covers 4 major scenarios and multiple option branches.

> 📖 For details, please refer to [references/decision-tree.md](references/decision-tree.md)

---

## Skill Classification Map

8 advanced skills divided into three categories: Integration Testing (4), Microservice Testing (1), Framework Migration (3), including each skill's core value, suitable scenarios, learning difficulty, and prerequisite skills.

> 📖 For details, please refer to [references/skill-classification-map.md](references/skill-classification-map.md)

---

## Common Task Mapping Table

Complete mapping of 7 common tasks, including scenario descriptions, recommended skills, implementation steps, prompt examples, and expected code structures.

> 📖 For details, please refer to [references/task-mapping-table.md](references/task-mapping-table.md)

---

## Integration Testing Level Mapping

Choose appropriate testing strategies based on project complexity:

### Level 1: Simple WebApi Project

**Project Characteristics**:
- Simple CRUD API
- No external dependencies or use in-memory implementations
- Simple business logic

**Recommended Skills**:
- `dotnet-testing-advanced-aspnet-integration-testing`

**Testing Focus**:
- Route validation
- Model binding
- HTTP responses
- Basic business logic

**Sample Projects**:
- TodoList API
- Simple product catalog

---

### Level 2: WebApi Project with Service Dependencies

**Project Characteristics**:
- Has business logic layer (Services)
- Depends on external services (can be Mocked)
- Medium complexity

**Recommended Skill Combination**:
1. `dotnet-testing-advanced-aspnet-integration-testing` (fundamentals)
2. `dotnet-testing-nsubstitute-mocking` (mocking dependencies)

**Testing Strategy**:
- Use NSubstitute to create Service stubs
- Test Controller and Service interactions
- Validate error handling

**Sample Projects**:
- E-commerce API (with inventory, order services)
- CMS system

---

### Level 3: Complete WebApi Project

**Project Characteristics**:
- Complex business logic
- Needs real database
- May have external API integrations
- Complete error handling

**Recommended Skill Combination**:
1. `dotnet-testing-advanced-webapi-integration-testing` (complete flow)
2. `dotnet-testing-advanced-testcontainers-database` (real database)
3. `dotnet-testing-advanced-testcontainers-nosql` (if using NoSQL)

**Testing Strategy**:
- Use Testcontainers to create real databases
- Complete end-to-end testing
- Test data preparation and cleanup
- Validate all error scenarios

**Sample Projects**:
- Large e-commerce platform
- Enterprise management system
- SaaS application

---

## Learning Path Recommendations

Contains complete learning plans for integration testing entry (1 week), microservice testing specialization (3-5 days), and framework migration paths, including daily learning focuses.

> 📖 For details, please refer to [references/learning-paths.md](references/learning-paths.md)

---

## Skill Combination Recommendations

Based on different project needs, the following skill combinations are recommended:

### Combination 1: Complete API Testing Project

**Suitable**: Creating complete test suites for production projects

**Skill Combination**:
1. `dotnet-testing-advanced-aspnet-integration-testing` (fundamentals)
2. `dotnet-testing-advanced-testcontainers-database` (real database)
3. `dotnet-testing-advanced-webapi-integration-testing` (complete flow)

**Learning Order**:
1. Learn aspnet-integration-testing for fundamentals first
2. Then learn testcontainers-database to master database testing
3. Finally learn webapi-integration-testing for integrated application

**Expected Results**:
- Can create complete tests for Web API projects
- Use real databases to validate behavior
- Test all CRUD endpoints and error handling

---

### Combination 2: Microservice Testing Solution

**Suitable**: Microservice architectures, distributed systems

**Skill Combination**:
1. `dotnet-testing-advanced-aspire-testing` (core)
2. `dotnet-testing-advanced-testcontainers-database` (database)
3. `dotnet-testing-advanced-testcontainers-nosql` (NoSQL)

**Learning Order**:
1. Learn testcontainers first (database testing fundamentals)
2. Then learn aspire-testing (microservice testing)

**Expected Results**:
- Test .NET Aspire projects
- Validate inter-service communication
- Use containerized environments for testing

---

### Combination 3: Framework Modernization

**Suitable**: Test framework upgrades or migrations

#### Option A: xUnit Upgrade
**Skills**:
- `dotnet-testing-advanced-xunit-upgrade-guide`

**Suitable**:
- Existing projects using xUnit 2.x
- Want to upgrade to latest version

---

#### Option B: TUnit Migration
**Skill Combination**:
1. `dotnet-testing-advanced-tunit-fundamentals` (fundamentals)
2. `dotnet-testing-advanced-tunit-advanced` (advanced)

**Suitable**:
- New projects choosing test frameworks
- Considering migration from xUnit

**Learning Order**:
1. Learn fundamentals first to understand basics
2. Then learn advanced to master advanced features

---

## Prerequisite Skills Requirements

Before learning advanced skills, it is recommended to master the following fundamental skills (from `dotnet-testing` fundamental skill set):

### Required Skills

#### 1. dotnet-testing-unit-test-fundamentals
**Why Required**:
- Integration tests also follow 3A Pattern
- FIRST principles equally apply
- Need to understand testing fundamentals concepts

---

#### 2. dotnet-testing-xunit-project-setup
**Why Required**:
- Need to create test projects
- Understand project structure
- Understand package management

---

#### 3. dotnet-testing-awesome-assertions-guide
**Why Required**:
- Integration tests need to validate HTTP responses
- FluentAssertions.Web provides powerful API assertions
- Improve test readability

---

### Recommended Skills

#### 1. dotnet-testing-nsubstitute-mocking
**Why Recommended**:
- May need to Mock external services in integration tests
- WebApplicationFactory needs to replace services

---

#### 2. dotnet-testing-autofixture-basics
**Why Recommended**:
- Quickly generate test data
- Reduce integration test boilerplate code

---

## Guided Conversation Examples

4 complete conversation examples showing how AI guides selection of correct skills: API testing, microservice testing, framework upgrade, TUnit evaluation.

> 📖 For details, please refer to [references/conversation-examples.md](references/conversation-examples.md)

---

## Relationship with Fundamental Skills

Advanced skills build upon fundamental skills:

**Fundamental Testing Capabilities** → `dotnet-testing` (Fundamental Skill Set)
- Unit test fundamentals
- Test data generation
- Assertions and mocking
- Special scenario handling

### ↓ Advanced Applications

**Advanced Integration Testing** → `dotnet-testing-advanced` (This Skill Set)
- Web API integration testing
- Containerized testing
- Microservice testing
- Framework upgrades

**Learning Recommendation**:
Complete the core skills of `dotnet-testing` fundamental skill set first, then enter this advanced skill set.

---

## Related Resources

### Original Data Sources

- **iThome Ironman Competition Series**: [Old School Software Engineer's Testing Practice - 30 Day Challenge](https://ithelp.ithome.com.tw/users/20066083/ironman/8276)
  🏆 2025 iThome Ironman Competition Software Development Group Champion

- **Complete Sample Code**: [30Days_in_Testing_Samples](https://github.com/kevintsengtw/30Days_in_Testing_Samples)
  Contains executable code for all sample projects

### Technical Requirements

**Integration Testing Skills**:
- .NET 8+
- Docker Desktop
- WSL2 (Windows environment)

**Aspire Testing Skills**:
- .NET 8+
- .NET Aspire Workload
- Docker Desktop

---

## Next Steps

Choose the advanced skill that matches your needs to start learning, or tell me your specific situation and I will recommend the most suitable learning path!

**Quick Start**:
- Want to test APIs → Start with `dotnet-testing-advanced-aspnet-integration-testing`
- Need real database → Start with `dotnet-testing-advanced-testcontainers-database`
- Microservice project → Use `dotnet-testing-advanced-aspire-testing`
- Framework upgrade → Use corresponding upgrade guide
- Unsure → Tell me your project situation and I will help you analyze
````
