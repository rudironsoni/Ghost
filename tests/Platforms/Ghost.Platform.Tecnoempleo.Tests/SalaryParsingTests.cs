using FluentAssertions;
using Ghost.Platform.Tecnoempleo.Jobs.Internal;
using Ghost.Contracts.Jobs;
using Xunit;

namespace Ghost.Platform.Tecnoempleo.Tests;

public class SalaryParsingTests
{
    [Theory]
    [InlineData("35.000 €", 35000, "EUR")]
    [InlineData("50.000 € al año", 50000, "EUR")]
    [InlineData("28.500 € brutos/año", 28500, "EUR")]
    [InlineData("40.000-45.000 €", 42500, "EUR")]
    [InlineData("30.000 - 35.000 €", 32500, "EUR")]
    [InlineData("55.000 €/año", 55000, "EUR")]
    [InlineData("25.000 € anuales", 25000, "EUR")]
    [InlineData("42.000 € brutos", 42000, "EUR")]
    [InlineData("20.000 € netos", 20000, "EUR")]
    [InlineData("65.000", 65000, "EUR")]
    [InlineData("75.000 EUR", 75000, "EUR")]
    [InlineData("85.000 euros", 85000, "EUR")]
    public void ParseSpanishSalaryValidFormatsReturnsCorrectAmount(string salaryText, decimal expectedAmount, string expectedCurrency)
    {
        var result = TecnoempleoConstants.ParseSpanishSalary(salaryText);

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
        var result = TecnoempleoConstants.ParseSpanishSalary(salaryText);

        result.Amount.Should().Be(expectedAmount);
        result.Currency.Should().Be(expectedCurrency);
    }

    [Theory]
    [InlineData("35,000 €")]
    [InlineData("35.000.000 €")]
    [InlineData("35K €")]
    [InlineData("35k euros")]
    [InlineData("€35.000")]
    public void ParseSpanishSalaryAlternativeFormatsReturnsCorrectAmount(string salaryText)
    {
        var result = TecnoempleoConstants.ParseSpanishSalary(salaryText);

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
    [InlineData("proyecto", JobType.Contract)]
    public void MapSpanishJobTypeValidTypesReturnsCorrectJobType(string spanishType, JobType expectedType)
    {
        var result = TecnoempleoConstants.MapSpanishJobType(spanishType);

        result.Should().Be(expectedType);
    }

    [Fact]
    public void GetSpanishSalaryPatternsReturnsCorrectPatterns()
    {
        var patterns = TecnoempleoConstants.GetSpanishSalaryPatterns();

        patterns.Should().NotBeEmpty();
        patterns.Should().Contain(p => p.Contains('€'));
        patterns.Should().Contain(p => p.Contains("EUR"));
        patterns.Should().Contain(p => p.Contains("euros"));
        patterns.Should().Contain(p => p.Contains("bruto"));
        patterns.Should().Contain(p => p.Contains("neto"));
    }

    [Fact]
    public void GetSpanishJobTypeMappingsReturnsCompleteMapping()
    {
        var mappings = TecnoempleoConstants.GetSpanishJobTypeMappings();

        mappings.Should().NotBeEmpty();
        mappings.Should().ContainKeys(
            "jornada completa", "jornada parcial", "prácticas", 
            "temporal", "indefinido", "freelance", "proyecto");
    }

    [Fact]
    public void GetTechnologyKeywordsReturnsCompleteList()
    {
        var keywords = TecnoempleoConstants.GetTechnologyKeywords();

        keywords.Should().NotBeEmpty();
        keywords.Should().Contain("desarrollador");
        keywords.Should().Contain("programador");
        keywords.Should().Contain("ingeniero");
        keywords.Should().Contain("python");
        keywords.Should().Contain("java");
        keywords.Should().Contain("javascript");
        keywords.Should().Contain("aws");
        keywords.Should().Contain("azure");
        keywords.Should().Contain("docker");
    }

    [Theory]
    [InlineData("Desarrollador Senior Java", true)]
    [InlineData("Programador Python", true)]
    [InlineData("Ingeniero DevOps", true)]
    [InlineData("Analista de Sistemas", true)]
    [InlineData("Consultor Cloud", true)]
    [InlineData("Gerente de Proyecto", false)]
    [InlineData("Director Comercial", false)]
    [InlineData("Asistente Administrativo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTechnologyJobValidatesCorrectly(string jobTitle, bool expectedResult)
    {
        var result = TecnoempleoConstants.IsTechnologyJob(jobTitle);

        result.Should().Be(expectedResult);
    }
}