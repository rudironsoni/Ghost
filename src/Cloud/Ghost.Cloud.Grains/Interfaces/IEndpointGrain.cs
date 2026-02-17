using Ghost.Cloud.Contracts.Endpoints;

namespace Ghost.Cloud.Grains.Interfaces;

public interface IEndpointGrain : IGrainWithStringKey
{
    public Task<EndpointManifest> GetManifestAsync();
    public Task ValidateInputAsync(JsonElement input);
    public Task<bool> IsHealthyAsync();
    public Task UpdateHealthAsync(bool healthy, string? errorMessage);
}
