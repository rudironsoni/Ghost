---
name: dotnet-testing
description: >
  .NET testing fundamentals overview and navigation center. Triggered when users
  ask about "how to write .NET tests", ".NET testing getting started", "required
  testing tools", "testing best practices", "learn testing from scratch", and
  other general testing needs. Recommends appropriate sub-skills based on
  specific requirements, covering testing fundamentals, test data, assertions,
  mocking, special scenarios, and other 19 fundamental skills.

  Keywords: dotnet testing, .NET testing, testing getting started, how to write
  tests, testing best practices, unit test, unit test, xunit, 3A pattern, FIRST
  principle, assertion, assertion, mock, stub, NSubstitute, test data,
  AutoFixture, Bogus, validator, FluentValidation, TimeProvider, IFileSystem,
  code coverage, ITestOutputHelper, test naming
---
Source: kevintsengtw/dotnet-testing-agent-skills (MIT). Ported into dotnet-agent-harness.

# .NET Testing Fundamentals Overview

---

## 🤖 AI Agent Important Notes

**When you (AI Agent) are loaded with this entry skill, please read the following guidelines first**:

### 📋 Positioning of This Skill

This file is a "navigation center" used to help find the correct **sub-skills**.

#### Your Tasks Are

1. ✅ Match corresponding sub-skills based on user needs
2. ✅ Use the `Skill` tool to load specific sub-skills
3. ✅ Let sub-skills provide professional testing guidance

#### Prohibited Behaviors

- ❌ Do not provide test code directly in this entry skill
- ❌ Do not start implementing tests without loading sub-skills
- ❌ Do not skip sub-skills to provide "general" testing advice

---

## 🎯 Quick Skill Reference Table (AI Agent Must Read)

### Keywords mentioned by users → Sub-skills to load

### Most Frequently Used Skills (Must Memorize)

| User Says...                                   | Load Command                                            | Purpose                        |
| ---------------------------------------------- | ------------------------------------------------------- | ------------------------------ |
| **Validator**, validator, CreateUserValidator  | `/skill dotnet-testing-fluentvalidation-testing`        | FluentValidation testing       |
| **Mock**, simulation, IRepository, IService    | `/skill dotnet-testing-nsubstitute-mocking`             | Mock external dependencies     |
| **AutoFixture**, test data generation          | `/skill dotnet-testing-autofixture-basics`              | Auto-generate test data        |
| **Assertion**, Should(), BeEquivalentTo        | `/skill dotnet-testing-awesome-assertions-guide`        | Fluent assertions (must learn) |
| **DateTime**, time testing, TimeProvider       | `/skill dotnet-testing-datetime-testing-timeprovider`   | Time-related testing           |
| **File**, file system, IFileSystem             | `/skill dotnet-testing-filesystem-testing-abstractions` | File system testing            |
| **Bogus**, fake data, Faker                    | `/skill dotnet-testing-bogus-fake-data`                 | Realistic data generation      |
| **Builder Pattern**, WithXxx                   | `/skill dotnet-testing-test-data-builder-pattern`       | Test Data Builder              |
| **Deep comparison**, DTO comparison, Excluding | `/skill dotnet-testing-complex-object-comparison`       | Complex object comparison      |

### Basic Entry Skills

| User Says...                                       | Load Command                                    | Purpose                |
| -------------------------------------------------- | ----------------------------------------------- | ---------------------- |
| **From scratch**, testing basics, FIRST principles | `/skill dotnet-testing-unit-test-fundamentals`  | Unit test fundamentals |
| **Test naming**, how to name tests                 | `/skill dotnet-testing-test-naming-conventions` | Naming conventions     |
| **Create test project**, xUnit setup               | `/skill dotnet-testing-xunit-project-setup`     | Project setup          |

### Advanced Skill Combinations

