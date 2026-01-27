# Ghostwright Monorepo Unification Plan

**Date:** 2026-01-27  
**Author:** Principal .NET Engineer  
**Status:** Approved for Implementation

---

## Executive Summary

Unify 8+ separate Ghostwright repositories into a single monorepo with a sophisticated architecture where the Ghostwright stealth browser is an isolated kernel module, and all other components (AI platforms, social platforms) are pluggable extensions.

---

## Current State

### Existing Repositories

| Repository | Purpose | Dependencies |
|------------|---------|--------------|
| `Ghostwright` | Core stealth browser (Patchright) | Standalone |
| `Ghostwright.Abstractions.Inference` | `IInferenceClient` contract | None |
| `Ghostwright.Abstractions.Social` | `ISocialClient` contract | None |
| `Ghostwright.Abstractions.Jobs` | `IJobClient` contract | None |
| `Ghostwright.Abstractions.News` | `INewsClient` contract | None |
| `Ghostwright.Abstractions.WebApi` | Web API contracts | None |
| `Ghostwright.Anthropic` | Anthropic/Claude integration | Ghostwright, Abstractions.Inference |
| `Ghostwright.OpenAI` | OpenAI/ChatGPT integration | Ghostwright, Abstractions.Inference |
| `Ghostwright.Google` | Google/Gemini integration | Ghostwright, Abstractions.Inference |
| `Ghostwright.LinkedIn` | LinkedIn automation | Ghostwright, Abstractions.* |

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
│   │   └── Ghostwright/
│   │       ├── Ghostwright.csproj
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
│   │   ├── Ghostwright.Contracts/
│   │   │   └── (re-exports kernel abstractions)
│   │   ├── Ghostwright.Contracts.Inference/
│   │   │   ├── IInferenceClient.cs
│   │   │   ├── InferenceRequest.cs
│   │   │   ├── InferenceResponse.cs
│   │   │   └── InferenceMessage.cs
│   │   ├── Ghostwright.Contracts.Social/
│   │   │   ├── ISocialClient.cs
│   │   │   ├── SocialProfile.cs
│   │   │   └── SocialPost.cs
│   │   ├── Ghostwright.Contracts.Jobs/
│   │   │   ├── IJobClient.cs
│   │   │   ├── JobListing.cs
│   │   │   └── JobApplication.cs
│   │   └── Ghostwright.Contracts.News/
│   │       ├── INewsClient.cs
│   │       └── NewsArticle.cs
│   │
│   ├── Platforms/                     # LAYER 2 - IMPLEMENTATIONS (FLAT)
│   │   ├── Ghostwright.Platform.Anthropic/
│   │   │   ├── AnthropicExtension.cs
│   │   │   ├── AnthropicClient.cs
│   │   │   └── AnthropicOptions.cs
│   │   ├── Ghostwright.Platform.Google/
│   │   │   ├── GoogleExtension.cs
│   │   │   ├── GoogleClient.cs
│   │   │   └── GoogleOptions.cs
│   │   ├── Ghostwright.Platform.LinkedIn/
│   │   │   ├── LinkedInExtension.cs
│   │   │   ├── LinkedInSocialClient.cs
│   │   │   ├── LinkedInJobClient.cs
│   │   │   └── LinkedInNewsClient.cs
│   │   └── Ghostwright.Platform.OpenAI/
│   │       ├── OpenAIExtension.cs
│   │       ├── OpenAIClient.cs
│   │       └── OpenAIOptions.cs
│   │
│   ├── Hosting/                       # LAYER 3 - COMPOSITION
│   │   ├── Ghostwright.Hosting/
│   │   │   ├── IExtension.cs
│   │   │   ├── GhostwriterBuilder.cs
│   │   │   ├── GhostwriterOptions.cs
│   │   │   └── ServiceCollectionExtensions.cs
│   │   └── Ghostwright.Hosting.WebApi/
│   │       └── WebApiServiceCollectionExtensions.cs
│   │
│   └── Sdk/                           # LAYER 4 - META-PACKAGE
│       └── Ghostwright.Sdk/
│           └── Ghostwright.Sdk.csproj
│
├── tests/                             # MIRRORS src/
│   ├── Core/
│   │   └── Ghostwright.Tests/
│   ├── Contracts/
│   │   ├── Ghostwright.Contracts.Tests/
│   │   └── Ghostwright.Contracts.Inference.Tests/
│   ├── Platforms/
│   │   ├── Ghostwright.Platform.Anthropic.Tests/
│   │   ├── Ghostwright.Platform.Google.Tests/
│   │   ├── Ghostwright.Platform.LinkedIn.Tests/
│   │   └── Ghostwright.Platform.OpenAI.Tests/
│   ├── Hosting/
│   │   └── Ghostwright.Hosting.Tests/
│   └── Integration/
│       └── Ghostwright.Integration.Tests/
│
├── samples/
│   ├── Ghostwright.Sample.Console/
│   ├── Ghostwright.Sample.WebApi/
│   └── Ghostwright.Sample.LinkedInBot/
│
├── tools/
│   ├── Ghostwright.Analyzers/
│   └── Ghostwright.SourceGenerators/
│
└── build/
    ├── Layers.props
    └── Package.props
