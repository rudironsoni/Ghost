# Ghost Monorepo Unification Plan

**Date:** 2026-01-27  
**Author:** Principal .NET Engineer  
**Status:** Approved for Implementation

---

## Executive Summary

Unify 8+ separate Ghost repositories into a single monorepo with a sophisticated architecture where the Ghost stealth browser is an isolated kernel module, and all other components (AI platforms, social platforms) are pluggable extensions.

---

## Current State

### Existing Repositories

| Repository | Purpose | Dependencies |
|------------|---------|--------------|
| `Ghost` | Core stealth browser (Patchright) | Standalone |
| `Ghost.Abstractions.Inference` | `IInferenceClient` contract | None |
| `Ghost.Abstractions.Social` | `ISocialClient` contract | None |
| `Ghost.Abstractions.Jobs` | `IJobClient` contract | None |
| `Ghost.Abstractions.News` | `INewsClient` contract | None |
| `Ghost.Abstractions.WebApi` | Web API contracts | None |
| `Ghost.Anthropic` | Anthropic/Claude integration | Ghost, Abstractions.Inference |
| `Ghost.OpenAI` | OpenAI/ChatGPT integration | Ghost, Abstractions.Inference |
| `Ghost.Google` | Google/Gemini integration | Ghost, Abstractions.Inference |
| `Ghost.LinkedIn` | LinkedIn automation | Ghost, Abstractions.* |

### Problems with Current Structure

1. **Version drift** - Each repo versions independently
2. **Diamond dependency** - Multiple repos depend on same contracts
3. **Build complexity** - No unified solution, separate CI pipelines
4. **Refactoring friction** - Changes spanning repos require coordinated releases
5. **Discovery difficulty** - Related code scattered across repos

---

## Architecture Decisions

### Decision 1: Extension Coupling
**Choice:** Strict isolation  
**Rationale:** Extensions communicate only via contracts and DI. No extension can directly reference another extension. This enables:
- Independent testing
- Swappable implementations
- Clear dependency graph
- Parallel development

### Decision 2: Versioning Strategy
**Choice:** Lockstep versioning  
**Rationale:** All packages share the same version number. This:
- Simplifies compatibility matrix
- Reduces "which versions work together" confusion
- Enables atomic releases
- Matches modern monorepo patterns (Nx, Turborepo)

### Decision 3: Patchright Exposure
**Choice:** Full abstraction  
**Rationale:** Patchright/Playwright types are internal implementation details. Public API exposes `IPage`, `IElement`, `IBrowserSession`. This:
- Allows swapping browser engine later
- Prevents tight coupling to Playwright API
- Enables mocking in tests
- Provides stable API surface

### Decision 4: Platform Organization
**Choice:** Flat Platforms folder  
**Rationale:** All browser-based integrations are "Platforms" (including AI providers that use browser automation). Domain grouping adds complexity without value - contracts define capabilities, not folders.

### Decision 5: Git History
**Choice:** Fresh start  
**Rationale:** Clean slate without history migration complexity. Old repos will be archived for reference.

### Decision 6: Test Structure
**Choice:** Mirror src/ in tests/  
**Rationale:** Parallel structure makes navigation intuitive. `src/Platforms/X` → `tests/Platforms/X.Tests`

---

## Target Architecture

### Directory Structure

