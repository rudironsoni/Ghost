# LinkedIn Ghost.Sdk.Spider Migration Tests

This directory contains comprehensive tests for the LinkedIn platform migration to Ghost.Sdk.Spider.

## Files Created

### 1. Migration Implementation
- **Migration/LinkedInJobEntity.cs** (116 lines)
  - Entity class using Ghost.Sdk.Spider attributes
  - Defines extraction rules for all LinkedIn job fields
  - Uses XPath selectors and formatters (Trim, Regex)
  - Includes validation for required fields

- **Migration/LinkedInSpider.cs** (123 lines)
  - Spider implementation extending Ghost.Sdk.Spider.Engine.Spider
  - Configures spider options (domains, patterns, concurrency)
  - Implements full spider pipeline with EntityParser
  - Demonstrates JavaScriptAdapter integration

### 2. Test Suites
- **LinkedInEntityTests.cs** (404 lines, 17 tests)
  - Tests entity extraction with real selectors
  - Validates all field extractions (Title, Company, Location, Description, etc.)
  - Tests formatter functionality (Trim, Regex for applicant count)
  - Tests with both synthetic and real HTML fixtures
  - Validates base properties (Id, SourceUrl, ExtractedAt)
  - Tests entity validation logic

- **LinkedInSpiderTests.cs** (470 lines, 23 tests)
  - Tests full spider pipeline
  - Tests ProcessResponseAsync with various scenarios
  - Tests URL filtering (ShouldFollowUrl)
  - Tests spider lifecycle (OnStart, OnComplete, OnError)
  - Tests with multiple job pages
  - Tests error handling and edge cases
  - Validates JavaScriptAdapter integration

### 3. Test Fixtures
- **Fixtures/test-job.html** (129 lines)
  - Synthetic LinkedIn job page HTML
  - Contains all required fields for testing
  - Includes job details, criteria, and metadata
  - Sample job: "Software Engineer, New Grad" at Stripe

## Test Coverage

### LinkedInEntityTests (17 tests)
1. ✅ Extract all fields from test fixture
2. ✅ Extract URLs (job, company, logo)
3. ✅ Extract description with rich content
4. ✅ Extract applicant count with regex formatter
5. ✅ Set base properties correctly
6. ✅ Pass validation with complete data
7. ✅ Extract from real fixture 1
8. ✅ Extract from real fixture 2
9. ✅ Handle optional fields in real fixture 3
10. ✅ Return null for empty content
11. ✅ Return null for invalid HTML
12. ✅ Trim formatter removes whitespace
13. ✅ Regex formatter extracts numeric values
14. ✅ Validation fails without title
15. ✅ Validation fails without company
16. ✅ Validation passes with required fields
17. ✅ GetMetadata returns entity configuration

### LinkedInSpiderTests (23 tests)
1. ✅ Spider name is "LinkedInJobSpider"
2. ✅ Start URLs contain LinkedIn job URLs
3. ✅ Options include linkedin.com domain
4. ✅ Options exclude admin pages
5. ✅ Options have reasonable defaults
6. ✅ Extract job from valid page
7. ✅ Extract jobs from multiple pages
8. ✅ Ignore invalid job pages
9. ✅ Skip non-HTML responses
10. ✅ Skip failed responses
11. ✅ OnStart clears extracted jobs
12. ✅ OnComplete is callable
13. ✅ OnError handles exceptions
14. ✅ Follow job view URLs
15. ✅ Follow job search URLs
16. ✅ Don't follow non-job URLs
17. ✅ Don't follow admin URLs
18. ✅ Don't follow logout URLs
19. ✅ Don't follow non-LinkedIn domains
20. ✅ Don't follow invalid URLs
21. ✅ Don't follow null URLs
22. ✅ Extract valid jobs from real fixtures
23. ✅ ExtractedJobs is read-only

## Key Features Demonstrated

### Entity Extraction
- XPath selectors for precise element targeting
- CSS selectors for simpler queries
- Attribute extraction (href, data-delayed-url)
- Multiple formatters in sequence (Order property)
- TrimFormatter for whitespace cleanup
- RegexFormatter for pattern extraction
- Field validation with Required attribute

### Spider Pipeline
- JavaScriptAdapter for dynamic content
- EntityParser for automated extraction
- SpiderOptions configuration
- URL filtering and domain restrictions
- Lifecycle hooks (OnStart, OnComplete, OnError)
- Response type validation
- Read-only extracted results

### Test Patterns
- NUnit framework with FluentAssertions
- Async test methods
- Fixture-based testing
- Synthetic and real data
- Edge case coverage
- Formatter validation
- Pipeline integration testing

## Running Tests

```bash
# Run all tests
dotnet test tests/Platforms/Ghost.Platform.LinkedIn.Tests/

# Run only entity tests
dotnet test --filter "FullyQualifiedName~LinkedInEntityTests"

# Run only spider tests
dotnet test --filter "FullyQualifiedName~LinkedInSpiderTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~Parse_WithTestJobFixture_ShouldExtractAllFields"
```

## Migration Benefits

This migration demonstrates:
1. **Declarative approach**: Attributes define extraction rules
2. **Type safety**: Strongly-typed entities
3. **Reusability**: EntityParser handles all extraction
4. **Maintainability**: Selectors defined with entity
5. **Testability**: Easy to test with synthetic fixtures
6. **Flexibility**: Formatters for data transformation
7. **Integration**: Works with JavaScriptAdapter pipeline

## Notes

- Tests use NUnit + FluentAssertions pattern
- Compatible with existing Ghost.Sdk.Spider.Tests patterns
- Real fixtures (linkedin-job-detail-*.html) already exist
- Synthetic fixture (test-job.html) for controlled testing
- All tests designed to pass with proper Ghost.Sdk.Spider setup
