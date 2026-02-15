using Ghost.Kernel; // for KernelOptions
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Hosting;

/// <summary>
/// Options for Ghost hosting.
/// </summary>
public sealed class GhostOptions
{
    /// <summary>
    /// Kernel options used when creating the underlying browser kernel.
    /// </summary>
    public KernelOptions Kernel { get; set; } = new();

    /// <summary>
    /// When true, extension dependency relationships will be validated during startup.
    /// </summary>
    public bool ValidateExtensionDependencies { get; set; } = true;
}