```
Ghost/
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── release.yml
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── GitVersion.yml
├── nuget.config
├── Ghost.sln
├── README.md
│
├── src/
│   ├── Core/                          # LAYER 0 - ISOLATED KERNEL
│   │   └── Ghost/
│   │       ├── Ghost.csproj
│   │       ├── Abstractions/          # Public interfaces
│   │       │   ├── IBrowserSession.cs
│   │       │   ├── IPage.cs
│   │       │   ├── IElement.cs
│   │       │   └── Options/
│   │       ├── Internal/              # Patchright wrappers
│   │       │   ├── BrowserSessionWrapper.cs
│   │       │   ├── PageWrapper.cs
│   │       │   └── ElementWrapper.cs
│   │       ├── Stealth/
│   │       ├── Session/
│   │       ├── Security/
│   │       └── Extensions/
│   │
│   ├── Contracts/                     # LAYER 1 - PURE INTERFACES
│   │   ├── Ghost.Contracts/
│   │   │   └── (re-exports kernel abstractions)
│   │   ├── Ghost.Contracts.Inference/
│   │   │   ├── IInferenceClient.cs
│   │   │   ├── InferenceRequest.cs
│   │   │   ├── InferenceResponse.cs
│   │   │   └── InferenceMessage.cs
│   │   ├── Ghost.Contracts.Social/
│   │   │   ├── ISocialClient.cs
│   │   │   ├── SocialProfile.cs
│   │   │   └── SocialPost.cs
│   │   ├── Ghost.Contracts.Jobs/
│   │   │   ├── IJobClient.cs
│   │   │   ├── JobListing.cs
│   │   │   └── JobApplication.cs
│   │   └── Ghost.Contracts.News/
│   │       ├── INewsClient.cs
│   │       └── NewsArticle.cs
│   │
│   ├── Platforms/                     # LAYER 2 - IMPLEMENTATIONS (FLAT)
│   │   ├── Ghost.Platform.Anthropic/
│   │   │   ├── AnthropicExtension.cs
│   │   │   ├── AnthropicClient.cs
│   │   │   └── AnthropicOptions.cs
│   │   ├── Ghost.Platform.Google/
│   │   │   ├── GoogleExtension.cs
│   │   │   ├── GoogleClient.cs
│   │   │   └── GoogleOptions.cs
│   │   ├── Ghost.Platform.LinkedIn/
│   │   │   ├── LinkedInExtension.cs
│   │   │   ├── LinkedInSocialClient.cs
│   │   │   ├── LinkedInJobClient.cs
│   │   │   └── LinkedInNewsClient.cs
│   │   └── Ghost.Platform.OpenAI/
│   │       ├── OpenAIExtension.cs
│   │       ├── OpenAIClient.cs
│   │       └── OpenAIOptions.cs
│   │
│   ├── Hosting/                       # LAYER 3 - COMPOSITION
│   │   ├── Ghost.Hosting/
│   │   │   ├── IExtension.cs
│   │   │   ├── GhostwriterBuilder.cs
│   │   │   ├── GhostwriterOptions.cs
│   │   │   └── ServiceCollectionExtensions.cs
│   │   └── Ghost.Hosting.WebApi/
│   │       └── WebApiServiceCollectionExtensions.cs
│   │
│   └── Sdk/                           # LAYER 4 - META-PACKAGE
│       └── Ghost.Sdk/
│           └── Ghost.Sdk.csproj
│
├── tests/                             # MIRRORS src/
│   ├── Core/
│   │   └── Ghost.Tests/
│   ├── Contracts/
│   │   ├── Ghost.Contracts.Tests/
│   │   └── Ghost.Contracts.Inference.Tests/
│   ├── Platforms/
│   │   ├── Ghost.Platform.Anthropic.Tests/
│   │   ├── Ghost.Platform.Google.Tests/
│   │   ├── Ghost.Platform.LinkedIn.Tests/
│   │   └── Ghost.Platform.OpenAI.Tests/
│   ├── Hosting/
│   │   └── Ghost.Hosting.Tests/
│   └── Integration/
│       └── Ghost.Integration.Tests/
│
├── samples/
│   ├── Ghost.Sample.Console/
│   ├── Ghost.Sample.WebApi/
│   └── Ghost.Sample.LinkedInBot/
│
├── tools/
│   ├── Ghost.Analyzers/
│   └── Ghost.SourceGenerators/
│
└── build/
    ├── Layers.props
    └── Package.props
```

### Dependency Graph

