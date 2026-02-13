using FluentAssertions;
using Ghost.Engine.Abstractions.Settings;
using Xunit;

namespace Ghost.Engine.Abstractions.Tests;

public sealed class GhostSettingsTests
{
    [Fact]
    public void GetOrDefault_ReturnsOverrideWhenPresent()
    {
        var settings = new LayeredGhostSettings(
            new Dictionary<string, object?> { ["TimeoutMs"] = 1000 },
            new Dictionary<string, object?> { ["TimeoutMs"] = 5000 });

        var timeout = settings.GetOrDefault("TimeoutMs", 2000);

        timeout.Should().Be(5000);
    }

    [Fact]
    public void TryGet_ReturnsFalseWhenMissing()
    {
        var settings = new LayeredGhostSettings(
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>());

        var exists = settings.TryGet<int>("MissingKey", out var value);

        exists.Should().BeFalse();
        value.Should().Be(default);
    }

    private sealed class LayeredGhostSettings : IGhostSettings
    {
        private readonly IReadOnlyDictionary<string, object?> _base;
        private readonly IReadOnlyDictionary<string, object?> _override;

        public LayeredGhostSettings(
            IReadOnlyDictionary<string, object?> baseValues,
            IReadOnlyDictionary<string, object?> overrideValues)
        {
            _base = baseValues;
            _override = overrideValues;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            if (_override.TryGetValue(key, out var overrideValue) && overrideValue is T castOverride)
            {
                value = castOverride;
                return true;
            }

            if (_base.TryGetValue(key, out var baseValue) && baseValue is T castBase)
            {
                value = castBase;
                return true;
            }

            value = default;
            return false;
        }

        public T GetOrDefault<T>(string key, T defaultValue)
        {
            return TryGet<T>(key, out var value) && value is not null
                ? value
                : defaultValue;
        }
    }
}
