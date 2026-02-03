# Learnings - Browser-First Strategy Implementation

## Implementation Pattern

### Strategy Enum Design
- Created `JobSearchStrategy` enum with four options: `HttpFirst`, `BrowserFirst`, `HttpOnly`, `BrowserOnly`
- Default set to `BrowserFirst` for better reliability (following LinkedIn's pattern)
- Each strategy is self-documenting with XML comments

### Backward Compatibility
- Maintained `UseBrowserFallback` property as obsolete for backward compatibility
- Property getter/setter automatically converts between old and new configuration
- Users can still use old configuration without breaking changes

### Strategy Implementation Pattern
- Main `SearchJobsAsync` method uses switch expression to route to appropriate strategy method
- Each strategy method is isolated: `TryBrowserFirstAsync`, `TryHttpFirstAsync`, `TryBrowserOnlyAsync`, `TryHttpOnlyAsync`
- Strategy methods handle their own error catching and fallback logic
- Clean separation of concerns makes code maintainable

### Logging Pattern
- Added comprehensive logging with `LoggerMessage.Define` for performance
- Four new log messages:
  - `s_logStrategyAttempt`: Logs which strategy is being attempted
  - `s_logStrategySuccess`: Logs successful strategy with job count
  - `s_logStrategyFailed`: Logs strategy failure and fallback attempt
  - `s_logStrategyNoFallback`: Logs when no fallback is available

### Fallback Logic
- `BrowserFirst`: Try browser → if fails/no results → try HTTP
- `HttpFirst`: Try HTTP → if fails/no results → try browser
- `BrowserOnly`: Try browser only, no fallback
- `HttpOnly`: Try HTTP only, no fallback

### Error Handling
- `OperationCanceledException` is re-thrown (don't catch cancellation)
- All other exceptions are caught and trigger fallback
- Empty results are treated as failure (trigger fallback)
- Graceful degradation when browser client is null

## Configuration Usage

```json
{
  "Ghost": {
    "Extensions": {
      "Google": {
        "Strategy": "BrowserFirst"
      }
    }
  }
}
```

Or via environment variable:
```bash
GHOST__EXTENSIONS__GOOGLE__STRATEGY=BrowserFirst
```

## Success Criteria Met
✅ Browser automation attempted first (default)
✅ HTTP API used as fallback if browser fails
✅ Configuration option allows strategy control
✅ Detailed logging shows strategy progression
✅ Code builds without errors
✅ Existing functionality preserved
