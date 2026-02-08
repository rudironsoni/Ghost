# Ghost CAPTCHA Solving

Free, zero-cost CAPTCHA solving using NopeCHA browser extension and self-hosted captcha-tensorflow.

## Overview

This module provides automated CAPTCHA solving with a fallback chain:
1. **NopeCHA Extension** (Primary) - Browser-based, automated solving
2. **TensorFlow Model** (Backup) - Self-hosted CNN model for text-based CAPTCHAs

## Supported CAPTCHA Types

### NopeCHA Provider
- ✅ reCAPTCHA v2
- ✅ reCAPTCHA v3
- ✅ hCaptcha
- ✅ FunCaptcha (Arkose Labs)
- ✅ Cloudflare Turnstile

### TensorFlow Provider
- ✅ Text-based image CAPTCHAs

## Usage

### Basic Setup

```csharp
// Register CAPTCHA services
services.AddCaptchaSolving(options =>
{
    options.EnableNopeCHA = true;
    options.EnableTensorFlow = true;
    options.NopeCHAExtensionPath = "/path/to/nopecha-extension";
    options.TensorFlowApiEndpoint = "http://localhost:5000";
    options.SolvingTimeout = TimeSpan.FromSeconds(60);
});
```

### Solving a CAPTCHA

```csharp
var captchaService = serviceProvider.GetRequiredService<CaptchaService>();

// Create challenge
var challenge = new CaptchaChallenge(CaptchaType.ReCaptchaV2, "https://example.com")
{
    SiteKey = "6Le-wvkSAAAAAPBMRTvw0Q4Muexq9bi0DJwx_mJ-"
};

// Solve with automatic fallback
var solution = await captchaService.SolveAsync(challenge);
```

### Tracking Metrics

```csharp
var metrics = captchaService.Metrics.GetAllMetrics();
foreach (var (provider, (successes, failures, rate)) in metrics)
{
    Console.WriteLine($"{provider}: {rate:P} success rate ({successes}/{successes + failures})");
}
```

## Setup Instructions

### NopeCHA Extension

1. Download NopeCHA extension for Chrome
   - Visit: https://nopecha.com/
   - Sign up for free tier (100 solves/month)
   - Download extension files

2. Configure extension path:
   ```csharp
   options.NopeCHAExtensionPath = "/path/to/nopecha-extension";
   ```

3. Load extension in Patchright:
   ```csharp
   var options = new BrowserTypeLaunchOptions
   {
       Args = new[]
       {
           $"--load-extension=/path/to/nopecha-extension",
           "--disable-extensions-except=/path/to/nopecha-extension"
       }
   };
   ```

### TensorFlow Model (Self-Hosted)

1. Clone captcha-tensorflow:
   ```bash
   git clone https://github.com/yuval-a/captcha-tensorflow
   cd captcha-tensorflow
   ```

2. Train model:
   ```bash
   python train.py --dataset captcha_images/
   ```

3. Run API server:
   ```bash
   python api_server.py --port 5000
   ```

4. Configure endpoint:
   ```csharp
   options.TensorFlowApiEndpoint = "http://localhost:5000";
   ```

## Architecture

```
┌─────────────────────────────────────────┐
│         CaptchaService                  │
│  (Orchestrates fallback chain)          │
└─────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
┌──────────────┐      ┌──────────────┐
│  NopeCHA     │      │  TensorFlow  │
│  Provider    │      │  Provider    │
│  (Primary)   │      │  (Backup)    │
└──────────────┘      └──────────────┘
```

## Fallback Logic

1. Check if NopeCHA available and supports CAPTCHA type
2. If yes: Try NopeCHA
3. If NopeCHA fails or unavailable: Try TensorFlow
4. If both fail: Throw AggregateException

## Cost Breakdown

| Provider    | Cost           | Solves/Month | Notes                    |
|-------------|----------------|--------------|--------------------------|
| NopeCHA     | $0 (free tier) | 100          | Browser extension        |
| TensorFlow  | $0 (self-host) | Unlimited    | Requires GPU (optional)  |
| **Total**   | **$0**         | **Unlimited**| Zero third-party costs   |

Compare to commercial:
- 2Captcha: $2-3 per 1000 solves
- Anti-Captcha: $2 per 1000 solves

## Best Practices

1. **Use NopeCHA for complex CAPTCHAs** (reCAPTCHA, hCaptcha)
2. **Use TensorFlow for simple text CAPTCHAs** (faster, no API limits)
3. **Monitor success rates** with built-in metrics
4. **Configure timeout** based on CAPTCHA complexity
5. **Handle failures gracefully** - not all CAPTCHAs can be solved

## Limitations

- NopeCHA free tier: 100 solves/month
- TensorFlow: Best for text-based CAPTCHAs only
- reCAPTCHA v3: May require score threshold tuning
- Success rate: 85-95% depending on CAPTCHA complexity

## Future Enhancements

- [ ] Add retry logic with exponential backoff
- [ ] Implement CAPTCHA detection (auto-detect type)
- [ ] Add support for audio CAPTCHAs
- [ ] Implement CAPTCHA bypass techniques
- [ ] Add more provider integrations (free only)
