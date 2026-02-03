# Ghost.Platform.X

X (formerly Twitter) platform integration for Ghost with comprehensive browser automation, thread support, video uploads, and first-class simulation capabilities.

## Features

- **Browser Automation** - Full browser-based interaction using Playwright
- **Thread Support** - Automatic splitting of long content into tweet threads
- **Media Support** - Upload images (up to 4) and videos (1 per tweet)
- **Cookie-Based Auth** - Persistent authentication using storage state
- **First-Class Simulation** - Test mode with validation, previews, and screenshots
- **Rate Limit Protection** - Built-in throttling and retry logic

## Quick Start

### 1. Installation

```bash
dotnet add package Ghost.Platform.X
```

### 2. Configuration

Add to your `appsettings.json`:

```json
{
  "X": {
    "BaseUrl": "https://x.com",
    "PageLoadTimeout": 30,
    "StorageStatePath": "/path/to/x-auth.json",
    "WarmUpEnabled": true,
    "MaxRetries": 3,
    "ThreadDelayMs": 2000
  }
}
```

### 3. Register Services

```csharp
services.AddXPlatform();
```

Or with custom configuration:

```csharp
services.AddXPlatform(options =>
{
    options.BaseUrl = "https://x.com";
    options.StorageStatePath = "/data/x-auth.json";
});
```

### 4. Authenticate

**Option A: Using existing browser session**

1. Log into X.com in your regular browser
2. Extract cookies to a storage state file
3. Set `StorageStatePath` in configuration

**Option B: Manual authentication (one-time)**

```csharp
var session = await browserSession.NewPageAsync();
var authenticator = serviceProvider.GetRequiredService<XAuthenticator>();
// Navigate to X.com and manually log in
await authenticator.SaveAuthenticationStateAsync();
```

### 5. Post Content

```csharp
var client = serviceProvider.GetRequiredService<ISocialClient>();

// Single tweet
await client.CreatePostAsync(new CreatePostRequest
{
    Content = "Hello from Ghost!"
});

// Thread (auto-split if > 280 chars)
await client.CreatePostAsync(new CreatePostRequest
{
    Content = "Long content that will be automatically split into multiple tweets...",
    MediaUrls = new[] { "/path/to/image.jpg" }
});
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BaseUrl` | string | "https://x.com" | X platform base URL |
| `PageLoadTimeout` | int | 30 | Page load timeout in seconds |
| `StorageStatePath` | string | null | Path to cookie storage file |
| `WarmUpEnabled` | bool | true | Pre-heat session on startup |
| `ProxyEnabled` | bool | false | Enable proxy support |
| `MaxRetries` | int | 3 | Max retry attempts for failed operations |
| `RetryDelayMs` | int | 5000 | Delay between retries in milliseconds |
| `ThreadDelayMs` | int | 2000 | Delay between tweets in a thread |
| `MaxTweetLength` | int | 280 | Maximum characters per tweet (read-only) |
| `MaxMediaAttachments` | int | 4 | Maximum images per tweet (read-only) |

## Simulation Mode

Test without actually posting to X:

```csharp
// Configure simulation mode
services.Configure<SimulationOptions>(options =>
{
    options.Enabled = true;
    options.CaptureScreenshots = true;
    options.ValidateSelectors = true;
});

// Get simulation service
var simulator = serviceProvider.GetRequiredService<ISocialSimulationService>();

// Simulate post
var result = await simulator.SimulatePostAsync(request, "X");

if (result.WouldSucceed)
{
    Console.WriteLine($"Post would succeed with ID: {result.SimulatedPostId}");
    Console.WriteLine($"Preview HTML: {result.PreviewHtml}");
}
```

## Error Handling

The provider uses specific exception types:

- `XAuthenticationException` - Authentication failures
- `XRateLimitException` - Rate limit exceeded
- `XValidationException` - Content validation errors
- `XBrowserException` - Browser automation errors

```csharp
try
{
    await client.CreatePostAsync(request);
}
catch (XRateLimitException ex)
{
    Console.WriteLine($"Rate limited. Retry after: {ex.RetryAfter}");
}
catch (XAuthenticationException ex)
{
    Console.WriteLine($"Authentication failed: {ex.Message}");
}
```

## Thread Support

Long content is automatically split into threads:

```csharp
var longContent = @"This is a very long post that will be automatically 
    split into multiple tweets. Each tweet will be numbered (1/N, 2/N, etc.)
    and will respect sentence boundaries to avoid breaking mid-sentence.";

await client.CreatePostAsync(new CreatePostRequest
{
    Content = longContent
});
// Creates: "... (1/3)", "... (2/3)", "... (3/3)"
```

## Media Upload

```csharp
// Single image
await client.CreatePostAsync(new CreatePostRequest
{
    Content = "Check out this image!",
    MediaUrls = new[] { "/path/to/image.jpg" }
});

// Multiple images (up to 4)
await client.CreatePostAsync(new CreatePostRequest
{
    Content = "Photo gallery",
    MediaUrls = new[] { "/path/1.jpg", "/path/2.jpg", "/path/3.jpg", "/path/4.jpg" }
});

// Video
await client.CreatePostAsync(new CreatePostRequest
{
    Content = "Watch this video!",
    MediaUrls = new[] { "/path/to/video.mp4" }
});
```

## Troubleshooting

### "Not authenticated to X"

**Cause:** No valid authentication state found  
**Solution:** 
1. Log into X.com in a browser
2. Extract cookies to storage state file
3. Verify `StorageStatePath` points to valid file

### "Rate limited"

**Cause:** X has rate limiting (300 tweets per 3 hours)  
**Solution:**
- Wait for rate limit to reset
- Implement retry logic with exponential backoff
- Use `XRateLimitException.RetryAfter` property

### "Could not find compose text box"

**Cause:** X DOM structure changed or anti-bot detection  
**Solution:**
- Update selectors in `XOptions`
- Enable stealth mode
- Check if X requires CAPTCHA

### "Media upload failed"

**Cause:** File too large or unsupported format  
**Solution:**
- Verify file exists: `File.Exists(path)`
- Check file size: Images max 5MB, Videos max 512MB
- Verify format: `.jpg`, `.png`, `.gif`, `.webp`, `.mp4`, `.mov`, `.webm`

### Thread numbering shows (1/1) instead of (1/N)

**Cause:** Content fits in single tweet  
**Solution:** This is expected behavior - only multi-tweet content gets numbering

## Advanced Usage

### Custom Content Splitting

```csharp
var splitter = serviceProvider.GetRequiredService<XPostContentSplitter>();
var parts = splitter.Split(longContent);

foreach (var (part, index) in parts.Select((p, i) => (p, i)))
{
    Console.WriteLine($"Tweet {index + 1}: {part}");
}
```

### Profile Operations

```csharp
// Get profile
var profile = await client.GetProfileAsync("username");

// Search profiles
var results = await client.SearchProfilesAsync(new ProfileSearchCriteria
{
    Query = "ghost",
    MaxResults = 10
});

// Follow user
await client.SendConnectionRequestAsync("username");

// Get followers
var connections = await client.GetConnectionsAsync(new ConnectionsOptions
{
    ProfileId = "username",
    MaxResults = 50
});
```

### Send Direct Messages

```csharp
await client.SendMessageAsync("recipient_username", "Hello!");
```

## License

MIT License - See LICENSE file for details
