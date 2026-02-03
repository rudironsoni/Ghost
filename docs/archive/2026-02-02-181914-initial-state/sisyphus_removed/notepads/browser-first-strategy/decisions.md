# Decisions - Browser-First Strategy Implementation

## Architectural Decisions

### 1. Default Strategy: BrowserFirst
**Decision**: Set default strategy to `BrowserFirst` instead of `HttpFirst`

**Rationale**:
- LinkedIn uses browser-first and has proven more reliable
- Browser automation is more resilient to API changes
- HTTP APIs can be blocked or rate-limited more easily
- Aligns with the task requirement to follow LinkedIn's successful pattern

### 2. Strategy Enum vs Boolean Flags
**Decision**: Use enum with four values instead of multiple boolean flags

**Rationale**:
- More explicit and self-documenting
- Prevents invalid combinations (e.g., both `UseBrowser` and `UseHttp` set to false)
- Easier to extend with new strategies in the future
- Cleaner switch expression in implementation

### 3. Separate Strategy Methods
**Decision**: Create separate methods for each strategy (`TryBrowserFirstAsync`, etc.)

**Rationale**:
- Each strategy has its own logic and fallback behavior
- Easier to test individual strategies
- Better separation of concerns
- More maintainable than nested if/else statements

### 4. Backward Compatibility Approach
**Decision**: Keep `UseBrowserFallback` as obsolete property with automatic conversion

**Rationale**:
- Existing users won't experience breaking changes
- Property getter/setter handles conversion transparently
- Clear deprecation message guides users to new property
- Can remove in future major version when appropriate

### 5. Logging Strategy
**Decision**: Use `LoggerMessage.Define` for high-performance logging

**Rationale**:
- More efficient than string interpolation in hot paths
- Structured logging with event IDs for filtering
- Consistent with existing logging patterns in the codebase
- Enables better observability and debugging

### 6. Empty Results as Failure
**Decision**: Treat empty results as failure and trigger fallback

**Rationale**:
- Empty results may indicate API issues or rate limiting
- Provides better user experience by trying alternative method
- Consistent with original implementation behavior
- Users get results if available from any method

## Configuration Design

### Strategy Property
- Type: `JobSearchStrategy` enum
- Default: `JobSearchStrategy.BrowserFirst`
- Configurable via appsettings.json or environment variables

### Obsolete Property
- `UseBrowserFallback` marked with `[Obsolete]` attribute
- Automatic conversion to/from `Strategy` property
- Maintains backward compatibility without breaking changes