| User Says...                   | Load Command                                                | Purpose                     |
| ------------------------------ | ----------------------------------------------------------- | --------------------------- |
| AutoFixture + Bogus            | `/skill dotnet-testing-autofixture-bogus-integration`       | Automation + realistic data |
| AutoFixture + NSubstitute      | `/skill dotnet-testing-autofixture-nsubstitute-integration` | Auto-create Mock            |
| AutoData, Theory testing       | `/skill dotnet-testing-autodata-xunit-integration`          | Parameterized testing       |
| Test output, ITestOutputHelper | `/skill dotnet-testing-test-output-logging`                 | Test logging                |
| Coverage, Coverlet             | `/skill dotnet-testing-code-coverage-analysis`              | Code coverage               |

---

## ⚠️ Usage Flow Examples

### ✅ Correct Flow

```text
User: Please help me create tests for CreateUserValidator

AI: I notice you need to test a Validator. According to the quick reference table,
    I should load the dotnet-testing-fluentvalidation-testing skill.

    [Use Skill tool to load sub-skill]

AI: Now following the FluentValidation Testing skill guidelines to create tests for you...
```

### ❌ Incorrect Flow

```text
User: Please help me create tests for CreateUserValidator

AI: Okay, let me write the tests... (start writing code directly without loading sub-skills)
```

---

## 📚 Complete Skill List

To view the complete list of 19 fundamental skills, detailed decision trees, and learning path recommendations, please
continue reading the subsequent content of this file.

**Human Developer Reference**: For quick lookup, please check [SKILLS_QUICK_INDEX.md](/SKILLS_QUICK_INDEX.md)

---

## Applicable Scenarios

When you encounter the following situations, I will help you find the correct skills:

- Just starting to learn .NET testing, don't know where to begin
- Want to create tests for existing projects, need complete guidance
- Need to improve test quality, looking for best practices
- Encounter specific testing scenarios, unsure which tool to use
- Want to learn about test data generation, assertions, mocking, and other technologies
- Hope to improve test readability and maintainability
- Need to handle special testing scenarios like time, file systems, etc.

## Quick Decision Tree

### Where Should I Start?

#### Scenario 1: Complete Beginner, Never Written Tests Before

**Recommended Learning Path**:

1. `dotnet-testing-unit-test-fundamentals` - Understand FIRST principles and 3A Pattern
2. `dotnet-testing-test-naming-conventions` - Learn naming conventions
3. `dotnet-testing-xunit-project-setup` - Create your first test project

**Why This Path**:

- FIRST principles are the foundation of all tests, establish correct concepts first
- Naming conventions make tests readable and maintainable
- Hands-on project creation transforms theory into practice

---

#### Scenario 2: Can Write Basic Tests, But Test Data Preparation Is Troublesome

**Recommended Skills (Choose One or Combine)**:

### Option A - Automation First

→ `dotnet-testing-autofixture-basics` Suitable: Need large amounts of test data, reduce boilerplate code

### Option B - Realistic Data First

→ `dotnet-testing-bogus-fake-data` Suitable: Need realistic test data (names, addresses, emails, etc.)

### Option C - Semantic Clarity First

→ `dotnet-testing-test-data-builder-pattern` Suitable: Need high readability, clear expression of test intent

### Option D - Best of Both

→ `dotnet-testing-autofixture-basics` + `dotnet-testing-autofixture-bogus-integration` Suitable: Need both automation
and realistic data

---

#### Scenario 3: Have External Dependencies (Database, API, Third-party Services) Need to Mock

**Recommended Skill Combination**:

1. `dotnet-testing-nsubstitute-mocking` - NSubstitute Mock framework fundamentals
2. `dotnet-testing-autofixture-nsubstitute-integration` - (Optional) Integrate AutoFixture with NSubstitute

**When Is the Second Skill Needed**:

- If you are already using AutoFixture to generate test data
- Want to automatically create Mock objects, reduce manual configuration

---

#### Scenario 4: Special Scenarios in Testing

### Time-related Testing

