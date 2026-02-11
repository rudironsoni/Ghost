using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.InfoJobs.Jobs.Internal;
using Xunit;

namespace Ghost.Platform.InfoJobs.Tests;

public class SalaryParsingTests
{
    [Theory]
    [InlineData("30.000 €", 30000, "EUR")]
    [InlineData("45.000 € al año", 45000, "EUR")]
    [InlineData("25.500 € brutos/año", 25500, "EUR")]
    [InlineData("35.000-40.000 €", 37500, "EUR")]
    [InlineData("28.000 - 32.000 €", 30000, "EUR")]
    [InlineData("50.000 €/año", 50000, "EUR")]
    [InlineData("22.000 € anuales", 22000, "EUR")]
    [InlineData("40.000 € brutos", 40000, "EUR")]
    [InlineData("18.000 € netos", 18000, "EUR")]
    [InlineData("60.000", 60000, "EUR")] // Without currency symbol
    [InlineData("70.000 EUR", 70000, "EUR")]
    [InlineData("80.000 euros", 80000, "EUR")]
    public void ParseSpanishSalaryValidFormatsReturnsCorrectAmount(string salaryText, decimal expectedAmount, string expectedCurrency)
    {
        // Act
        var result = InfoJobsConstants.ParseSpanishSalary(salaryText);

        // Assert
        result.Amount.Should().Be(expectedAmount);
        result.Currency.Should().Be(expectedCurrency);
    }

    [Theory]
    [InlineData("", 0, "EUR")]
    [InlineData("No especificado", 0, "EUR")]
    [InlineData("A convenir", 0, "EUR")]
    [InlineData("Salario competitivo", 0, "EUR")]
    [InlineData("Según experiencia", 0, "EUR")]
    public void ParseSpanishSalarySpecialCasesHandlesCorrectly(string salaryText, decimal expectedAmount, string expectedCurrency)
    {
        // Act
        var result = InfoJobsConstants.ParseSpanishSalary(salaryText);

        // Assert
        result.Amount.Should().Be(expectedAmount);
        result.Currency.Should().Be(expectedCurrency);
    }

    [Theory]
    [InlineData("30,000 €")] // Comma separator (English style)
    [InlineData("30.000.000 €")] // Million format
    [InlineData("30K €")]
    [InlineData("30k euros")]
    [InlineData("€30.000")] // Currency symbol first
    public void ParseSpanishSalaryAlternativeFormatsReturnsCorrectAmount(string salaryText)
    {
        // Act
        var result = InfoJobsConstants.ParseSpanishSalary(salaryText);

        // Assert
        result.Amount.Should().BeGreaterThan(0);
        result.Currency.Should().Be("EUR");
    }

    [Theory]
    [InlineData("jornada completa", JobType.FullTime)]
    [InlineData("jornada completa presencial", JobType.FullTime)]
    [InlineData("jornada completa teletrabajo", JobType.FullTime)]
    [InlineData("jornada completa híbrido", JobType.FullTime)]
    [InlineData("jornada parcial", JobType.PartTime)]
    [InlineData("jornada parcial presencial", JobType.PartTime)]
    [InlineData("prácticas", JobType.Internship)]
    [InlineData("prácticas curriculares", JobType.Internship)]
    [InlineData("prácticas extracurriculares", JobType.Internship)]
    [InlineData("beca", JobType.Internship)]
    [InlineData("contrato en prácticas", JobType.Internship)]
    [InlineData("temporal", JobType.Contract)]
    [InlineData("contrato temporal", JobType.Contract)]
    [InlineData("obra o servicio", JobType.Contract)]
    [InlineData("indefinido", JobType.FullTime)]
    [InlineData("contrato indefinido", JobType.FullTime)]
    [InlineData("freelance", JobType.Contract)]
    [InlineData("autónomo", JobType.Contract)]
    public void MapSpanishJobTypeValidTypesReturnsCorrectJobType(string spanishType, JobType expectedType)
    {
        // Act
        var result = InfoJobsConstants.MapSpanishJobType(spanishType);

        // Assert
        result.Should().Be(expectedType);
    }

    [Fact]
    public void GetSpanishSalaryPatternsReturnsCorrectPatterns()
    {
        // Act
        var patterns = InfoJobsConstants.GetSpanishSalaryPatterns();

        // Assert
        patterns.Should().NotBeEmpty();
        patterns.Should().Contain(p => p.Contains('€'));
        patterns.Should().Contain(p => p.Contains("EUR"));
        patterns.Should().Contain(p => p.Contains("euros"));
    }

    [Fact]
    public void GetSpanishJobTypeMappingsReturnsCompleteMapping()
    {
        // Act
        var mappings = InfoJobsConstants.GetSpanishJobTypeMappings();

        // Assert
        mappings.Should().NotBeEmpty();
        mappings.Should().ContainKeys(
            "jornada completa", "jornada parcial", "prácticas",
            "temporal", "indefinido", "freelance");
    }
}
