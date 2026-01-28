# Ghost

A sophisticated stealth browser automation framework with a pluggable extension architecture.

## Architecture

Ghost is organized as a monorepo with strict layering:

```
┌─────────────────────────────────────────┐
│              LAYER 4: SDK               │
│         Ghost.Sdk (meta-pkg)      │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│           LAYER 3: HOSTING              │
│     Ghost.Hosting.{*,WebApi}      │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│          LAYER 2: PLATFORMS             │
│  Anthropic │ Google │ LinkedIn │ OpenAI │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│          LAYER 1: CONTRACTS             │
│      Ghost.Contracts.*            │
└─────────────────────┬───────────────────┘
                      │
╔═════════════════════╧═══════════════════╗
║          LAYER 0: KERNEL                ║
║            Ghost                  ║
║  (Stealth browser - fully isolated)     ║
╚═════════════════════════════════════════╝
```

## Quick Start

```bash
# Install the SDK package (includes everything)
dotnet add package Ghost.Sdk
```

```csharp
using Ghost.Hosting;
using Ghost.Platform.Anthropic;
using Ghost.Platform.LinkedIn;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddGhost(ghost =>
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
| `Ghost` | Core stealth browser engine |
| `Ghost.Contracts` | Core interfaces (IBrowserSession, IPage) |
| `Ghost.Contracts.Inference` | IInferenceClient contract |
| `Ghost.Contracts.Social` | ISocialClient contract |
| `Ghost.Contracts.Jobs` | IJobClient contract |
| `Ghost.Contracts.News` | INewsClient contract |
| `Ghost.Platform.Anthropic` | Claude via claude.ai |
| `Ghost.Platform.OpenAI` | ChatGPT via chatgpt.com |
| `Ghost.Platform.Google` | Gemini via gemini.google.com |
| `Ghost.Platform.LinkedIn` | LinkedIn automation |
| `Ghost.Hosting` | DI and configuration |
| `Ghost.Hosting.WebApi` | ASP.NET Core integration |
| `Ghost.Sdk` | Meta-package for quick start |

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
