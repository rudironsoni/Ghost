# Patchright Migration Guide

## Overview

Ghost has migrated from **Microsoft.Playwright** to **Patchright** - an undetected fork of Playwright specifically designed for stealth browser automation.

## Why Patchright?

Patchright is a patched version of Playwright that provides superior stealth capabilities:

### Detection Bypasses
- ✅ **Runtime.enable Leak** - Avoids detection by executing JavaScript in isolated execution contexts
- ✅ **Console.enable Leak** - Disabled to prevent console-based detection
- ✅ **Command Flags Leaks** - Tweaked default args to avoid automation detection
- ✅ **Closed Shadow Roots** - Can interact with elements in closed shadow DOMs
- ✅ **navigator.webdriver** - Removed by adding `--disable-blink-features=AutomationControlled`

### Platforms Passed
- ✅ Brotector
- ✅ Cloudflare
- ✅ Kasada
- ✅ Akamai
- ✅ Shape/F5
- ✅ Datadome
- ✅ Fingerprint.com
- ✅ CreepJS
- ✅ Sannysoft
- ✅ Incolumitas
- ✅ IPHey
- ✅ Browserscan
- ✅ Pixelscan

## Migration Details

### Changes Made

1. **Package References Updated**:
   - `Ghost.Sdk.Spider.Tests/Ghost.Sdk.Spider.Tests.csproj`: Changed `Microsoft.Playwright.NUnit` → `Patchright.NUnit`
   - `TestLinkedInScraper/TestLinkedInScraper.csproj`: Changed `Microsoft.Playwright` → `Patchright`

2. **Central Package Management**:
   - `Directory.Packages.props` already included:
     - `Patchright` (v1.58.0)
     - `Patchright.NUnit` (v1.58.0)

3. **No Code Changes Required**:
   - Patchright is a **drop-in replacement**
   - Uses the same `Microsoft.Playwright` namespace
   - All existing code remains unchanged

### Verification

```bash
# Build succeeded with 0 errors, 0 warnings
dotnet build Ghost.sln --configuration Release --no-restore

# No Microsoft.Playwright references found
grep -r "Microsoft\.Playwright" --include="*.csproj" .
# Result: No matches

# Patchright references confirmed
grep -r "Patchright" --include="*.csproj" --include="*.props" .
# Result: 
# - Directory.Packages.props: Patchright v1.58.0
# - Ghost.csproj: PackageReference to Patchright
# - Ghost.Sdk.Spider.csproj: PackageReference to Patchright
# - Ghost.Sdk.Spider.Tests.csproj: PackageReference to Patchright.NUnit
# - TestLinkedInScraper.csproj: PackageReference to Patchright
```

## Best Practices

### For Maximum Stealth

Use Chrome instead of Chromium with the following configuration:

```csharp
await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
    userDataDir: "...",
    new BrowserTypeLaunchPersistentContextOptions 
    {
        Channel = "chrome",
        Headless = false,
        ViewportSize = ViewportSize.NoViewport,
        // ⚠️ IMPORTANT: Do NOT add custom browser headers or userAgent
        // Let Patchright handle fingerprinting
    });
```

### Installation

Install Chrome for Patchright:
```bash
# Windows
./playwright.ps1 install --with-deps chrome

# Linux/macOS
./playwright.sh install --with-deps chrome
```

### Important Notes

1. **Chromium Only**: Patchright only patches CHROMIUM-based browsers. Firefox and Webkit are NOT supported.

2. **Namespace**: Continue using `using Microsoft.Playwright;` - Patchright uses the same namespace for compatibility.

3. **API Compatibility**: All Microsoft.Playwright APIs work exactly the same. No code changes needed.

4. **Extended API**: Patchright adds optional `isolatedContext` parameter to evaluation methods:
   ```csharp
   await page.EvaluateAsync("...", isolatedContext: true); // Default is true
   ```

## References

- [Patchright .NET GitHub](https://github.com/Kaliiiiiiiiii-Vinyzu/patchright-dotnet)
- [Patchright Driver](https://github.com/Kaliiiiiiiiii-Vinyzu/patchright)
- [Original Playwright Documentation](https://playwright.dev/dotnet/docs/intro)
- [Patchright NuGet Package](https://www.nuget.org/packages/Patchright)

## License

Patchright is licensed under Apache 2.0, same as Playwright.

---

**Migration Completed**: 2026-02-08  
**Patchright Version**: 1.58.0  
**Status**: ✅ Production Ready
