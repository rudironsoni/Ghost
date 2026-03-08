# Skill Index

Complete index of all 189+ skills in dotnet-agent-harness, organized for discoverability and routing.

> **Migration Note:** Category names were renamed in this version. Old → New mappings:
> - `devops` → `operations`
> - `platforms` → `ui-frameworks`
> - `tooling` → `developer-experience`

---

## Quick Navigation

- [By Category](#by-category) - Skills grouped by taxonomy category
- [By Complexity](#by-complexity) - Skills by difficulty level
- [Alphabetical List](#alphabetical-list) - Complete A-Z listing
- [By Target Platform](#by-target-platform) - Platform compatibility matrix

---

## By Category

### fundamentals (22 skills)

Core C# and .NET language/runtime skills.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-csharp-coding-standards | coding-standards | beginner | Naming, file layout, style rules |
| dotnet-csharp-modern-patterns | language-patterns | intermediate | Records, pattern matching, collection expressions |
| dotnet-csharp-async-patterns | language-patterns | intermediate | Task patterns, ConfigureAwait, cancellation |
| dotnet-csharp-dependency-injection | dependency-injection | intermediate | DI registration, lifetimes, keyed services |
| dotnet-csharp-concurrency-patterns | language-patterns | advanced | Lock, SemaphoreSlim, concurrent collections |
| dotnet-csharp-api-design | design-principles | advanced | Public API design, naming, error patterns |
| dotnet-csharp-source-generators | language-patterns | advanced | IIncrementalGenerator, GeneratedRegex, STJ |
| dotnet-csharp-type-design-performance | design-principles | advanced | Struct vs class, sealed, readonly struct |
| dotnet-csharp-nullable-reference-types | coding-standards | intermediate | Annotations, migration strategies |
| dotnet-csharp-code-smells | diagnostics | intermediate | Anti-patterns, async misuse, DI mistakes |
| dotnet-csharp-configuration | configuration | intermediate | Options pattern, user secrets, feature flags |
| dotnet-solid-principles | design-principles | intermediate | SOLID and DRY principles |
| dotnet-validation-patterns | configuration | intermediate | DataAnnotations, IValidatableObject |
| dotnet-input-validation | configuration | intermediate | .NET 10 AddValidation, FluentValidation |
| dotnet-file-io | fundamentals | intermediate | FileStream, RandomAccess, FileSystemWatcher |
| dotnet-gc-memory | fundamentals | advanced | GC modes, LOH/POH, Gen0/1/2 |
| dotnet-channels | fundamentals | advanced | Channel<T>, bounded/unbounded, backpressure |
| dotnet-io-pipelines | fundamentals | advanced | PipeReader/PipeWriter, protocol parsers |
| dotnet-file-based-apps | fundamentals | intermediate | .NET 10 file-based C# apps |
| dotnet-serialization | fundamentals | intermediate | System.Text.Json, Protobuf, MessagePack |
| dotnet-native-interop | fundamentals | advanced | P/Invoke, LibraryImport, marshalling |
| dotnet-aot-architecture | fundamentals | advanced | AOT-first design, source generators |

### testing (40 skills)

Testing methodology, frameworks, and specialized testing scenarios.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-testing | overview | beginner | Testing fundamentals overview and navigation |
| dotnet-testing-advanced | overview | intermediate | Advanced testing navigation center |
| dotnet-testing-unit-test-fundamentals | fundamentals | beginner | FIRST principles, 3A Pattern |
| dotnet-testing-test-naming-conventions | fundamentals | beginner | Naming conventions and best practices |
| dotnet-testing-xunit-project-setup | fundamentals | beginner | xUnit test project creation |
| dotnet-testing-awesome-assertions-guide | assertions | beginner | Fluent assertions with AwesomeAssertions |
| dotnet-testing-complex-object-comparison | assertions | intermediate | BeEquivalentTo, Excluding, deep comparison |
| dotnet-testing-nsubstitute-mocking | mocking | intermediate | NSubstitute for test doubles |
| dotnet-testing-autofixture-basics | test-data | beginner | Auto-generate test data |
| dotnet-testing-autofixture-customization | test-data | intermediate | ISpecimenBuilder, custom generators |
| dotnet-testing-autofixture-bogus-integration | test-data | intermediate | Hybrid AutoFixture + Bogus |
| dotnet-testing-autofixture-nsubstitute-integration | mocking | intermediate | Auto-mocking with NSubstitute |
| dotnet-testing-bogus-fake-data | test-data | beginner | Realistic fake data generation |
| dotnet-testing-test-data-builder-pattern | test-data | intermediate | Builder pattern for test data |
| dotnet-testing-datetime-testing-timeprovider | specialized | intermediate | TimeProvider, FakeTimeProvider |
| dotnet-testing-filesystem-testing-abstractions | specialized | intermediate | IFileSystem, MockFileSystem |
| dotnet-testing-private-internal-testing | specialized | advanced | InternalsVisibleTo, reflection testing |
| dotnet-testing-fluentvalidation-testing | assertions | intermediate | Testing FluentValidation validators |
| dotnet-testing-test-output-logging | fundamentals | intermediate | ITestOutputHelper, test logging |
| dotnet-testing-code-coverage-analysis | coverage | intermediate | Coverlet, Fine Code Coverage |
| dotnet-testing-strategy | overview | intermediate | Unit vs integration vs E2E decision |
| dotnet-xunit | frameworks | intermediate | xUnit v3 patterns, Theory, fixtures |
| dotnet-testing-advanced-xunit-upgrade-guide | frameworks | intermediate | xUnit 2.x to 3.x migration |
| dotnet-testing-advanced-tunit-fundamentals | frameworks | intermediate | TUnit new-gen testing framework |
| dotnet-testing-advanced-tunit-advanced | frameworks | advanced | TUnit data-driven, DI, retry/timeout |
| dotnet-integration-testing | integration | advanced | WebApplicationFactory, Testcontainers |
| dotnet-testing-advanced-aspnet-integration-testing | integration | advanced | ASP.NET Core integration testing |
| dotnet-testing-advanced-webapi-integration-testing | integration | advanced | Web API endpoint testing |
| dotnet-testing-advanced-testcontainers-database | integration | advanced | SQL Server, PostgreSQL, MySQL containers |
| dotnet-testing-advanced-testcontainers-nosql | integration | advanced | MongoDB, Redis container testing |
| dotnet-testing-advanced-aspire-testing | integration | advanced | .NET Aspire distributed app testing |
| dotnet-testing-autodata-xunit-integration | test-data | intermediate | [AutoData], [InlineAutoData] |
| dotnet-test-quality | coverage | advanced | Coverlet, Stryker.NET mutation testing |
| dotnet-snapshot-testing | testing | intermediate | Verify snapshots, API responses |
| dotnet-playwright | testing | intermediate | Browser automation tests |
| dotnet-blazor-testing | testing | intermediate | bUnit, component rendering |
| dotnet-maui-testing | testing | advanced | Appium device automation |
| dotnet-uno-testing | testing | advanced | Playwright for WASM |
| dotnet-ui-testing-core | testing | intermediate | Page objects, test selectors |
| dotnet-add-testing | tooling | beginner | Scaffold xUnit project |

### architecture (15 skills)

Design patterns, principles, and system architecture.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-architecture-patterns | patterns | intermediate | Vertical slices, pipelines, caching |
| dotnet-domain-modeling | domain-modeling | advanced | Aggregates, value objects, domain events |
| dotnet-messaging-patterns | messaging | advanced | Pub/sub, competing consumers, sagas |
| dotnet-resilience | resilience | intermediate | Polly v8, retry, circuit breaker |
| dotnet-caching-patterns | caching | intermediate | Memory, distributed, HybridCache |
| dotnet-middleware-patterns | patterns | intermediate | ASP.NET Core middleware |
| dotnet-background-services | patterns | intermediate | BackgroundService, IHostedService |
| dotnet-realtime-communication | messaging | advanced | SignalR, SSE, gRPC streaming |
| dotnet-service-communication | patterns | intermediate | REST vs gRPC vs SignalR decisions |
| dotnet-observability | patterns | intermediate | OpenTelemetry, tracing, metrics |
| dotnet-structured-logging | patterns | intermediate | Log pipelines, aggregation, correlation |
| dotnet-api-versioning | patterns | intermediate | Asp.Versioning.Http, URL/header versioning |
| dotnet-minimal-apis | patterns | intermediate | Route groups, filters, TypedResults |
| dotnet-grpc | patterns | intermediate | Proto definition, code-gen, streaming |
| dotnet-aspire-patterns | patterns | advanced | AppHost, service discovery, components |

### web (20 skills)

ASP.NET Core, web frameworks, and API development.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-blazor-patterns | blazor | intermediate | Hosting models, render modes, routing |
| dotnet-blazor-components | blazor | intermediate | Lifecycle, state management, JS interop |
| dotnet-blazor-auth | blazor | intermediate | Login/logout, AuthorizeView, OIDC |
| dotnet-api-design | api-design | advanced | Public API naming, return types, errors |
| dotnet-api-security | security | advanced | Identity, OAuth, JWT, passkeys |
| dotnet-openapi | api-design | intermediate | OpenAPI docs, Swashbuckle migration |
| dotnet-http-client | web | intermediate | IHttpClientFactory, typed clients |
| dotnet-minimal-apis | minimal-apis | intermediate | Minimal API patterns |
| dotnet-middleware-patterns | minimal-apis | intermediate | Pipeline ordering, exception handling |
| dotnet-input-validation | validation | intermediate | AddValidation, FluentValidation, ProblemDetails |
| dotnet-validation-patterns | validation | intermediate | DataAnnotations, IValidatableObject |
| dotnet-api-versioning | api-design | intermediate | Versioning strategies |
| dotnet-api-surface-validation | api-design | advanced | PublicApiAnalyzers, Verify snapshots |
| dotnet-api-docs | docs | intermediate | DocFX, OpenAPI-as-docs |
| dotnet-containers | web | intermediate | Containerize .NET apps |
| dotnet-container-deployment | web | advanced | Kubernetes, Docker Compose |
| dotnet-security-owasp | security | advanced | OWASP Top 10 mitigations |
| dotnet-documentation-strategy | docs | intermediate | Starlight, Docusaurus, DocFX decisions |
| dotnet-realtime-communication | web | advanced | SignalR, SSE, gRPC streaming |
| dotnet-10-csharp-14 | web | intermediate | .NET 10/C# 14 minimal APIs |

### data (12 skills)

Data access, EF Core, caching, and messaging.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-efcore-patterns | ef-core | intermediate | DbContext, AsNoTracking, query splitting |
| dotnet-efcore-architecture | ef-core | advanced | Read/write split, aggregates, N+1 |
| dotnet-data-access-strategy | data-access | intermediate | EF Core vs Dapper vs ADO.NET |
| dotnet-serialization | serialization | intermediate | System.Text.Json, Protobuf |
| dotnet-channels | messaging | advanced | Channel<T>, producer/consumer |
| dotnet-messaging-patterns | messaging | advanced | Event-driven patterns |
| dotnet-resilience | resilience | intermediate | Fault tolerance |
| dotnet-caching-patterns | caching | intermediate | Caching strategies |
| dotnet-testing-advanced-testcontainers-database | data-access | advanced | Database container testing |
| dotnet-testing-advanced-testcontainers-nosql | data-access | advanced | MongoDB, Redis testing |
| dotnet-input-validation | validation | intermediate | Request validation |
| dotnet-validation-patterns | validation | intermediate | Model validation |

### performance (12 skills)

Performance optimization and measurement.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-performance-patterns | patterns | advanced | Span, ArrayPool, ref struct, sealed |
| dotnet-gc-memory | memory | advanced | GC tuning, LOH/POH |
| dotnet-benchmarkdotnet | benchmarking | intermediate | BenchmarkDotNet setup, analysis |
| dotnet-profiling | profiling | advanced | dotnet-counters, dotnet-trace |
| dotnet-native-aot | aot | advanced | PublishAot, size optimization |
| dotnet-aot-architecture | aot | advanced | AOT-first design |
| dotnet-aot-wasm | aot | advanced | Blazor/Uno WASM AOT |
| dotnet-linq-optimization | patterns | intermediate | IQueryable vs IEnumerable |
| dotnet-csharp-type-design-performance | patterns | advanced | Struct design, sealed classes |
| dotnet-ci-benchmarking | benchmarking | advanced | CI performance gating |
| dotnet-maui-aot | aot | advanced | MAUI iOS/Catalyst optimization |
| dotnet-trimming | aot | intermediate | Trimming annotations |

### security (6 skills)

Security, authentication, and hardening.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-security-owasp | owasp | advanced | OWASP Top 10 mitigation |
| dotnet-api-security | auth | advanced | Identity, OAuth, JWT, passkeys |
| dotnet-cryptography | crypto | advanced | AES-GCM, RSA, ECDSA |
| dotnet-secrets-management | secrets | intermediate | User secrets, rotation |
| dotnet-blazor-auth | auth | intermediate | Blazor auth flows |
| dotnet-input-validation | validation | intermediate | Validation patterns |

### operations (26 skills)

CI/CD, containers, deployment, and automation.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-gha-build-test | github-actions | intermediate | GitHub Actions .NET build/test |
| dotnet-gha-deploy | github-actions | intermediate | Azure Web Apps, GitHub Pages |
| dotnet-gha-publish | github-actions | intermediate | NuGet push, containers |
| dotnet-gha-patterns | github-actions | intermediate | Reusable workflows, composite actions |
| dotnet-ado-build-test | azure-devops | intermediate | Azure DevOps build/test |
| dotnet-ado-publish | azure-devops | intermediate | NuGet push, containers to ACR |
| dotnet-ado-patterns | azure-devops | intermediate | Templates, variable groups |
| dotnet-ado-unique | azure-devops | intermediate | Environments, approvals |
| dotnet-containers | containers | intermediate | Multi-stage Dockerfiles |
| dotnet-container-deployment | deployment | advanced | Kubernetes, probes |
| dotnet-cli-release-pipeline | release | advanced | GitHub Actions release matrix |
| dotnet-cli-packaging | release | intermediate | Homebrew, apt, winget |
| dotnet-release-management | release | intermediate | NBGV versioning, changelogs |
| dotnet-github-releases | release | intermediate | GitHub Releases, assets |
| dotnet-github-docs | release | beginner | README, CONTRIBUTING |
| dotnet-add-ci | ci-cd | intermediate | GitHub vs Azure DevOps |
| dotnet-msbuild-authoring | ci-cd | advanced | MSBuild targets, props |
| dotnet-msbuild-tasks | ci-cd | advanced | ITask, ToolTask |
| dotnet-build-analysis | ci-cd | intermediate | MSBuild output, errors |
| dotnet-build-optimization | ci-cd | intermediate | Slow build diagnosis |
| dotnet-cli-distribution | ci-cd | intermediate | AOT vs framework-dependent |
| dotnet-library-api-compat | ci-cd | advanced | Binary/source compat |
| dotnet-api-surface-validation | ci-cd | advanced | Public API changes |
| dotnet-slopwatch | ci-cd | intermediate | Detect disabled tests |
| dotnet-artifacts-output | ci-cd | intermediate | UseArtifactsOutput layout |
| dotnet-project-structure | ci-cd | beginner | Solution layout, CPM |

### ui-frameworks (20 skills)

UI frameworks for mobile and desktop.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-maui-development | maui | intermediate | MAUI project structure, XAML |
| dotnet-maui-aot | maui | advanced | MAUI iOS/Catalyst AOT |
| dotnet-maui-testing | maui | advanced | Appium device automation |
| dotnet-wpf-modern | wpf | intermediate | MVVM Toolkit, Fluent theme |
| dotnet-wpf-migration | wpf | intermediate | WPF/WinForms to .NET 8+ |
| dotnet-winui | winui | intermediate | WinUI 3 desktop apps |
| dotnet-winforms-basics | winforms | beginner | WinForms modernization |
| dotnet-uno-platform | uno | intermediate | Uno Platform, Extensions |
| dotnet-uno-targets | uno | intermediate | WASM, iOS, Android deployment |
| dotnet-uno-testing | uno | advanced | Playwright WASM testing |
| dotnet-uno-mcp | uno | intermediate | Uno MCP server queries |
| dotnet-ui-chooser | platforms | beginner | Framework decision tree |
| dotnet-blazor-patterns | blazor | intermediate | Blazor hosting models |
| dotnet-blazor-components | blazor | intermediate | Component lifecycle |
| dotnet-blazor-auth | blazor | intermediate | Auth flows |
| dotnet-blazor-testing | blazor | intermediate | bUnit testing |
| dotnet-aot-wasm | platforms | advanced | WASM AOT compilation |
| dotnet-accessibility | platforms | intermediate | SemanticProperties, ARIA |
| dotnet-terminal-gui | platforms | intermediate | Terminal.Gui v2 apps |
| dotnet-spectre-console | platforms | intermediate | Rich console output |

### developer-experience (32 skills)

CLI tools, analyzers, MSBuild, and documentation.

| Skill | Subcategory | Complexity | Description |
|-------|-------------|------------|-------------|
| dotnet-cli-architecture | cli | intermediate | Command/handler separation |
| dotnet-cli-distribution | cli | intermediate | AOT vs framework-dependent |
| dotnet-cli-packaging | cli | intermediate | Package manager manifests |
| dotnet-cli-release-pipeline | cli | advanced | GitHub Actions releases |
| dotnet-system-commandline | cli | intermediate | System.CommandLine 2.0 |
| dotnet-roslyn-analyzers | analyzers | advanced | DiagnosticAnalyzer, CodeFixProvider |
| dotnet-add-analyzers | analyzers | intermediate | Nullable, trimming analyzers |
| dotnet-msbuild-authoring | msbuild | advanced | MSBuild targets, props |
| dotnet-msbuild-tasks | msbuild | advanced | ITask, ToolTask |
| dotnet-nuget-authoring | nuget | intermediate | SDK-style csproj packaging |
| dotnet-project-structure | project | beginner | Solution layout, CPM |
| dotnet-project-analysis | project | beginner | Solution layout analysis |
| dotnet-scaffold-project | project | beginner | Create .NET solution |
| dotnet-version-detection | project | beginner | Detect TFM/SDK |
| dotnet-version-upgrade | project | intermediate | TFM upgrade paths |
| dotnet-solution-navigation | project | beginner | Entry points, .sln/.slnx, dependency graphs |
| dotnet-csproj-reading | project | intermediate | Read SDK-style csproj |
| dotnet-xml-docs | docs | intermediate | XML documentation tags |
| dotnet-api-docs | docs | intermediate | DocFX, documentation |
| dotnet-documentation-strategy | docs | intermediate | Documentation tooling decisions |
| dotnet-editorconfig | developer-experience | intermediate | EditorConfig rules |
| dotnet-tool-management | developer-experience | beginner | Global/local .NET tools |
| dotnet-file-based-apps | developer-experience | intermediate | .NET 10 file-based apps |
| dotnet-mermaid-diagrams | developer-experience | intermediate | Mermaid for .NET |
| dotnet-serena-code-navigation | serena | intermediate | Serena MCP navigation |
| dotnet-serena-refactoring | serena | intermediate | Symbol-level refactoring |
| dotnet-serena-analysis-patterns | serena | intermediate | Architecture validation |
| serena | serena | intermediate | LSP operations |
| rulesync | rulesync | intermediate | RuleSync CLI tool |
| dotnet-agent-harness-manifest | rulesync | intermediate | Skill manifest management |
| dotnet-agent-harness-hooks | rulesync | intermediate | Hooks and MCP guide |
| dotnet-agent-harness-offline | rulesync | intermediate | Offline mode caching |
| dotnet-agent-harness-recommender | rulesync | intermediate | Skill recommendation |
| dotnet-agent-harness-test-framework | rulesync | advanced | Skill testing framework |

---

## By Complexity

### Beginner (57 skills)

Entry-level skills for developers new to .NET.

| Skill | Category | Description |
|-------|----------|-------------|
| dotnet-testing | testing | Testing fundamentals overview |
| dotnet-testing-unit-test-fundamentals | testing | FIRST principles, 3A Pattern |
| dotnet-testing-test-naming-conventions | testing | Naming conventions |
| dotnet-testing-xunit-project-setup | testing | xUnit project setup |
| dotnet-testing-awesome-assertions-guide | testing | Fluent assertions |
| dotnet-testing-autofixture-basics | testing | Auto-generate test data |
| dotnet-testing-bogus-fake-data | testing | Realistic fake data |
| dotnet-csharp-coding-standards | fundamentals | Coding standards |
| dotnet-solution-navigation | fundamentals | Solution structure |
| dotnet-version-detection | fundamentals | TFM detection |
| dotnet-add-testing | testing | Scaffold test project |
| dotnet-winforms-basics | ui-frameworks | WinForms modernization |
| dotnet-ui-chooser | ui-frameworks | Framework selection |
| dotnet-scaffold-project | developer-experience | Create solution |
| dotnet-project-structure | developer-experience | Solution layout |
| dotnet-project-analysis | developer-experience | Solution analysis |
| dotnet-tool-management | developer-experience | .NET tools |
| dotnet-roslyn-analyzers | developer-experience | Create analyzers |
| dotnet-editorconfig | developer-experience | EditorConfig |
| dotnet-github-docs | operations | GitHub documentation |
| dotnet-release-management | operations | Versioning |
| dotnet-version-detection | operations | Detect versions |

### Intermediate (95 skills)

Practical skills for working developers.

### Advanced (47 skills)

Expert-level skills for senior developers and architects.

---

## Alphabetical List

Complete A-Z listing of all skills with category assignment.

<details>
<summary>Click to expand full alphabetical list (189 skills)</summary>

| # | Skill Name | Category | Subcategory | Complexity |
|---|------------|----------|-------------|------------|
| 1 | agentic-eval | developer-experience | analyzers | intermediate |
| 2 | ai-prompt-engineering-safety-review | developer-experience | analyzers | advanced |
| 3 | deep-research | developer-experience | analyzers | intermediate |
| 4 | deep-wiki | developer-experience | docs | intermediate |
| 5 | dotnet-10-csharp-14 | fundamentals | language-patterns | intermediate |
| 6 | dotnet-add-analyzers | developer-experience | analyzers | intermediate |
| 7 | dotnet-add-ci | operations | ci-cd | intermediate |
| 8 | dotnet-add-testing | testing | frameworks | beginner |
| 9 | dotnet-ado-build-test | operations | azure-devops | intermediate |
| 10 | dotnet-ado-patterns | operations | azure-devops | intermediate |
| 11 | dotnet-ado-publish | operations | azure-devops | intermediate |
| 12 | dotnet-ado-unique | operations | azure-devops | intermediate |
| 13 | dotnet-advisor | developer-experience | analyzers | intermediate |
| 14 | dotnet-agent-gotchas | fundamentals | diagnostics | intermediate |
| 15 | dotnet-agent-harness-hooks | developer-experience | rulesync | intermediate |
| 16 | dotnet-agent-harness-manifest | developer-experience | rulesync | intermediate |
| 17 | dotnet-agent-harness-offline | developer-experience | rulesync | intermediate |
| 18 | dotnet-agent-harness-recommender | developer-experience | rulesync | intermediate |
| 19 | dotnet-agent-harness-test-framework | developer-experience | rulesync | advanced |
| 20 | dotnet-api-design | web | api-design | advanced |
| 21 | dotnet-api-docs | web | docs | intermediate |
| 22 | dotnet-api-security | web | security | advanced |
| 23 | dotnet-api-surface-validation | operations | ci-cd | advanced |
| 24 | dotnet-api-versioning | web | api-design | intermediate |
| 25 | dotnet-aot-architecture | fundamentals | aot | advanced |
| 26 | dotnet-aot-wasm | ui-frameworks | aot | advanced |
| 27 | dotnet-architecture-patterns | architecture | patterns | intermediate |
| 28 | dotnet-artifacts-output | operations | ci-cd | intermediate |
| 29 | dotnet-aspire-patterns | architecture | patterns | advanced |
| 30 | dotnet-background-services | architecture | patterns | intermediate |
| 31 | dotnet-benchmarkdotnet | performance | benchmarking | intermediate |
| 32 | dotnet-blazor-auth | ui-frameworks | blazor | intermediate |
| 33 | dotnet-blazor-components | ui-frameworks | blazor | intermediate |
| 34 | dotnet-blazor-patterns | ui-frameworks | blazor | intermediate |
| 35 | dotnet-blazor-testing | testing | testing | intermediate |
| 36 | dotnet-build-analysis | operations | ci-cd | intermediate |
| 37 | dotnet-build-optimization | operations | ci-cd | intermediate |
| 38 | dotnet-channels | fundamentals | messaging | advanced |
| 39 | dotnet-ci-benchmarking | performance | benchmarking | advanced |
| 40 | dotnet-cli-architecture | developer-experience | cli | intermediate |
| 41 | dotnet-cli-distribution | developer-experience | cli | intermediate |
| 42 | dotnet-cli-packaging | developer-experience | cli | intermediate |
| 43 | dotnet-cli-release-pipeline | developer-experience | cli | advanced |
| 44 | dotnet-container-deployment | web | deployment | advanced |
| 45 | dotnet-containers | web | containers | intermediate |
| 46 | dotnet-cryptography | security | crypto | advanced |
| 47 | dotnet-csharp-api-design | fundamentals | design-principles | advanced |
| 48 | dotnet-csharp-async-patterns | fundamentals | language-patterns | intermediate |
| 49 | dotnet-csharp-code-smells | fundamentals | diagnostics | intermediate |
| 50 | dotnet-csharp-coding-standards | fundamentals | coding-standards | beginner |
| 51 | dotnet-csharp-concurrency-patterns | fundamentals | language-patterns | advanced |
| 52 | dotnet-csharp-configuration | fundamentals | configuration | intermediate |
| 53 | dotnet-csharp-dependency-injection | fundamentals | dependency-injection | intermediate |
| 54 | dotnet-csharp-modern-patterns | fundamentals | language-patterns | intermediate |
| 55 | dotnet-csharp-nullable-reference-types | fundamentals | coding-standards | intermediate |
| 56 | dotnet-csharp-source-generators | fundamentals | language-patterns | advanced |
| 57 | dotnet-csharp-type-design-performance | fundamentals | design-principles | advanced |
| 58 | dotnet-data-access-strategy | data | data-access | intermediate |
| 59 | dotnet-domain-modeling | architecture | domain-modeling | advanced |
| 60 | dotnet-efcore-architecture | data | ef-core | advanced |
| 61 | dotnet-efcore-patterns | data | ef-core | intermediate |
| 62 | dotnet-editorconfig | developer-experience | tooling | intermediate |
| 63 | dotnet-file-based-apps | fundamentals | language-patterns | intermediate |
| 64 | dotnet-file-io | fundamentals | fundamentals | intermediate |
| 65 | dotnet-gha-build-test | operations | github-actions | intermediate |
| 66 | dotnet-gha-deploy | operations | github-actions | intermediate |
| 67 | dotnet-gha-patterns | operations | github-actions | intermediate |
| 68 | dotnet-gha-publish | operations | github-actions | intermediate |
| 69 | dotnet-github-docs | operations | release | beginner |
| 70 | dotnet-github-releases | operations | release | intermediate |
| 71 | dotnet-grpc | web | patterns | intermediate |
| 72 | dotnet-gc-memory | fundamentals | memory | advanced |
| 73 | dotnet-http-client | web | web | intermediate |
| 74 | dotnet-input-validation | web | validation | intermediate |
| 75 | dotnet-integration-testing | testing | integration | advanced |
| 76 | dotnet-io-pipelines | fundamentals | fundamentals | advanced |
| 77 | dotnet-library-api-compat | operations | ci-cd | advanced |
| 78 | dotnet-linq-optimization | fundamentals | patterns | intermediate |
| 79 | dotnet-localization | fundamentals | configuration | intermediate |
| 80 | dotnet-maui-aot | ui-frameworks | maui | advanced |
| 81 | dotnet-maui-development | ui-frameworks | maui | intermediate |
| 82 | dotnet-maui-testing | testing | testing | advanced |
| 83 | dotnet-mer | web | patterns | intermediate |
| 84 | dotnet-messaging-patterns | architecture | messaging | advanced |
| 85 | dotnet-mermaid-diagrams | developer-experience | tooling | intermediate |
| 86 | dotnet-microsoft-agent-framework | developer-experience | analyzers | advanced |
| 87 | dotnet-middleware-patterns | web | minimal-apis | intermediate |
| 88 | dotnet-minimal-apis | web | minimal-apis | intermediate |
| 89 | dotnet-modernize | fundamentals | diagnostics | intermediate |
| 90 | dotnet-msbuild-authoring | developer-experience | msbuild | advanced |
| 91 | dotnet-msbuild-tasks | developer-experience | msbuild | advanced |
| 92 | dotnet-msix | ui-frameworks | platforms | intermediate |
| 93 | dotnet-multi-targeting | fundamentals | aot | intermediate |
| 94 | dotnet-native-aot | performance | aot | advanced |
| 95 | dotnet-native-interop | fundamentals | fundamentals | advanced |
| 96 | dotnet-nuget-authoring | developer-experience | nuget | intermediate |
| 97 | dotnet-observability | architecture | patterns | intermediate |
| 98 | dotnet-openapi | web | api-design | intermediate |
| 99 | dotnet-performance-patterns | performance | patterns | advanced |
| 100 | dotnet-playwright | testing | testing | intermediate |
| 101 | dotnet-profiling | performance | profiling | advanced |
| 102 | dotnet-project-analysis | developer-experience | project | beginner |
| 103 | dotnet-project-structure | developer-experience | project | beginner |
| 104 | dotnet-release-management | operations | release | intermediate |
| 105 | dotnet-resilience | architecture | resilience | intermediate |
| 106 | dotnet-roslyn-analyzers | developer-experience | analyzers | advanced |
| 107 | dotnet-security-owasp | security | owasp | advanced |
| 108 | dotnet-serena-analysis-patterns | developer-experience | serena | intermediate |
| 109 | dotnet-serena-code-navigation | developer-experience | serena | intermediate |
| 110 | dotnet-serena-refactoring | developer-experience | serena | intermediate |
| 111 | dotnet-serialization | fundamentals | serialization | intermediate |
| 112 | dotnet-service-communication | architecture | patterns | intermediate |
| 113 | dotnet-snapshot-testing | testing | testing | intermediate |
| 114 | dotnet-solid-principles | fundamentals | design-principles | intermediate |
| 115 | dotnet-solution-navigation | fundamentals | tooling | beginner |
| 116 | dotnet-spectre-console | ui-frameworks | platforms | intermediate |
| 117 | dotnet-structured-logging | architecture | patterns | intermediate |
| 118 | dotnet-system-commandline | developer-experience | cli | intermediate |
| 119 | dotnet-terminal-gui | ui-frameworks | platforms | intermediate |
| 120 | dotnet-test-quality | testing | coverage | advanced |
| 121 | dotnet-testing | testing | overview | beginner |
| 122 | dotnet-testing-advanced | testing | overview | intermediate |
| 123 | dotnet-testing-advanced-aspnet-integration-testing | testing | integration | advanced |
| 124 | dotnet-testing-advanced-aspire-testing | testing | integration | advanced |
| 125 | dotnet-testing-advanced-testcontainers-database | testing | integration | advanced |
| 126 | dotnet-testing-advanced-testcontainers-nosql | testing | integration | advanced |
| 127 | dotnet-testing-advanced-tunit-advanced | testing | frameworks | advanced |
| 128 | dotnet-testing-advanced-tunit-fundamentals | testing | frameworks | intermediate |
| 129 | dotnet-testing-advanced-webapi-integration-testing | testing | integration | advanced |
| 130 | dotnet-testing-advanced-xunit-upgrade-guide | testing | frameworks | intermediate |
| 131 | dotnet-testing-async-patterns | fundamentals | language-patterns | intermediate |
| 132 | dotnet-testing-autodata-xunit-integration | testing | test-data | intermediate |
| 133 | dotnet-testing-autofixture-basics | testing | test-data | beginner |
| 134 | dotnet-testing-autofixture-bogus-integration | testing | test-data | intermediate |
| 135 | dotnet-testing-autofixture-customization | testing | test-data | intermediate |
| 136 | dotnet-testing-autofixture-nsubstitute-integration | testing | mocking | intermediate |
| 137 | dotnet-testing-awesome-assertions-guide | testing | assertions | beginner |
| 138 | dotnet-testing-bogus-fake-data | testing | test-data | beginner |
| 139 | dotnet-testing-code-coverage-analysis | testing | coverage | intermediate |
| 140 | dotnet-testing-complex-object-comparison | testing | assertions | intermediate |
| 141 | dotnet-testing-datetime-testing-timeprovider | testing | specialized | intermediate |
| 142 | dotnet-testing-filesystem-testing-abstractions | testing | specialized | intermediate |
| 143 | dotnet-testing-fluentvalidation-testing | testing | assertions | intermediate |
| 144 | dotnet-testing-fundamentals | testing | fundamentals | beginner |
| 145 | dotnet-testing-integration | testing | integration | advanced |
| 146 | dotnet-testing-nsubstitute-mocking | testing | mocking | intermediate |
| 147 | dotnet-testing-private-internal-testing | testing | specialized | advanced |
| 148 | dotnet-testing-strategy | testing | overview | intermediate |
| 149 | dotnet-testing-test-data-builder-pattern | testing | test-data | intermediate |
| 150 | dotnet-testing-test-naming-conventions | testing | fundamentals | beginner |
| 151 | dotnet-testing-test-output-logging | testing | fundamentals | intermediate |
| 152 | dotnet-testing-unit-test-fundamentals | testing | fundamentals | beginner |
| 153 | dotnet-testing-xunit-project-setup | testing | fundamentals | beginner |
| 154 | dotnet-trimming | performance | aot | intermediate |
| 155 | dotnet-ui-chooser | ui-frameworks | platforms | beginner |
| 156 | dotnet-ui-testing-core | testing | testing | intermediate |
| 157 | dotnet-uno-mcp | ui-frameworks | uno | intermediate |
| 158 | dotnet-uno-platform | ui-frameworks | uno | intermediate |
| 159 | dotnet-uno-targets | ui-frameworks | uno | intermediate |
| 160 | dotnet-uno-testing | testing | testing | advanced |
| 161 | dotnet-validation-patterns | fundamentals | configuration | intermediate |
| 162 | dotnet-version-detection | fundamentals | tooling | beginner |
| 163 | dotnet-version-upgrade | developer-experience | project | intermediate |
| 164 | dotnet-winforms-basics | ui-frameworks | winforms | beginner |
| 165 | dotnet-winui | ui-frameworks | winui | intermediate |
| 166 | dotnet-wpf-migration | ui-frameworks | wpf | intermediate |
| 167 | dotnet-wpf-modern | ui-frameworks | wpf | intermediate |
| 168 | dotnet-xml-docs | developer-experience | docs | intermediate |
| 169 | dotnet-xunit | testing | frameworks | intermediate |
| 170 | github | developer-experience | analyzers | intermediate |
| 171 | mcp-discovery | developer-experience | analyzers | intermediate |
| 172 | mcp-health | developer-experience | analyzers | intermediate |
| 173 | microsoft-learn-mcp | developer-experience | analyzers | intermediate |
| 174 | rulesync | developer-experience | rulesync | intermediate |
| 175 | serena | developer-experience | serena | intermediate |
| 176 | wiki-ado-convert | developer-experience | docs | intermediate |
| 177 | wiki-agents-md | developer-experience | docs | intermediate |
| 178 | wiki-architect | developer-experience | docs | intermediate |
| 179 | wiki-changelog | developer-experience | docs | intermediate |
| 180 | wiki-llms-txt | developer-experience | docs | intermediate |
| 181 | wiki-onboarding | developer-experience | docs | intermediate |
| 182 | wiki-page-writer | developer-experience | docs | intermediate |
| 183 | wiki-qa | developer-experience | docs | intermediate |
| 184 | wiki-researcher | developer-experience | docs | intermediate |
| 185 | wiki-vitepress | developer-experience | docs | intermediate |

</details>

---

## By Target Platform

Platform compatibility matrix for all skills.

| Skill | claudecode | opencode | codexcli | copilot | geminicli | antigravity | factorydroid |
|-------|:----------:|:--------:|:--------:|:-------:|:---------:|:-----------:|:------------:|
| dotnet-testing | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| dotnet-csharp-coding-standards | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| dotnet-architecture-patterns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| dotnet-performance-patterns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| dotnet-security-owasp | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| *All skills target: ['*']* | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

> **Note**: Most skills use `targets: ['*']` for universal compatibility.

---

## Category Meta-Skills Quick Reference

High-level navigation skills for category discovery:

| Meta-Skill | Category | Skills Referenced | Purpose |
|------------|----------|-------------------|---------|
| dotnet-fundamentals | fundamentals | ~25 skills | Core language and runtime |
| dotnet-testing | testing | ~35 skills | Testing methodology |
| dotnet-architecture | architecture | ~15 skills | Design patterns |
| dotnet-performance | performance | ~10 skills | Optimization |
| dotnet-security | security | ~10 skills | Security and hardening |

---

## Update Log

| Date | Change | Author |
|------|--------|--------|
| 2026-03-07 | Initial taxonomy with 189 skills | dotnet-agent-harness |
| 2026-03-07 | Renamed categories: devops→operations, platforms→ui-frameworks, tooling→developer-experience | dotnet-agent-harness |

---

## Contributing

To add a new skill to the index:

1. Create the skill in appropriate category folder
2. Add frontmatter with `category`, `subcategory`, `complexity`, `tags`
3. Update this INDEX.md with the skill entry
4. Update category meta-skill if needed
5. Verify cross-references are correct
