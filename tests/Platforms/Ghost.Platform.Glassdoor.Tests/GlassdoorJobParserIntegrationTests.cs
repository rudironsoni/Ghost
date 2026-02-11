using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor.Internal;
using Xunit;

namespace Ghost.Platform.Glassdoor.Tests;

/// <summary>
/// Integration tests for GlassdoorJobParser covering mock JSON responses and parser resilience.
/// </summary>
public class GlassdoorJobParserIntegrationTests
{
    [Fact]
    public void ParseSearchResponse_ExtractsJobs_WhenJsonIsValid()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": 100000,
                                        "p90": 150000,
                                        "payCurrency": "USD"
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var job = result.First();
        job.Title.Should().Be("Software Engineer");
        job.Company.Should().Be("Tech Company");
        job.Location.Should().Be("San Francisco, CA");
        job.Salary.Should().Be("100000 - 150000 USD");
        job.Source.Should().Be("Glassdoor");
    }

    [Fact]
    public void ParseSearchResponse_ExtractsMultipleJobs_WhenJsonContainsMultipleJobs()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-1",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Company A",
                                "location": "San Francisco, CA"
                            },
                            {
                                "jobId": "job-2",
                                "jobTitleText": "Data Scientist",
                                "employerNameFromSearch": "Company B",
                                "location": "New York, NY"
                            },
                            {
                                "jobId": "job-3",
                                "jobTitleText": "Product Manager",
                                "employerNameFromSearch": "Company C",
                                "location": "Austin, TX"
                            }
                        ],
                        "totalResults": 3
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(j => j.Title == "Software Engineer");
        result.Should().Contain(j => j.Title == "Data Scientist");
        result.Should().Contain(j => j.Title == "Product Manager");
    }

    [Fact]
    public void ParseSearchResponse_HandlesEmptyJson()
    {
        // Arrange
        var json = "";

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResponse_HandlesNullJson()
    {
        // Arrange
        string? json = null;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResponse_HandlesInvalidJson()
    {
        // Arrange
        var json = "invalid json";

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResponse_HandlesMissingOptionalFields()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var job = result.First();
        job.Title.Should().Be("Software Engineer");
        job.Company.Should().Be("Tech Company");
        job.Location.Should().BeNull();
        job.Salary.Should().BeNull();
    }

    [Fact]
    public void ParseSearchResponse_HandlesMissingTitle()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResponse_HandlesSalaryWithOnlyMinValue()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": 100000,
                                        "payCurrency": "USD"
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Salary.Should().Be("100000 USD");
    }

    [Fact]
    public void ParseSearchResponse_HandlesSalaryWithOnlyMaxValue()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p90": 150000,
                                        "payCurrency": "USD"
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Salary.Should().Be("150000 USD");
    }

    [Fact]
    public void ParseSearchResponse_HandlesSalaryWithoutCurrency()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": 100000,
                                        "p90": 150000
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Salary.Should().Be("100000 - 150000");
    }

    [Fact]
    public void ParseSearchResponse_HandlesNestedJobStructure()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": 100000,
                                        "p90": 150000,
                                        "payCurrency": "USD"
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var job = result.First();
        job.Title.Should().Be("Software Engineer");
        job.Company.Should().Be("Tech Company");
        job.Location.Should().Be("San Francisco, CA");
        job.Salary.Should().Be("100000 - 150000 USD");
    }

    [Fact]
    public void ParseSearchResponse_HandlesAlternativeEmployerNameField()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerName": "Tech Company",
                                "location": "San Francisco, CA"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Company.Should().Be("Tech Company");
    }

    [Fact]
    public void ParseSearchResponse_HandlesAlternativeLocationField()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "jobLocationCity": "San Francisco"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Location.Should().Be("San Francisco");
    }

    [Fact]
    public void ParseSearchResponse_HandlesEmptyJobsArray()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [],
                        "totalResults": 0
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResponse_HandlesJobsInDifferentStructure()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobs": [
                        {
                            "jobId": "job-123",
                            "jobTitleText": "Software Engineer",
                            "employerNameFromSearch": "Tech Company",
                            "location": "San Francisco, CA"
                        }
                    ]
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Software Engineer");
    }

    [Fact]
    public void ParseSearchResponse_HandlesMalformedJobObject()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA"
                            },
                            {
                                "jobId": "job-456",
                                "jobTitleText": "Data Scientist",
                                "employerNameFromSearch": "Data Corp",
                                "location": "New York, NY"
                            }
                        ],
                        "totalResults": 2
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Data Scientist");
    }

    [Fact]
    public void ParseSearchResponse_HandlesSalaryAsString()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": "100000",
                                        "p90": "150000",
                                        "payCurrency": "USD"
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Salary.Should().Be("100000 - 150000 USD");
    }

    [Fact]
    public void ParseSearchResponse_HandlesDeeplyNestedStructure()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "results": {
                            "jobs": [
                                {
                                    "jobId": "job-123",
                                    "jobTitleText": "Software Engineer",
                                    "employerNameFromSearch": "Tech Company",
                                    "location": "San Francisco, CA"
                                }
                            ]
                        }
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Software Engineer");
    }

    [Fact]
    public void ParseSearchResponse_HandlesMixedJobStructures()
    {
        // Arrange
        var json = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-1",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Company A",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": 100000,
                                        "p90": 150000,
                                        "payCurrency": "USD"
                                    }
                                }
                            },
                            {
                                "jobId": "job-2",
                                "jobTitleText": "Data Scientist",
                                "employerName": "Company B",
                                "jobLocationCity": "New York"
                            },
                            {
                                "jobId": "job-3",
                                "jobTitleText": "Product Manager",
                                "employerNameFromSearch": "Company C"
                            }
                        ],
                        "totalResults": 3
                    }
                }
            }
            """;

        // Act
        var result = GlassdoorJobParser.ParseSearchResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(j => j.Title == "Software Engineer" && j.Salary != null);
        result.Should().Contain(j => j.Title == "Data Scientist" && j.Location == "New York");
        result.Should().Contain(j => j.Title == "Product Manager" && j.Location == null);
    }
}