```

### Dependency Graph

```
                    ┌──────────────────────────────────────┐
                    │              HOST                    │
                    │   services.AddGhostwright(...)       │
                    └───────────────┬──────────────────────┘
                                    │
                    ┌───────────────▼───────────────┐
                    │      LAYER 3: HOSTING         │
                    │  Ghostwright.Hosting          │
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
                    │  Ghostwright.Contracts.*      │
                    │  (IInferenceClient, etc.)     │
                    └───────────────┬───────────────┘
                                    │
                    ╔═══════════════╧═══════════════╗
                    ║      LAYER 0: KERNEL          ║
                    ║        Ghostwright            ║
                    ║  ───────────────────────────  ║
                    ║  IBrowserSession, IPage       ║
                    ║  Patchright (internal only)   ║
                    ╚═══════════════════════════════╝
```

---

## NuGet Packages

| Package | Layer | Purpose |
|---------|-------|---------|
| `Ghostwright` | Kernel | Core stealth browser engine |
| `Ghostwright.Contracts` | Contracts | Core browser interfaces |
| `Ghostwright.Contracts.Inference` | Contracts | `IInferenceClient` and DTOs |
| `Ghostwright.Contracts.Social` | Contracts | `ISocialClient` and DTOs |
| `Ghostwright.Contracts.Jobs` | Contracts | `IJobClient` and DTOs |
| `Ghostwright.Contracts.News` | Contracts | `INewsClient` and DTOs |
| `Ghostwright.Platform.Anthropic` | Platform | Claude via claude.ai |
| `Ghostwright.Platform.OpenAI` | Platform | ChatGPT via chatgpt.com |
| `Ghostwright.Platform.Google` | Platform | Gemini via gemini.google.com |
| `Ghostwright.Platform.LinkedIn` | Platform | LinkedIn automation |
| `Ghostwright.Hosting` | Hosting | DI and configuration |
| `Ghostwright.Hosting.WebApi` | Hosting | ASP.NET Core integration |
| `Ghostwright.Sdk` | SDK | Meta-package for quick start |

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

1. Extract public interfaces from Ghostwright core:
   - `IBrowserSession`
   - `IPage`
   - `IElement`
2. Create internal wrappers:
   - `BrowserSessionWrapper` (wraps Patchright context)
   - `PageWrapper` (wraps Patchright page)
   - `ElementWrapper` (wraps Patchright element handle)
3. Rename namespaces:
   - `Ghostwright.Abstractions.*` → `Ghostwright.Contracts.*`

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

3. Create `Ghostwright.Hosting`:
   ```csharp
   services.AddGhostwright(ghost =>
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
   - `Ghostwright.Sample.Console` - basic usage
   - `Ghostwright.Sample.WebApi` - ASP.NET Core integration
   - `Ghostwright.Sample.LinkedInBot` - real-world automation

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
namespace Ghostwright.Hosting;

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
namespace Ghostwright;

public interface IBrowserSession : IAsyncDisposable
{
    ValueTask<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default);
    IReadOnlyList<IPage> Pages { get; }
    ValueTask<ICookieManager> GetCookieManagerAsync(CancellationToken ct = default);
}
```

### IPage

```csharp
namespace Ghostwright;

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
namespace Ghostwright.Contracts.Inference;

public interface IInferenceClient
{
    Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken ct = default);
    IAsyncEnumerable<InferenceChunk> StreamAsync(InferenceRequest request, CancellationToken ct = default);
}
```
