using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Ghost.Platform.OpenAI.Tests;

public class OpenAIExtensionTests
{
    [Fact]
    public void NameShouldContainOpenAI()
    {
        var ext = new OpenAIExtension();
        ext.Name.Should().NotBeNullOrEmpty();
        ext.Name.ToLowerInvariant().Should().Contain("openai");
    }

    [Fact]
    public void ConfigureServicesDoesNotThrow()
    {
        var ext = new OpenAIExtension();
        var services = new ServiceCollection();
        var configMock = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();
        configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(sectionMock.Object);
        Action act = () => ext.ConfigureServices(services, configMock.Object);
        act.Should().NotThrow();
    }
}
