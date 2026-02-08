# TLS Fingerprint Randomization (JA3) Implementation

## Overview
This module implements JA3 fingerprint randomization to defeat TLS fingerprinting-based bot detection systems. Each browser session can present a unique TLS signature that mimics real browsers.

## Components

### JA3Profile.cs
Core data model representing a JA3 TLS fingerprint:
- TLS version
- Cipher suites (order matters)
- Extensions (order matters)
- Elliptic curves
- EC point formats

Provides methods to convert to JA3 string format and generate MD5 hash (standard JA3 fingerprint).

### BrowserProfiles.cs
Realistic JA3 profiles based on actual browser TLS handshakes:
- Chrome 120+
- Firefox 121+
- Safari 17+
- Edge (Chromium)

Each profile contains authentic cipher suites, extensions, and curves observed from real browsers.

### JA3Randomizer.cs
Generates randomized JA3 profiles while maintaining realistic browser characteristics:
- Shuffles cipher suites (keeping TLS 1.3 ciphers first)
- Randomizes extension order (keeping critical extensions like `server_name` first)
- Varies elliptic curve preferences (keeping X25519 preferred)

Ensures each profile is unique while remaining browser-realistic.

### TLSFingerprintService.cs
Service for applying TLS fingerprints to browser contexts:
- Generates random JA3 profiles
- Provides browser-specific launch arguments
- Placeholder for CDP-based TLS modification (future enhancement)

## Usage

```csharp
// Generate a random JA3 profile
var randomizer = new JA3Randomizer();
var profile = randomizer.GenerateRandomProfile();

Console.WriteLine($"JA3 Hash: {profile.ToJA3Hash()}");
Console.WriteLine($"JA3 String: {profile.ToJA3String()}");

// Generate browser-specific profile
var chromeProfile = randomizer.GenerateRandomProfile("chrome");
var firefoxProfile = randomizer.GenerateRandomProfile("firefox");

// Generate multiple unique profiles
var profiles = randomizer.GenerateMultipleProfiles(10);

// Use with TLSFingerprintService
var service = new TLSFingerprintService(logger);
var profile = service.GenerateProfile("chrome");
```

## Verification

### Manual Testing
Run the JA3Demo program to verify profile generation:
```bash
cd tests/Core/Ghost.Tests
dotnet run --project Ghost.Tests.csproj -- Stealth/TLS/JA3Demo.cs
```

Expected output:
- 10 unique JA3 hashes
- Different profiles for each browser type
- Valid JA3 string format: `TLSVersion,Ciphers,Extensions,Curves,Formats`

### Online Testing
Test against https://ja3er.com/json to verify actual TLS fingerprints:
```bash
curl -s https://ja3er.com/json | jq .
```

Compare the detected JA3 hash with generated profiles.

## Implementation Status

### ✅ Completed
- JA3 profile data model
- Browser-realistic base profiles (Chrome, Firefox, Safari, Edge)
- Randomization algorithm with constraints
- Profile generation and hashing
- Unit tests for all components
- Static method support

### ⚠️ Limitations
- **TLS modification not yet integrated with Patchright**
  - Requires low-level network control (proxy or browser kernel patches)
  - CDP does not support direct cipher/extension manipulation
  - Current implementation generates profiles but cannot apply them to actual connections

### 🔧 Future Enhancements
1. **Proxy-based TLS termination**
   - Use mitmproxy with custom TLS configuration
   - Apply JA3 profiles to actual handshakes

2. **Patchright kernel patches**
   - Modify Playwright/Patchright to support TLS customization
   - Add CDP commands for cipher suite control

3. **Live verification**
   - Integrate with ja3er.com API
   - Automated testing against fingerprinting services

4. **Profile learning**
   - Capture real browser fingerprints from network traces
   - Update base profiles based on latest browser versions

## Testing

### Unit Tests
All core functionality is tested:
- ✅ JA3 string format generation
- ✅ MD5 hash calculation
- ✅ Profile cloning
- ✅ Browser profile validity
- ✅ Randomization maintains constraints
- ✅ Unique profile generation
- ✅ Deterministic randomization (seeded)

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~TLS"
```

### Integration Testing
Manual verification required:
1. Generate profiles: `var profile = randomizer.GenerateRandomProfile();`
2. Verify uniqueness: Generate 10+ profiles, check hash diversity
3. Verify format: JA3 string should match `VERSION,CIPHERS,EXTENSIONS,CURVES,FORMATS`
4. Verify hash: 32-character lowercase hex string

## Security Notes

- **MD5 usage**: JA3 specification requires MD5 for fingerprint hashing. This is NOT for cryptographic security, only for fingerprint identification.
- **Fingerprint diversity**: More randomization = more unique but potentially more detectable. Current implementation balances realism with diversity.
- **Browser signatures**: Base profiles are derived from actual browser handshakes. Keep updated as browsers evolve.

## References

- [JA3 Specification](https://github.com/salesforce/ja3)
- [JA3er Testing Tool](https://ja3er.com/)
- [TLS 1.3 RFC 8446](https://tools.ietf.org/html/rfc8446)
- [ScrapingAnt JA3 Research](https://scrapingant.com/blog/ja3-fingerprinting)

## Known Issues

1. **Build dependency on Patchright IPage**
   - TLSFingerprintService.VerifyFingerprintAsync() is a placeholder
   - Requires resolving IPage interface usage

2. **No actual TLS modification**
   - Profiles are generated but not applied to connections
   - Requires proxy or browser kernel integration

3. **Limited browser coverage**
   - Only Chrome, Firefox, Safari, Edge
   - Could add Brave, Opera, other browsers

## Contributing

When updating profiles:
1. Capture real TLS handshakes using Wireshark
2. Extract cipher suites, extensions, curves from Client Hello
3. Update BrowserProfiles.cs with new data
4. Verify against ja3er.com
5. Add tests for new profiles
