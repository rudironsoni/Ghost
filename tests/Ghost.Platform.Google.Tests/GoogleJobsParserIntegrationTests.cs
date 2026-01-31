using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Google.Tests;

/// <summary>
/// Integration tests for GoogleJobsParser covering mock HTML responses and parser resilience.
/// </summary>
public class GoogleJobsParserIntegrationTests
{
    private readonly ILogger _logger;

    public GoogleJobsParserIntegrationTests()
    {
        _logger = Substitute.For<ILogger>();
    }

    [Fact]
    public void ParseFromHtml_ExtractsJobsFromJsonLd()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    },
                    "baseSalary": {
                        "@type": "MonetaryAmount",
                        "value": {
                            "@type": "MonetaryAmount",
                            "minValue": 100000,
                            "maxValue": 150000,
                            "currency": "USD"
                        }
                    },
                    "employmentType": "FULL_TIME",
                    "datePosted": "2024-01-15"
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var job = result.First();
        job.Title.Should().Be("Software Engineer");
        job.Company.Should().Be("Tech Company");
        job.Location.Should().Be("San Francisco");
        job.Salary.Should().Be("USD 100000-150000");
        job.JobType.Should().Be(JobType.FullTime);
        job.Source.Should().Be("Google");
    }

    [Fact]
    public void ParseFromHtml_ExtractsMultipleJobsFromJsonLd()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Company A"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "Job 1",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-1"
                    }
                }
                </script>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Data Scientist",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Company B"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "New York"
                        }
                    },
                    "description": "Job 2",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-2"
                    }
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(j => j.Title == "Software Engineer");
        result.Should().Contain(j => j.Title == "Data Scientist");
    }

    [Fact]
    public void ParseFromHtml_HandlesConsentPage()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Before you continue to Google Search</title>
            </head>
            <body>
                <h1>Consent Required</h1>
                <p>Please accept our cookies</p>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFromHtml_HandlesEmptyHtml()
    {
        // Arrange
        var html = "";

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFromHtml_HandlesNullHtml()
    {
        // Arrange
        string? html = null;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html!, _loggerMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFromHtml_HandlesMalformedJsonLd()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFromHtml_ExtractsJobsFromNestedArray()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/json">
                [
                    [
                        "Software Engineer",
                        "Tech Company",
                        "San Francisco",
                        "Full-time",
                        "100000-150000",
                        "2024-01-15",
                        "A great job opportunity",
                        "job-123"
                    ],
                    [
                        "Data Scientist",
                        "Data Corp",
                        "New York",
                        "Full-time",
                        "120000-180000",
                        "2024-01-16",
                        "Data analysis role",
                        "job-456"
                    ]
                ]
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void ParseFromHtml_HandlesMissingOptionalFields()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    }
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var job = result.First();
        job.Title.Should().Be("Software Engineer");
        job.Company.Should().Be("Tech Company");
        job.Location.Should().Be("San Francisco");
        job.Description.Should().BeNull();
        job.Salary.Should().BeNull();
        job.JobType.Should().Be(JobType.Unknown);
    }

    [Fact]
    public void ParseFromHtml_ParsesDifferentJobTypes()
    {
        // Arrange
        var jobTypes = new[]
        {
            ("FULL_TIME", JobType.FullTime),
            ("PART_TIME", JobType.PartTime),
            ("CONTRACTOR", JobType.Contract),
            ("INTERN", JobType.Internship)
        };

        foreach (var (employmentType, expectedJobType) in jobTypes)
        {
            var html = $$"""
                <!DOCTYPE html>
                <html>
                <head></head>
                <body>
                    <script type="application/ld+json">
                    {
                        "@context": "https://schema.org",
                        "@type": "JobPosting",
                        "title": "Test Job",
                        "hiringOrganization": {
                            "@type": "Organization",
                            "name": "Test Company"
                        },
                        "jobLocation": {
                            "@type": "Place",
                            "address": {
                                "@type": "PostalAddress",
                                "addressLocality": "Test Location"
                            }
                        },
                        "description": "Test",
                        "identifier": {
                            "@type": "PropertyValue",
                            "value": "job-test"
                        },
                        "employmentType": "{{employmentType}}"
                    }
                    </script>
                </body>
                </html>
                """;

            // Act
            var result = GoogleJobsParser.ParseFromHtml(html, _loggerMock.Object);

            // Assert
            result.Should().HaveCount(1);
            result.First().JobType.Should().Be(expectedJobType, $"Should parse {employmentType} correctly");
        }
    }

    [Fact]
    public void ParseFromHtml_HandlesJsonLdArray()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                [
                    {
                        "@context": "https://schema.org",
                        "@type": "JobPosting",
                        "title": "Software Engineer",
                        "hiringOrganization": {
                            "@type": "Organization",
                            "name": "Company A"
                        },
                        "jobLocation": {
                            "@type": "Place",
                            "address": {
                                "@type": "PostalAddress",
                                "addressLocality": "San Francisco"
                            }
                        },
                        "description": "Job 1",
                        "identifier": {
                            "@type": "PropertyValue",
                            "value": "job-1"
                        }
                    },
                    {
                        "@context": "https://schema.org",
                        "@type": "JobPosting",
                        "title": "Data Scientist",
                        "hiringOrganization": {
                            "@type": "Organization",
                            "name": "Company B"
                        },
                        "jobLocation": {
                            "@type": "Place",
                            "address": {
                                "@type": "PostalAddress",
                                "addressLocality": "New York"
                            }
                        },
                        "description": "Job 2",
                        "identifier": {
                            "@type": "PropertyValue",
                            "value": "job-2"
                        }
                    }
                ]
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ParseFromHtml_HandlesSalaryAsString()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    },
                    "baseSalary": "$100,000 - $150,000"
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Salary.Should().Be("$100,000 - $150,000");
    }

    [Fact]
    public void ParseFromHtml_HandlesInvalidDatePosted()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    },
                    "datePosted": "invalid-date"
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().PostedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ParseFromHtml_HandlesMissingTitle()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    }
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFromHtml_HandlesMultipleScriptTags()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/json">
                {
                    "data": "not a job"
                }
                </script>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    }
                }
                </script>
                <script type="application/json">
                {
                    "other": "data"
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Software Engineer");
    }

    [Fact]
    public void ParseFromHtml_HandlesWrappedJson()
    {
        // Arrange
        var html = """
            )]}'
            [
                [
                    "Software Engineer",
                    "Tech Company",
                    "San Francisco",
                    "Full-time",
                    "100000-150000",
                    "2024-01-15",
                    "A great job opportunity",
                    "job-123"
                ]
            ]
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void ParseFromHtml_HandlesNonJobPostingJsonLd()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "Organization",
                    "name": "Tech Company"
                }
                </script>
            </body>
            </html>
            """;

        // Act
        var result = GoogleJobsParser.ParseFromHtml(html, _logger);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
