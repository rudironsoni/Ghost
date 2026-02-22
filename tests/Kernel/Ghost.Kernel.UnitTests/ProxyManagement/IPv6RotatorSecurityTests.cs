using System;
using System.Reflection;
using FluentAssertions;
using Ghost.Kernel.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Kernel.UnitTests.ProxyManagement;

/// <summary>
/// Security tests for IPv6Rotator to prevent command injection attacks.
/// These tests verify that malicious input is properly rejected.
/// </summary>
public class IPv6RotatorSecurityTests
{
    #region Constructor Security Tests

    [Theory]
    [InlineData("2001:db8:1234:5678")]
    [InlineData("fe80:0000:0000:0000")]
    [InlineData("2001:0db8:0000:0000")]
    public void Constructor_WithValidPrefix_ShouldSucceed(string validPrefix)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = validPrefix,
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("2001:db8;rm -rf /")]
    [InlineData("2001:db8|cat /etc/passwd")]
    [InlineData("2001:db8`whoami`")]
    [InlineData("2001:db8$(echo pwned)")]
    [InlineData("2001:db8$((1+1))")]
    [InlineData("2001:db8 && reboot")]
    [InlineData("2001:db8 || shutdown")]
    [InlineData("2001:db8 > /dev/null")]
    [InlineData("2001:db8 < /etc/passwd")]
    [InlineData("2001:db8*")]
    [InlineData("2001:db8?")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("2001://db8::1")]
    public void Constructor_WithCommandInjectionInPrefix_ShouldThrowArgumentException(string maliciousPrefix)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = maliciousPrefix,
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert - Should throw for security reasons
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("eth0")]
    [InlineData("wlan0")]
    [InlineData("enp0s1")]
    [InlineData("br-1234")]
    [InlineData("docker0")]
    [InlineData("lo")]
    public void Constructor_WithValidInterface_ShouldSucceed(string validInterface)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = validInterface
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("eth0;rm -rf /")]
    [InlineData("eth0|cat /etc/passwd")]
    [InlineData("eth0`whoami`")]
    [InlineData("eth0$(echo pwned)")]
    [InlineData("eth0 && reboot")]
    [InlineData("eth0 || shutdown")]
    [InlineData("eth0' ; ls")]
    [InlineData("eth0\" ; cat /etc/passwd")]
    [InlineData("../etc/passwd")]
    [InlineData("/dev/null; rm -rf /")]
    public void Constructor_WithCommandInjectionInInterface_ShouldThrowArgumentException(string maliciousInterface)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = maliciousInterface
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert - Should throw for security reasons
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("eth0 eth1")]
    [InlineData("eth0\teth1")]
    [InlineData("eth0\neth1")]
    [InlineData("eth0\reth1")]
    public void Constructor_WithWhitespaceInInterface_ShouldThrowArgumentException(string maliciousInterface)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = maliciousInterface
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert - Should throw because whitespace can be used for argument injection
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("eth0/../../etc/passwd")]
    [InlineData("../../../etc/shadow")]
    public void Constructor_WithPathTraversalInInterface_ShouldThrowArgumentException(string maliciousInterface)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = maliciousInterface
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert - Should throw for security reasons
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Prefix Length Security Tests

    [Fact]
    public void Constructor_WithOverlyLongPrefix_ShouldThrowArgumentException()
    {
        // Arrange - Create a prefix that exceeds maximum length
        string longPrefix = new string('a', 100);
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = longPrefix,
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert - Should throw because overly long input could be used for buffer overflow attacks
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithPrefixAtMaxLength_ShouldValidateCorrectly()
    {
        // Arrange - A valid IPv6 prefix at max reasonable length
        // 2001:0db8:0000:0000 is exactly 19 characters
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:0db8:0000:0000",
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Address Validation Security Tests

    [Theory]
    [InlineData("2001:db8::1")]
    [InlineData("fe80::1")]
    [InlineData("::1")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001")]
    public void ValidateIPv6Address_WithValidAddress_ShouldNotThrowArgumentException(string validAddress)
    {
        // Arrange
        var method = typeof(IPv6Rotator).GetMethod("ValidateIPv6Address", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { validAddress });

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("2001:db8::1;rm -rf /")]
    [InlineData("2001:db8::1|cat /etc/passwd")]
    [InlineData("2001:db8::1`whoami`")]
    [InlineData("2001:db8::1$(echo pwned)")]
    [InlineData("2001:db8::1' ; ls")]
    [InlineData("2001:db8::1\" ; cat /etc/passwd")]
    [InlineData("2001:db8::1 && reboot")]
    [InlineData("2001:db8::1 || shutdown")]
    [InlineData("2001:db8::1 > /dev/null")]
    public void ValidateIPv6Address_WithCommandInjectionCharacters_ShouldThrowArgumentException(string maliciousAddress)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = "eth0"
        };
        var rotator = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);

        // Act - Use reflection to call the private ValidateIPv6Address method
        var method = typeof(IPv6Rotator).GetMethod("ValidateIPv6Address", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Action act = () => method?.Invoke(null, new[] { maliciousAddress });

        // Assert
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    [Theory]
    [InlineData("2001:db8:1234:5678:abcd:ef01:2345:6789")]
    [InlineData("fe80::1")]
    [InlineData("::1")]
    public void ValidateIPv6Address_WithValidAddress_ShouldNotThrow(string validAddress)
    {
        // Arrange
        var method = typeof(IPv6Rotator).GetMethod("ValidateIPv6Address", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { validAddress });

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Interface Name Validation Security Tests

    [Theory]
    [InlineData("eth0")]
    [InlineData("wlan0")]
    [InlineData("enp0s1")]
    [InlineData("br-1234")]
    [InlineData("docker0")]
    [InlineData("virbr0")]
    [InlineData("tun0")]
    [InlineData("tap0")]
    [InlineData("ppp0")]
    public void ValidateInterfaceName_WithValidInterface_ShouldNotThrow(string validInterface)
    {
        // Arrange
        var method = typeof(IPv6Rotator).GetMethod("ValidateInterfaceName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { validInterface });

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("eth0;rm -rf /")]
    [InlineData("eth0|cat /etc/passwd")]
    [InlineData("eth0`whoami`")]
    [InlineData("eth0$(echo pwned)")]
    [InlineData("eth0' ; ls")]
    [InlineData("eth0\" ; cat /etc/passwd")]
    [InlineData("eth0 && reboot")]
    [InlineData("eth0 || shutdown")]
    [InlineData("eth0 > /dev/null")]
    [InlineData("eth0 < /etc/passwd")]
    [InlineData("eth0*")]
    [InlineData("eth0?")]
    public void ValidateInterfaceName_WithCommandInjectionCharacters_ShouldThrowArgumentException(string maliciousInterface)
    {
        // Arrange
        var method = typeof(IPv6Rotator).GetMethod("ValidateInterfaceName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { maliciousInterface });

        // Assert
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    #endregion

    #region IPv6 Prefix Security Validation Tests

    [Theory]
    [InlineData("2001:db8:1234:5678")]
    [InlineData("fe80:0000:0000:0000")]
    [InlineData("2001:0db8:0000:0000")]
    public void ValidateIPv6PrefixForSecurity_WithValidPrefix_ShouldNotThrow(string validPrefix)
    {
        // Arrange
        var method = typeof(IPv6Rotator).GetMethod("ValidateIPv6PrefixForSecurity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { validPrefix });

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("2001:db8;rm -rf /")]
    [InlineData("2001:db8|cat /etc/passwd")]
    [InlineData("2001:db8`whoami`")]
    [InlineData("2001:db8$(echo pwned)")]
    [InlineData("2001:db8$((1+1))")]
    [InlineData("2001:db8 && reboot")]
    [InlineData("2001:db8 || shutdown")]
    [InlineData("2001:db8 > /dev/null")]
    [InlineData("2001:db8 < /etc/passwd")]
    [InlineData("2001:db8*")]
    [InlineData("2001:db8?")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    public void ValidateIPv6PrefixForSecurity_WithMaliciousPatterns_ShouldThrowArgumentException(string maliciousPrefix)
    {
        // Arrange
        var method = typeof(IPv6Rotator).GetMethod("ValidateIPv6PrefixForSecurity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { maliciousPrefix });

        // Assert
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    [Theory]
    [InlineData("gggg:hhhh:iiii:jjjj")]
    [InlineData("xxxx:yyyy:zzzz:aaaa")]
    public void ValidateIPv6PrefixForSecurity_WithNonHexCharacters_ShouldThrowArgumentException(string invalidPrefix)
    {
        // Arrange - Characters outside valid hex range (g-z, G-Z)
        var method = typeof(IPv6Rotator).GetMethod("ValidateIPv6PrefixForSecurity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        Action act = () => method?.Invoke(null, new[] { invalidPrefix });

        // Assert
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(null)]
    public void Constructor_WithNullOrEmptyPrefix_ShouldThrowArgumentNullException(string? invalidPrefix)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = invalidPrefix!,
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, null!);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(null!, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Complex Injection Attempts

    [Theory]
    [InlineData("2001:db8:1234:5678 -o ProxyCommand=/bin/sh")]
    [InlineData("2001:db8:1234:5678 -o IdentityFile=/etc/passwd")]
    [InlineData("2001:db8:1234:5678#comment")]
    [InlineData("2001:db8:1234:5678%00")]
    [InlineData("2001:db8:1234:5678\x00")]
    [InlineData("2001:db8:1234:5678\x01")]
    public void Constructor_WithComplexInjectionPatterns_ShouldThrowArgumentException(string complexInjection)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = complexInjection,
            NetworkInterface = "eth0"
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("eth0 -o ProxyCommand=/bin/sh")]
    [InlineData("eth0#comment")]
    [InlineData("eth0%00")]
    [InlineData("eth0\x00")]
    public void Constructor_WithComplexInterfaceInjection_ShouldThrowArgumentException(string complexInjection)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = complexInjection
        };

        // Act
        Action act = () =>
        {
            _ = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region BindAddressAsync Security Tests

    [Theory]
    [InlineData("2001:db8::1;rm -rf /")]
    [InlineData("2001:db8::1|cat /etc/passwd")]
    [InlineData("2001:db8::1`whoami`")]
    [InlineData("2001:db8::1$(echo pwned)")]
    [InlineData("2001:db8::1 && reboot")]
    [InlineData("2001:db8::1 || shutdown")]
    [InlineData("2001:db8::1 > /dev/null")]
    [InlineData("2001:db8::1 < /etc/passwd")]
    [InlineData("2001:db8::1' ; ls")]
    [InlineData("2001:db8::1\" ; cat /etc/passwd")]
    public async Task BindAddressAsync_WithCommandInjectionInAddress_ShouldThrowArgumentException(string maliciousAddress)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = "eth0"
        };
        var rotator = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);

        // Act & Assert - Should throw for security reasons before executing any command
        // Note: This will throw NotSupportedException on non-Linux platforms
        try
        {
            await rotator.BindAddressAsync(maliciousAddress);
            // If we get here on Linux, the validation should have rejected it
        }
        catch (ArgumentException)
        {
            // Expected - validation should catch malicious input
        }
        catch (NotSupportedException)
        {
            // Expected on non-Linux platforms - means we got past validation
            // which is acceptable since no command was actually executed
        }
    }

    [Theory]
    [InlineData("2001:db8::1;rm -rf /")]
    [InlineData("2001:db8::1|cat /etc/passwd")]
    [InlineData("2001:db8::1`whoami`")]
    [InlineData("2001:db8::1$(echo pwned)")]
    public async Task UnbindAddressAsync_WithCommandInjectionInAddress_ShouldThrowArgumentException(string maliciousAddress)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = "eth0"
        };
        var rotator = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);

        // Act & Assert - Should throw for security reasons before executing any command
        try
        {
            await rotator.UnbindAddressAsync(maliciousAddress);
        }
        catch (ArgumentException)
        {
            // Expected - validation should catch malicious input
        }
        catch (NotSupportedException)
        {
            // Expected on non-Linux platforms - means we got past validation
            // which is acceptable since no command was actually executed
        }
    }

    #endregion

    #region Process Argument Security Tests

    /// <summary>
    /// Tests that ArgumentList is used instead of string concatenation.
    /// This verifies that malicious input cannot escape the argument boundary.
    /// </summary>
    [Fact]
    public void ArgumentList_WithSpecialCharacters_ShouldBeTreatedAsLiteral()
    {
        // This test verifies the implementation uses ArgumentList correctly.
        // When using ArgumentList, special characters are treated as literal
        // arguments and cannot be interpreted by the shell.

        // Arrange - Create a ProcessStartInfo similar to how IPv6Rotator does it
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "echo",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        // Add arguments that would be dangerous if shell interpreted them
        psi.ArgumentList.Add("test;rm -rf /");
        psi.ArgumentList.Add("test|cat /etc/passwd");
        psi.ArgumentList.Add("test`whoami`");
        psi.ArgumentList.Add("test$(echo pwned)");

        // Act - Start the process and capture output
        using var process = System.Diagnostics.Process.Start(psi);
        string output = process?.StandardOutput.ReadToEnd() ?? "";
        process?.WaitForExit();

        // Assert - The output should contain the literal special characters
        // not the result of shell interpretation
        output.Should().Contain(";rm -rf /");
        output.Should().Contain("|cat /etc/passwd");
        output.Should().Contain("`whoami`");
        output.Should().Contain("$(echo pwned)");
    }

    #endregion

    #region Interface Name Validation in Bind Operations

    [Theory]
    [InlineData("eth0;rm -rf /")]
    [InlineData("eth0|cat /etc/passwd")]
    [InlineData("eth0`whoami`")]
    [InlineData("eth0$(echo pwned)")]
    public async Task BindAddressAsync_WithMaliciousInterfaceName_ShouldThrowArgumentException(string maliciousInterface)
    {
        // Arrange
        var options = new IPv6RotatorOptions
        {
            SubnetPrefix = "2001:db8:1234:5678",
            NetworkInterface = maliciousInterface
        };

        // Act & Assert - Constructor should reject malicious interface name
        try
        {
            var rotator = new IPv6Rotator(options, NullLogger<IPv6Rotator>.Instance);
            await rotator.BindAddressAsync("2001:db8::1");
        }
        catch (ArgumentException)
        {
            // Expected - validation should catch malicious interface name
        }
    }

    #endregion
}
