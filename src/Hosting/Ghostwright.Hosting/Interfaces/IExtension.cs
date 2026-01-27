// This file is intentionally kept as a thin wrapper to reference the
// IExtension contract defined in Ghostwright.Contracts. Remove this file
// to avoid duplicate definitions and ensure consumers use Ghostwright.Contracts.IExtension.
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Hosting;

// Thin wrapper that forwards to the Contracts definition.
public interface IExtension : Ghostwright.Contracts.IExtension { }
