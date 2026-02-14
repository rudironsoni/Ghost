using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Ghost.Plugin.OpenAI.Tests;

public class OpenAIPluginTests
{
    [Fact]
    public void NameShouldContainOpenAI()
    {
        var plugin = new OpenAIPlugin();
        plugin.Name.Should().NotBeNullOrEmpty();
        plugin.Name.ToLowerInvariant().Should().Contain("openai");
    }

    [Fact]
    public void ConfigureServicesDoesNotThrow()
    {
        var plugin = new OpenAIPlugin();
        var services = new ServiceCollection();
        var configMock = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();
        configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(sectionMock.Object);
        Action act = () => plugin.ConfigureServices(services, configMock.Object);
        act.Should().NotThrow();
    }
}
