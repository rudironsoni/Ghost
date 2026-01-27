# Ghostwright

A sophisticated stealth browser automation framework with a pluggable extension architecture.

## Architecture

Ghostwright is organized as a monorepo with strict layering:

```
┌─────────────────────────────────────────┐
│              LAYER 4: SDK               │
│         Ghostwright.Sdk (meta-pkg)      │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│           LAYER 3: HOSTING              │
│     Ghostwright.Hosting.{*,WebApi}      │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│          LAYER 2: PLATFORMS             │
│  Anthropic │ Google │ LinkedIn │ OpenAI │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│          LAYER 1: CONTRACTS             │
│      Ghostwright.Contracts.*            │
└─────────────────────┬───────────────────┘
                      │
╔═════════════════════╧═══════════════════╗
║          LAYER 0: KERNEL                ║
║            Ghostwright                  ║
║  (Stealth browser - fully isolated)     ║
╚═════════════════════════════════════════╝
```

## Quick Start

```bash
# Install the SDK package (includes everything)
dotnet add package Ghostwright.Sdk
```

```csharp
using Ghostwright.Hosting;
using Ghostwright.Platform.Anthropic;
using Ghostwright.Platform.LinkedIn;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddGhostwright(ghost =>
        {
            ghost.ConfigureKernel(k => k.Headless = true);
            ghost.UseExtension<AnthropicExtension>();
            ghost.UseExtension<LinkedInExtension>();
        });
    })
    .Build();

// Get services via DI
var inference = host.Services.GetRequiredService<IInferenceClient>();
var jobs = host.Services.GetRequiredService<IJobClient>();
```

## Packages

| Package | Description |
|---------|-------------|
| `Ghostwright` | Core stealth browser engine |
| `Ghostwright.Contracts` | Core interfaces (IBrowserSession, IPage) |
| `Ghostwright.Contracts.Inference` | IInferenceClient contract |
| `Ghostwright.Contracts.Social` | ISocialClient contract |
| `Ghostwright.Contracts.Jobs` | IJobClient contract |
| `Ghostwright.Contracts.News` | INewsClient contract |
| `Ghostwright.Platform.Anthropic` | Claude via claude.ai |
| `Ghostwright.Platform.OpenAI` | ChatGPT via chatgpt.com |
| `Ghostwright.Platform.Google` | Gemini via gemini.google.com |
| `Ghostwright.Platform.LinkedIn` | LinkedIn automation |
| `Ghostwright.Hosting` | DI and configuration |
| `Ghostwright.Hosting.WebApi` | ASP.NET Core integration |
| `Ghostwright.Sdk` | Meta-package for quick start |

## Building

```bash
# Restore and build
dotnet build Ghost.sln

# Run tests
dotnet test Ghost.sln

# Run tests with coverage
dotnet test Ghost.sln --collect:"XPlat Code Coverage"
```

## License

MIT
