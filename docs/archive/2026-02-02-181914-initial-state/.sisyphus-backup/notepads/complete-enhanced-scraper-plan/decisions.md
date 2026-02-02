
## Abstract Proxy Configuration System - Design Decisions (2025-02-01)

### 1. Namespace Location: `Ghost.ProxyConfiguration`

**Decision**: Place new configuration classes in `Ghost.ProxyConfiguration` namespace, separate from `Ghost.Core`.

**Rationale**:
- `Ghost.Core` contains fundamental options (KernelOptions, SessionOptions)
- `Ghost.ProxyConfiguration` separates proxy-specific abstractions from core kernel options
- Mirrors existing pattern where specialized features get dedicated namespaces (Ghost.Services, Ghost.Abstractions, Ghost.Http)
- Allows future proxy feature additions (factory, health management, geographic targeting) in same namespace

**Impact**: 
- Requires `using Ghost.ProxyConfiguration;` in client code
- Clean separation of concerns
- Foundation for Task 10 (health intelligence) and Task 11 (geographic targeting) which will also live in same namespace

### 2. ProxySourceConfig in ProxyConfiguration Namespace

**Decision**: Create new `ProxySourceConfig` in `Ghost.ProxyConfiguration` namespace while keeping existing `Ghost.Core.ProxySourceConfig` unchanged.

**Rationale**:
- Existing `Ghost.Core.ProxySourceConfig` is used by `StaticProxySource` and likely other services
- Changing or moving it would be a breaking change
- New `ProxySourceConfig` enables advanced configuration without affecting legacy code
- Allows dual-use pattern: legacy code uses old version, new code uses new version

**Impact**:
- Two similar classes exist with same name in different namespaces
- Clear upgrade path for legacy code (use `Ghost.ProxyConfiguration.ProxySourceConfig` for new features)
- No breaking changes to existing system

### 3. Separate FallbackChain Property

**Decision**: Create separate `FallbackChain` property in `ProxySystemOptions` instead of reusing `Sources`.

**Rationale**:
- Enables graceful degradation strategy (primary → fallback)
- Makes configuration intent explicit (which proxies are primary vs fallback)
- Allows different handling logic (e.g., fallback sources not rotated until primary exhausted)
- Separates concerns: sources are configured proxies, fallback chain is degradation strategy

**Impact**:
- Developers must explicitly configure both Sources and FallbackChain
- Clear semantic meaning in configuration
- Enables sophisticated routing strategies in Task 10

### 4. RotationStrategy as String Enum

**Decision**: Use `string` property for rotation strategy instead of strict enum.

**Rationale**:
- Provider-agnostic: allows any strategy name without modifying enum
- Extensible: new strategies can be added without code changes
- Configuration-driven: strategies can be added at runtime or via config files
- Matches existing Ghost pattern: `ProxyOptions.Strategy` in Core uses string

**Impact**:
- No type safety for strategy names
- Documentation must explain valid strategy values
- Requires factory/validator in Task 9 to validate strategy implementations

### 5. HealthCheckIntervalSeconds = 300 Default

**Decision**: Default health check interval to 300 seconds (5 minutes).

**Rationale**:
- Balances between responsiveness and overhead
- Not too aggressive (prevents hammering proxy servers)
- Not too relaxed (detects failures within reasonable time)
- Industry standard for health checking (comparable to AWS ELB)
- Can be disabled by setting to 0 (enables zero-overhead mode)

**Impact**:
- Task 10 (proxy health) must respect this interval
- Enables background health check sweep without additional configuration

### 6. ProxySourceConfig Type Property Optional

**Decision**: Make `Type` property nullable string (optional).

**Rationale**:
- Some sources may be auto-discovered (type inferred from URL or host)
- Configuration backwards compatible (Type can be omitted)
- Allows future auto-detection without breaking existing configs
- Nullable clearly indicates optional nature in API documentation

**Impact**:
- Factory (Task 9) must handle null Type gracefully
- Could infer from other properties (e.g., Url presence → Api type)

### 7. Enabled Property Default: true

**Decision**: Default `Enabled` to `true` for ProxySourceConfig.

**Rationale**:
- Principle of least surprise: configured sources are active by default
- Allows toggling without removing configuration
- Simpler than null checks (no "optional source" concept)
- Consistent with existing `Ghost.Core.ProxySourceConfig` pattern

**Impact**:
- Disabled sources remain in configuration (useful for staging/disabling without deletion)
- Task 9 factory must check Enabled flag before processing

### 8. Separate Username/Password Properties

**Decision**: Keep `Username` and `Password` as separate nullable properties instead of combined auth object.

**Rationale**:
- Simpler configuration (flat structure)
- Matches standard Basic Auth pattern (username:password)
- Easier to configure via environment variables
- Some proxy providers use only one (e.g., token-based auth in Url)

**Impact**:
- Consumer must pair Username with Password manually
- No type safety ensuring password only when username exists
- Validation logic in Task 10 can enforce pairing if needed


## ProxyHealthIntelligence Design Decisions

### Namespace Choice
- **Decision**: Placed in Ghost.ProxyManagement namespace
- **Rationale**: Separates health intelligence from configuration concerns (ProxyConfiguration)
- **Alternative**: Could have been in Ghost.Services with other proxy implementations
- **Trade-off**: Creates new namespace but maintains clear separation of concerns

### Health Check Strategy
- **Decision**: Use httpbin.org/ip for health checks
- **Rationale**: Lightweight, reliable, HTTPS endpoint that works globally
- **Alternative**: Could use configurable health check URLs per proxy
- **Trade-off**: Dependency on external service but universally accessible

### Metrics Storage
- **Decision**: In-memory with List<double> for latency history
- **Rationale**: Simple, fast, sufficient for most use cases
- **Concern**: Unbounded growth of LatencyHistory list
- **Mitigation**: Could add max size limit (e.g., last 1000 measurements)

### Blacklist Threshold
- **Decision**: Blacklist after 5 consecutive failures
- **Rationale**: Balance between tolerance and quick failure detection
- **Configuration**: Hardcoded but could be made configurable in future

### Rotation Strategy Selection
- **Decision**: String-based strategy selection with switch expression
- **Rationale**: Simple, readable, easily extensible
- **Alternative**: Strategy pattern with IProxySelectionStrategy interface
- **Trade-off**: Less extensible but simpler for current needs

### Whitelist Priority
- **Decision**: Always return whitelisted proxies first before others
- **Rationale**: Allows admins to force specific proxy usage
- **Behavior**: Even if whitelisted proxy has poor performance, it's still prioritized

### Fallback Activation
- **Decision**: Automatic fallback when healthy proxies exhausted
- **Rationale**: Provides resilience without manual intervention
- **Behavior**: Once fallback activated, never switches back to primary

### Thread Safety Approach
- **Decision**: Mixed approach (ConcurrentDictionary + locks where needed)
- **Rationale**: ConcurrentDictionary for high-contention paths, locks for whitelist
- **Performance**: Optimized for read-heavy workloads (GetProxyAsync)

### Geographic Latency Design
- **Decision**: Placeholder structure with Dictionary<string, List<double>>
- **Rationale**: Foundation ready for future implementation
- **Current**: Not fully implemented, needs country detection mechanism

