# SessionFactory 2.0 Implementation Plan

## Context So Far

We have completed:
1. **Task 1**: Implemented Tiered Browser Pool Manager with Hot/Warm/Cold tiers
2. **Task 2**: Enhanced Stealth Matrix with advanced canvas fingerprint obfuscation

Now we're working on **Task 3**: Build SessionFactory 2.0 with Orchestration

## Current State Analysis

### Existing Session Infrastructure
- `SessionFactory.cs` - Basic factory for RotatingProxySession instances
- `RotatingProxySession.cs` - HTTP session with proxy rotation and retry logic
- `RotatingProxySessionOptions.cs` - Configuration options for sessions

### Dependencies Available
- Tiered browser pool manager (Hot/Warm/Cold tiers)
- Enhanced stealth scripts with deterministic seeding
- Proxy provider infrastructure

## Task 3 Requirements

### What to Build
Create `ISessionOrchestrator` interface and implementation with:
1. Context-aware session allocation
2. Geographic proxy routing
3. Complexity-based fingerprint matching
4. Session health monitoring with automatic recovery
5. Session persistence across requests
6. Session affinity (sticky sessions)
7. Session TTL and automatic rotation

### Integration Points
- Use existing `GhostKernel` for browser session creation
- Integrate with tiered browser pool
- Use existing proxy provider infrastructure
- Maintain backward compatibility

### Files to Create/Modify
- `src/Platforms/Ghost.Platform.Common/Session/ISessionOrchestrator.cs`
- `src/Platforms/Ghost.Platform.Common/Session/SessionOrchestrator.cs`
- `src/Platforms/Ghost.Platform.Common/Session/SessionOrchestratorOptions.cs`
- Possibly extend existing SessionFactory

## Next Steps After Task 3
- Task 4: Integrate SessionFactory into Platforms (Indeed, Glassdoor, Google Jobs)
- Task 5: Create DotnetSpider Integration Layer
- Task 6: Define Entity Models for All Platforms

## Key Design Decisions for SessionFactory 2.0

1. **Session Types**: Support both HTTP sessions (RotatingProxySession) and Browser sessions (from tiered pool)
2. **Orchestration**: Centralized session management with health monitoring
3. **Backward Compatibility**: Extend existing SessionFactory rather than replace
4. **Resource Management**: Proper cleanup and disposal patterns
5. **Configuration**: Flexible options system

## Technical Requirements

### Must Have
- Interface-based design (`ISessionOrchestrator`)
- Context-aware allocation (geographic, complexity-based)
- Health monitoring with automatic recovery
- Session persistence and affinity
- Proper async disposal patterns

### Must NOT Do
- Break existing SessionFactory API
- Create sessions without proper cleanup
- Ignore session health failures
- Allow session leaks

## Implementation Approach

1. Create new orchestrator interface and implementation
2. Extend existing SessionFactory to use orchestrator
3. Add session health monitoring capabilities
4. Implement context-aware allocation logic
5. Add geographic routing and complexity matching
6. Ensure proper resource management