→ `dotnet-testing-datetime-testing-timeprovider` Handles: DateTime.Now, timezone conversion, time calculations

### File System Testing

→ `dotnet-testing-filesystem-testing-abstractions` Handles: File read/write, directory operations, path handling

### Private/Internal Member Testing

→ `dotnet-testing-private-internal-testing` Handles: Need to test private, internal members (but use cautiously)

---

#### Scenario 5: Need Better Assertion Methods

### Basic Needs - Fluent Assertions

→ `dotnet-testing-awesome-assertions-guide` Should be used by all projects to improve test readability

### Advanced Needs - Complex Object Comparison

→ `dotnet-testing-complex-object-comparison` Handles: Deep object comparison, DTO validation, Entity comparison

### Validation Rule Testing

→ `dotnet-testing-fluentvalidation-testing` Handles: Testing FluentValidation validators

---

#### Scenario 6: Want to Learn About Test Coverage

→ `dotnet-testing-code-coverage-analysis` Learn: Use Coverlet to analyze code coverage, generate reports

## Skill Classification Map

Divide 19 fundamental skills into 7 categories (testing fundamentals, test data generation, test doubles, assertion
validation, special scenarios, test metrics, framework integration), each containing skill reference tables, learning
paths, and code examples.

> 📖 For details, please refer to [references/skill-classification-map.md](references/skill-classification-map.md)

## Common Task Mapping Table

Provide 7 common testing tasks (create project from scratch, service dependency testing, time logic testing, etc.) with
skill combination recommendations, implementation steps, and prompt examples.

> 📖 For details, please refer to [references/task-mapping-table.md](references/task-mapping-table.md)

## Learning Path Recommendations

Plan daily learning schedules for beginner path (1-2 weeks) and advanced path (2-3 weeks), including skills, learning
focuses, and practical exercises.

> 📖 For details, please refer to [references/learning-paths.md](references/learning-paths.md)

## Guided Conversation Examples

Show how AI interacts with you to help choose the correct skills, including 4 common conversation scenarios: beginner
entry, handling dependencies, specific problems, improving tests.

> 📖 For details, please refer to [references/conversation-examples.md](references/conversation-examples.md)

## Relationship with Advanced Skills

After completing fundamental skills, if you need integration testing, API testing, containerized testing, or
microservice testing, please refer to:

**Advanced Integration Testing** → `dotnet-testing-advanced`

- ASP.NET Core integration testing
- Containerized testing (Testcontainers)
- Microservice testing (.NET Aspire)
- Test framework upgrades and migrations

## Related Resources

### Original Data Sources

- **iThome Ironman Competition Series**:
  [Old School Software Engineer's Testing Practice - 30 Day Challenge](https://ithelp.ithome.com.tw/users/20066083/ironman/8276)
  🏆 2025 iThome Ironman Competition Software Development Group Champion

- **Complete Sample Code**: [30Days_in_Testing_Samples](https://github.com/kevintsengtw/30Days_in_Testing_Samples)
  Contains executable code for all sample projects

### Learning Documents

This skill set is refined from the following complete materials:

- **Agent Skills: From Architecture Design to Practical Application** (docs/Agent_Skills_Mastery.pdf) Complete coverage
  of Agent Skills from theory to practice

- **Claude Code Skills: Making AI a Professional Craftsman** (docs/Agent_Skills_Architecture.pdf) In-depth analysis of
  Agent Skills architecture design

- **.NET Testing: Write Better, Run Faster** (docs/NET_Testing_Write_Better_Run_Faster.pdf) Test execution optimization
  and debugging techniques

## Next Steps

Choose the skill that matches your needs to start learning, or tell me your specific situation and I will recommend the
most suitable learning path!

**Quick Start**:

- Beginner → Start with `dotnet-testing-unit-test-fundamentals`
- Experienced → Tell me the specific problems you encounter
- Unsure → Tell me your project situation and I will help you analyze
