using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghostwright.Core; // for KernelOptions

namespace Ghostwright.Hosting;

/// <summary>
/// Options for Ghostwriter hosting.
/// </summary>
public sealed class GhostwriterOptions
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