```
                    ┌──────────────────────────────────────┐
                    │              HOST                    │
                    │   services.AddGhost(...)       │
                    └───────────────┬──────────────────────┘
                                    │
                    ┌───────────────▼───────────────┐
                    │      LAYER 3: HOSTING         │
                    │  Ghost.Hosting          │
                    └───────────────┬───────────────┘
                                    │
    ┌───────────────────────────────┼───────────────────────────────┐
    │           │           │           │           │               │
    ▼           ▼           ▼           ▼           ▼               ▼
┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐       ┌────────┐
│Anthropic│ │ Google │ │LinkedIn│ │ OpenAI │ │Twitter │  ...  │ Future │
└────┬───┘ └────┬───┘ └────┬───┘ └────┬───┘ └────┬───┘       └────┬───┘
     │          │          │          │          │                │
     │          LAYER 2: PLATFORMS (FLAT)        │                │
     └──────────┴──────────┴──────────┴──────────┴────────────────┘
                                    │
                    ┌───────────────▼───────────────┐
                    │      LAYER 1: CONTRACTS       │
                    │  Ghost.Contracts.*      │
                    │  (IInferenceClient, etc.)     │
                    └───────────────┬───────────────┘
                                    │
                    ╔═══════════════╧═══════════════╗
                    ║      LAYER 0: KERNEL          ║
                    ║        Ghost            ║
                    ║  ───────────────────────────  ║
                    ║  IBrowserSession, IPage       ║
                    ║  Patchright (internal only)   ║
                    ╚═══════════════════════════════╝
```

---

## NuGet Packages

| Package | Layer | Purpose |
|---------|-------|---------|
| `Ghost` | Kernel | Core stealth browser engine |
| `Ghost.Contracts` | Contracts | Core browser interfaces |
| `Ghost.Contracts.Inference` | Contracts | `IInferenceClient` and DTOs |
| `Ghost.Contracts.Social` | Contracts | `ISocialClient` and DTOs |
| `Ghost.Contracts.Jobs` | Contracts | `IJobClient` and DTOs |
| `Ghost.Contracts.News` | Contracts | `INewsClient` and DTOs |
| `Ghost.Platform.Anthropic` | Platform | Claude via claude.ai |
| `Ghost.Platform.OpenAI` | Platform | ChatGPT via chatgpt.com |
| `Ghost.Platform.Google` | Platform | Gemini via gemini.google.com |
| `Ghost.Platform.LinkedIn` | Platform | LinkedIn automation |
| `Ghost.Hosting` | Hosting | DI and configuration |
| `Ghost.Hosting.WebApi` | Hosting | ASP.NET Core integration |
| `Ghost.Sdk` | SDK | Meta-package for quick start |

---

## Implementation Phases

### Phase 1: Scaffold & Migration

1. Create monorepo folder structure
2. Create build infrastructure:
   - `Directory.Build.props` (global settings)
   - `Directory.Packages.props` (Central Package Management)
   - `global.json` (SDK pinning)
   - `GitVersion.yml` (lockstep versioning)
   - `.editorconfig`, `.gitignore`, `nuget.config`
3. Copy source code from existing repos (no git history)
4. Create unified `Ghost.sln`

### Phase 2: Kernel Abstraction

1. Extract public interfaces from Ghost core:
   - `IBrowserSession`
   - `IPage`
   - `IElement`
2. Create internal wrappers:
   - `BrowserSessionWrapper` (wraps Patchright context)
   - `PageWrapper` (wraps Patchright page)
   - `ElementWrapper` (wraps Patchright element handle)
3. Rename namespaces:
   - `Ghost.Abstractions.*` → `Ghost.Contracts.*`

### Phase 3: Extension System

1. Define `IExtension` interface:
   ```csharp
   public interface IExtension
   {
       string Name { get; }
       Version Version { get; }
       IReadOnlyList<Type> ProvidedServices { get; }
       IReadOnlyList<Type> RequiredServices { get; }
       void ConfigureServices(IServiceCollection services, IConfiguration config);
   }
   ```

