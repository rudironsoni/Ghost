using System;
using FluentAssertions;
using Xunit;

namespace Ghostwright.Hosting.Tests
{
    public class ExtensionExceptionTests
    {
        [Fact]
        public void Constructor_SetsExtensionName()
        {
            var ex = new ExtensionException("X","msg");
            ex.ExtensionName.Should().Be("X");
        }

        [Fact]
        public void Constructor_SetsMessage()
        {
            var ex = new ExtensionException("E","oops");
            ex.ToString().Should().Contain("oops");
        }

        [Fact]
        public void Message_ContainsExtensionName()
        {
            var ex = new ExtensionException("E","boom");
            ex.Message.Should().Contain("Extension: E");
        }
    }
}
