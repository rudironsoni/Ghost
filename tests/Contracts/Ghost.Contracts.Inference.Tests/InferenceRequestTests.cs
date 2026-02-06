using System;
using FluentAssertions;
using Ghost.Contracts.Inference;
using Xunit;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceRequestTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var r = new InferenceRequest();
        r.Model.Should().BeEmpty();
        r.Messages.Should().BeEmpty();
        r.Temperature.Should().Be(0.0);
        r.MaxTokens.Should().Be(0);
        r.TopP.Should().Be(1.0);
        r.StopSequences.Should().BeEmpty();
        r.SystemPrompt.Should().BeNull();
    }

    [Fact]
    public void Can_Set_And_Equality_Works()
    {
        var m = new InferenceMessage { Role = InferenceRole.Assistant, Content = "hi" };
        var r1 = new InferenceRequest { Model = "x", Messages = new[] { m }, Temperature = 0.5, MaxTokens = 10, TopP = 0.9, StopSequences = new[] { "s" }, SystemPrompt = "sys" };
        var r2 = new InferenceRequest { Model = "x", Messages = new[] { m }, Temperature = 0.5, MaxTokens = 10, TopP = 0.9, StopSequences = new[] { "s" }, SystemPrompt = "sys" };

        r1.Should().BeEquivalentTo(r2);
    }

    [Fact]
    public void EdgeCases_NullOrEmpty_Accepted()
    {
        var r = new InferenceRequest { Model = string.Empty, Messages = Array.Empty<InferenceMessage>(), StopSequences = Array.Empty<string>() };
        r.Model.Should().Be(string.Empty);
    }
}
