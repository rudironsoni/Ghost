# Ghost

A .NET 10 job scraping platform with plugin architecture for extracting job listings from multiple job boards and career sites.

## Architecture

Ghost is built with a layered architecture that provides clear separation of concerns and extensibility:

- **Layer 0 - Kernel**: Core engine, stealth, sessions, proxies
- **Layer 1 - Contracts**: Public interfaces, DTOs, shared contracts
- **Layer 2 - Plugins**: Platform-specific plugins (LinkedIn, Indeed, Google, Glassdoor, etc.)
- **Layer 3 - Platform**: Shared infrastructure (Abstractions, Contracts, Extensions, Hosting, Observability, Storage)
- **Layer 4 - Engine**: Scraper engines
- **Layer 5 - Apps**: Deployable entrypoints (WebApi, Worker)
- **Layer 6 - Sdk**: Framework for building scrapers

## Directory Structure

```
Ghost/
├── src/
│   ├── Kernel/Ghost/              # Core engine (renamed from Core)
│   ├── Contracts/                 # Public interfaces and DTOs
│   ├── Plugins/                   # Platform-specific plugins
│   │   ├── Ghost.Plugin.LinkedIn/
│   │   ├── Ghost.Plugin.Indeed/
│   │   ├── Ghost.Plugin.Google/
│   │   └── ...
│   ├── Platform/                  # Shared infrastructure
│   │   ├── Abstractions/
│   │   ├── Contracts/
│   │   ├── Extensions/
│   │   ├── Hosting/
│   │   ├── Observability/
│   │   └── Storage/
│   ├── Engine/                    # Scraper engines
│   ├── Apps/                      # Deployable entrypoints
│   │   ├── Ghost.WebApi/
│   │   └── Ghost.Worker/
│   └── Sdk/                       # Framework for building scrapers
├── tests/                         # Test projects with suffix taxonomy
│   ├── Kernel/                    # Kernel tests
│   ├── Platform/                  # Platform tests
│   ├── Plugins/                   # Plugin tests
│   ├── Apps/                      # Application tests
│   ├── Engine/                    # Engine tests
│   ├── Contracts/                 # Contracts tests
│   ├── Sdk/                       # SDK tests
│   ├── Shared/                    # Shared testing infrastructure
│   ├── Legacy/                    # Legacy tests (preserved for reference)
│   └── Architecture/              # Architecture tests
├── docs/                          # Documentation
├── Ghost.sln                      # Solution file
└── docker-compose.yml             # Docker composition
```

## Build

Build the entire solution:

```bash
dotnet build Ghost.sln
```

## Test

Run all tests:

```bash
dotnet test Ghost.sln
```

## Docker

Build and run with Docker Compose:

```bash
docker-compose up
```

Build specific service:

```bash
docker-compose build ghost-webapi
```

## Quick Start

1. Clone the repository
2. Restore dependencies: `dotnet restore Ghost.sln`
3. Build the solution: `dotnet build Ghost.sln`
4. Run tests: `dotnet test Ghost.sln`
5. Run the Web API: `dotnet run --project src/Apps/Ghost.WebApi/Ghost.WebApi.csproj`

## Features

- **Plugin Architecture**: Extensible plugin system for adding new job boards
- **Stealth Mode**: Advanced browser fingerprinting and behavior mimicking
- **Proxy Management**: Rotating proxy support with health checking
- **Resilience**: Circuit breaker and retry patterns for reliability
- **Observability**: Structured logging, metrics, and health checks
- **Multi-Platform**: Support for LinkedIn, Indeed, Google, Glassdoor, and more

## Contributing

Contributions are welcome! Please ensure all tests pass and follow the coding standards defined in AGENTS.md.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
