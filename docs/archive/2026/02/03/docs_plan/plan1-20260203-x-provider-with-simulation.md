# Implementation Plan: Ghost.Platform.X with First-Class Simulation

**Date:** 2026-02-03  
**Objective:** Implement X (Twitter) social media provider with reusable simulation infrastructure

## Overview

This plan implements a complete X (formerly Twitter) social media provider for Ghost, including thread support, video uploads, cookie-based authentication, and a first-class simulation framework that can be reused across all social platforms.

## Architecture

### Core Components

1. **Ghost.Contracts.Simulation** - Reusable simulation abstractions
2. **Ghost.Platform.X** - X (Twitter) provider implementation
3. **Ghost.Platform.X.Tests** - Comprehensive test suite (80%+ coverage)

### Design Decisions

- **Simulation Mode:** First-class citizen at the framework level, not coupled to X
- **Thread Support:** Automatic content splitting and reply chaining for multi-tweet posts
- **Media Support:** Images (up to 4) and video (1) per tweet
- **Authentication:** Cookie-based using Playwright storage state persistence
- **Browser Automation:** Based on established LinkedIn pattern

## Phase 1: Simulation Contracts (Ghost.Contracts.Simulation)

### Files

```
src/Contracts/Ghost.Contracts.Simulation/
├── Ghost.Contracts.Simulation.csproj
├── ISocialSimulationService.cs      # Core simulation interface
├── SimulationResult.cs               # Result of simulation
├── SimulationRecord.cs               # Record of simulated action
├── SimulationOptions.cs              # Configuration options
└── IXPlatformSimulationValidator.cs  # Platform-specific validator interface
```

### Features

- **Global Simulation Mode:** Toggle via configuration
- **Action Recording:** Complete audit trail of what would be executed
- **Content Validation:** Platform-specific rules (char limits, media counts)
- **Screenshot Capture:** Visual evidence at each step
- **Decorator Pattern:** Transparent wrapping of ISocialClient implementations

## Phase 2: X Provider Implementation (Ghost.Platform.X)

### Project Structure

```
src/Platforms/Ghost.Platform.X/
├── Ghost.Platform.X.csproj
├── XOptions.cs                       # Configuration (base URL, limits, auth)
├── XExtension.cs                     # DI registration and extension setup
├── XSocialClient.cs                  # Main ISocialClient implementation
└── Internal/
    ├── XAuthenticator.cs            # Cookie-based authentication
    ├── XPostContentSplitter.cs      # Thread content splitting (280 char limit)
    ├── XThreadComposer.cs           # Multi-tweet composition and posting
    └── XSimulationValidator.cs      # X-specific validation rules
```

### Components

#### XOptions
- BaseUrl: https://x.com
- MaxTweetLength: 280
- MaxMediaAttachments: 4 (images) or 1 (video)
- StorageStatePath: Path to cookie persistence file
- WarmUpEnabled, ProxyEnabled: Session management

#### XAuthenticator
- Load/save storage state for cookie persistence
- IsLoggedInAsync() verification
- EnsureAuthenticatedAsync() with retry logic
- WarmUpAsync() for session pre-heating

#### XPostContentSplitter
- Split at sentence boundaries
- Respect URL boundaries (don't break URLs)
- Handle threads exceeding 280 chars
- Context-aware splitting (e.g., "(1/3)")

#### XThreadComposer
- Single tweet or multi-tweet thread support
- Media upload automation (images via file input, video support)
- Reply chaining: extract tweet ID → reply → repeat
- Rate limit awareness with retry logic
- Wait for success indicators

#### XSocialClient
Implements all 7 ISocialClient methods:
1. **GetProfileAsync** - Navigate /{username}, extract profile data
2. **CreatePostAsync** - Use XThreadComposer with simulation support
3. **GetFeedAsync** - Parse /home timeline
4. **SearchProfilesAsync** - Use /search?q={query}&f=user
5. **SendMessageAsync** - Navigate /messages/compose
6. **GetConnectionsAsync** - Parse /following or /followers
7. **SendConnectionRequestAsync** - Click Follow button

## Phase 3: Testing (Ghost.Platform.X.Tests)

### Test Coverage Requirements

- **Minimum 80% code coverage**
- Unit tests for all public methods
- Integration tests with mocked browser
- Simulation mode validation
- Error handling and edge cases

### Test Structure

```
tests/Platforms/Ghost.Platform.X.Tests/
├── Ghost.Platform.X.Tests.csproj
├── XOptionsTests.cs
├── XPostContentSplitterTests.cs
├── XThreadComposerTests.cs
├── XSocialClientTests.cs
├── XExtensionTests.cs
└── XSimulationValidatorTests.cs
```

## Phase 4: Solution Integration

### Projects to Add

1. **Ghost.Contracts.Simulation** - src/Contracts/
2. **Ghost.Platform.X** - src/Platforms/
3. **Ghost.Platform.X.Tests** - tests/Platforms/

### Dependencies

```
Ghost.Contracts.Simulation → (none, pure contracts)
Ghost.Platform.X → Ghost.Contracts.Social
                → Ghost.Contracts.Simulation
                → Ghost (Core)
Ghost.Platform.X.Tests → Ghost.Platform.X
                      → xUnit
                      → Moq
                      → Coverlet
```

## Configuration

### appsettings.json Example

```json
{
  "Simulation": {
    "Enabled": true,
    "CaptureScreenshots": true,
    "ValidateSelectors": true,
    "ScreenshotDirectory": "/tmp/simulations",
    "MaxRecordedActions": 1000,
    "WriteToDisk": true,
    "SimulationLogPath": "/tmp/simulation.log"
  },
  "X": {
    "BaseUrl": "https://x.com",
    "PageLoadTimeout": 30,
    "StorageStatePath": "/data/x-auth.json",
    "WarmUpEnabled": true,
    "ProxyEnabled": false
  }
}
```

## Browser Selectors (X-Specific)

Key selectors for browser automation:
- Composer: `div[role='textbox'][contenteditable='true']`
- Post button: `button[data-testid='tweetButton']`
- Media input: `input[type='file']`
- Profile name: `h1[data-testid='UserName']`
- Timeline: `article[data-testid='tweet']`
- Reply button: `button[data-testid='reply']`

## Success Criteria

1. ✅ All projects build successfully
2. ✅ Unit tests pass with 80%+ coverage
3. ✅ X provider implements all ISocialClient methods
4. ✅ Thread support works for content >280 chars
5. ✅ Video upload support functional
6. ✅ Cookie-based authentication persists sessions
7. ✅ Simulation mode works globally across all platforms
8. ✅ Screenshot capture and validation in simulation mode

## Future Enhancements (Deferred)

- Cross-platform posting (simultaneous X + LinkedIn)
- Analytics tracking and metrics
- Draft management service
- Rate limiting with automatic backoff
- Multi-account support

---

**Status:** Ready for implementation
**Estimated Effort:** 2-3 days
**Dependencies:** None (all new code)