2. Implement extensions for all platforms

3. Create `Ghost.Hosting`:
   ```csharp
   services.AddGhost(ghost =>
   {
       ghost.ConfigureKernel(k => k.Headless = true);
       ghost.UseExtension<AnthropicExtension>();
       ghost.UseExtension<LinkedInExtension>();
   });
   ```

4. Replace NuGet references with ProjectReference

### Phase 4: Tooling & CI

1. Create Roslyn analyzers:
   - `KernelIsolationAnalyzer` (GW0001: Core cannot reference extensions)
   - `LayerDependencyAnalyzer` (GW0002: Layer ordering violations)

2. Set up GitHub Actions:
   - CI: build, test on every push/PR
   - Release: pack, publish to NuGet on tag

### Phase 5: Samples & Documentation

1. Create sample projects:
   - `Ghost.Sample.Console` - basic usage
   - `Ghost.Sample.WebApi` - ASP.NET Core integration
   - `Ghost.Sample.LinkedInBot` - real-world automation

2. Write documentation:
   - README.md with quick start
   - Architecture decision records
   - Migration guide from old repos

---

## Testing Strategy

### Unit Tests (80%+ coverage)

- **Core**: Browser abstraction, session management, stealth features
- **Contracts**: Validation, serialization
- **Platforms**: Client logic, page interactions (mocked browser)
- **Hosting**: Extension loading, DI configuration

### Integration Tests

- End-to-end browser automation
- Platform-specific flows (login, navigation)
- Extension interoperability

### Test Infrastructure

- xUnit as test framework
- NSubstitute for mocking
- FluentAssertions for assertions
- Coverlet for code coverage

---

## Migration Checklist

### Pre-Migration
- [ ] Freeze development on old repos
- [ ] Document any in-flight changes
- [ ] Notify team of migration timeline

### Migration
- [ ] Create new Ghost monorepo
- [ ] Copy source files
- [ ] Update namespaces and references
- [ ] Verify build passes
- [ ] Run all tests
- [ ] Verify code coverage ≥ 80%

### Post-Migration
- [ ] Archive old repositories
- [ ] Update CI/CD pipelines
- [ ] Update documentation links
- [ ] Announce migration complete

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Breaking changes | Comprehensive test suite, phased rollout |
| Build complexity | Central Package Management, unified props |
| Developer confusion | Clear folder structure, documentation |
| CI/CD migration | Parallel pipelines during transition |

---

## Success Criteria

1. All existing functionality preserved
2. Build passes with zero warnings
3. Test coverage ≥ 80% across solution
4. Sample applications run successfully
5. NuGet packages publish correctly
6. Old repositories archived

---

## Appendix: Key Interfaces

### IExtension

```csharp
namespace Ghost.Hosting;

public interface IExtension
{
    string Name { get; }
    Version Version { get; }
    IReadOnlyList<Type> ProvidedServices { get; }
    IReadOnlyList<Type> RequiredServices { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
```

### IBrowserSession

```csharp
namespace Ghost;

public interface IBrowserSession : IAsyncDisposable
{
    ValueTask<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default);
    IReadOnlyList<IPage> Pages { get; }
    ValueTask<ICookieManager> GetCookieManagerAsync(CancellationToken ct = default);
}
```

### IPage

```csharp
namespace Ghost;

public interface IPage : IAsyncDisposable
{
    string Url { get; }
    Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken ct = default);
    Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
    Task<T> EvaluateAsync<T>(string expression, CancellationToken ct = default);
    Task WaitForSelectorAsync(string selector, WaitOptions? options = null, CancellationToken ct = default);
    Task ScreenshotAsync(Stream destination, ScreenshotOptions? options = null, CancellationToken ct = default);
    Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default);
    Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default);
}
```

### IInferenceClient

```csharp
namespace Ghost.Contracts.Inference;

public interface IInferenceClient
{
    Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default);
    IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, CancellationToken ct = default);
}
```
