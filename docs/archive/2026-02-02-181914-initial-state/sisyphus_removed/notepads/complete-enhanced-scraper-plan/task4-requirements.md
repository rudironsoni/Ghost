# Task 4: Integrate SessionFactory into Platforms - Implementation Plan

## Current State Analysis

### Existing Platform Implementations

1. **IndeedApiClient.cs** (283 lines)
   - Creates HttpClient per request with SocketsHttpHandler
   - Manual proxy handling with WebProxy
   - Rate limiting with SemaphoreSlim
   - Direct IProxyProvider usage
   - No session continuity between requests

2. **GlassdoorApiClient.cs** (705 lines)
   - Uses single HttpClient injected via constructor
   - Enhanced retry policy with Polly
   - Complex CSRF token extraction and validation
   - Manual rate limiting
   - No session continuity between requests

3. **GoogleJobsApiClient.cs** (288 lines)
   - Uses single HttpClient injected via constructor
   - Enhanced retry policy with Polly
   - Manual cookie handling for consent bypass
   - No session continuity between requests

### Session Infrastructure Available

1. **Old SessionFactory.cs** (88 lines)
   - Creates RotatingProxySession instances
   - Platform-specific configuration
   - No browser session support

2. **New SessionOrchestrator** (ISessionOrchestrator interface)
   - Context-aware session allocation
   - HTTP and Browser session support
   - Health monitoring
   - Session affinity
   - Session persistence
   - Proper lifecycle management

## Integration Strategy

### Phase 1: Modify Platform Constructors
- Inject ISessionOrchestrator instead of IProxyProvider/HttpClient
- Maintain backward compatibility with existing constructors
- Add new constructors that use SessionOrchestrator

### Phase 2: Refactor HTTP Request Handling
- Replace direct HttpClient usage with SessionOrchestrator
- Use GetHttpSessionAsync to get RotatingProxySession
- Leverage session continuity for better performance
- Maintain existing retry and rate limiting logic where appropriate

### Phase 3: Add Session Health Monitoring
- Implement session health checks
- Add automatic session recycling on failure
- Add session affinity for consistent user experience

### Phase 4: Browser Session Integration (Future)
- Add support for browser sessions where needed
- Implement complexity-based routing decisions

## Platform-Specific Implementation Details

### Indeed Platform
- Current: Creates new HttpClient per request
- Target: Use shared RotatingProxySession from SessionOrchestrator
- Benefits: Session continuity, better proxy rotation, health monitoring

### Glassdoor Platform
- Current: Uses single HttpClient with manual proxy handling
- Target: Use RotatingProxySession from SessionOrchestrator
- Benefits: Better CSRF token handling through session continuity, improved reliability

### Google Jobs Platform
- Current: Uses single HttpClient with manual cookie handling
- Target: Use RotatingProxySession from SessionOrchestrator
- Benefits: Session continuity for consent bypass, better reliability

## Backward Compatibility Requirements

1. **Existing Constructors**: Must remain unchanged
2. **Public APIs**: Must remain compatible
3. **Configuration**: Must support existing options
4. **Behavior**: Should maintain or improve existing functionality

## Files to Modify

1. `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs`
2. `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs`
3. `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`

## Implementation Approach

### Step 1: Add SessionOrchestrator Support to IndeedApiClient
- Add new constructor with ISessionOrchestrator
- Modify SearchAsync to use SessionOrchestrator
- Maintain existing constructor for backward compatibility

### Step 2: Add SessionOrchestrator Support to GlassdoorApiClient
- Add new constructor with ISessionOrchestrator
- Modify SearchAsync to use SessionOrchestrator
- Maintain existing constructor for backward compatibility

### Step 3: Add SessionOrchestrator Support to GoogleJobsApiClient
- Add new constructor with ISessionOrchestrator
- Modify SearchAsync to use SessionOrchestrator
- Maintain existing constructor for backward compatibility

## Key Design Decisions

1. **Session Continuity**: Use same session for pagination requests
2. **Health Monitoring**: Automatic session recycling on failures
3. **Rate Limiting**: Maintain existing rate limiting but enhance with session awareness
4. **Error Handling**: Leverage SessionOrchestrator health monitoring
5. **Backward Compatibility**: Preserve existing APIs while adding new capabilities