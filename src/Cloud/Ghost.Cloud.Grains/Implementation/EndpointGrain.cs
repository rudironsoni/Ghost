using Ghost.Cloud.Contracts.Endpoints;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.State;
using Orleans.Runtime;

namespace Ghost.Cloud.Grains.Implementation;

public sealed class EndpointGrain : Grain, IEndpointGrain
{
    private readonly IPersistentState<EndpointState> _state;

    public EndpointGrain([PersistentState("endpoint", "Default")] IPersistentState<EndpointState> state)
    {
        _state = state;
    }

    public Task<EndpointManifest> GetManifestAsync()
    {
        return Task.FromResult(new EndpointManifest
        {
            EndpointId = _state.State.EndpointId,
            Version = _state.State.Version,
            PluginId = _state.State.PluginId,
            DisplayName = _state.State.DisplayName,
            Capability = _state.State.Capability,
            InputSchema = _state.State.InputSchema,
            OutputSchema = _state.State.OutputSchema,
            SupportedDeliveryModes = _state.State.SupportedDeliveryModes
        });
    }

    public Task ValidateInputAsync(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Undefined || input.ValueKind == JsonValueKind.Null)
        {
            throw new ArgumentException("Input cannot be null or undefined");
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync() => Task.FromResult(_state.State.IsHealthy);

    public async Task UpdateHealthAsync(bool healthy, string? errorMessage)
    {
        _state.State.IsHealthy = healthy;
        _state.State.LastErrorMessage = errorMessage;
        _state.State.LastHealthCheck = DateTimeOffset.UtcNow;
        await _state.WriteStateAsync().ConfigureAwait(false);
    }
}
