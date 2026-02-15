// This file is intentionally kept as a thin wrapper to reference the
// IExtension contract defined in Ghost.Contracts. Remove this file
// to avoid duplicate definitions and ensure consumers use Ghost.Contracts.IExtension.
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Hosting;

// Thin wrapper that forwards to the Contracts definition.
public interface IExtension : Ghost.Contracts.IExtension { }
