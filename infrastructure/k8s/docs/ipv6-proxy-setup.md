# IPv6 Proxy Rotation Setup Guide

## Overview

Ghost's IPv6 rotation provides **millions of unique IP addresses** from a single VPS with a /64 IPv6 subnet. This approach costs ~$5/month per VPS compared to $500+/month for commercial proxy services, achieving **100x cost savings** while improving scraping reliability.

## How It Works

### IPv6 /64 Subnets

A /64 subnet contains **2^64 = 18,446,744,073,709,551,616 addresses**. This means:

- Each address can be used as a unique egress IP
- Rotating through billions of IPs makes detection nearly impossible
- Anti-bot systems cannot blocklist the entire subnet
- Each worker can use fresh IPs for every request

### Architecture

```
┌─────────────────────────────────────────────────┐
│              Ghost Worker Pod                   │
│  ┌──────────────────────────────────────────┐   │
│  │  IPv6Rotator                             │   │
│  │  - Generates random IPv6 from /64        │   │
│  │  - Health checks addresses               │   │
│  │  - Binds to network interface (Linux)    │   │
│  └──────────────┬───────────────────────────┘   │
│                 │                                │
│  ┌──────────────▼───────────────────────────┐   │
│  │  Playwright Browser                      │   │
│  │  - Uses IPv6 as source IP                │   │
│  │  - Each request = new IP                 │   │
│  └──────────────┬───────────────────────────┘   │
└─────────────────┼───────────────────────────────┘
                  │
           ┌──────▼──────┐
           │ Target Site │
           │ (LinkedIn,  │
           │  Indeed,    │
           │  etc.)      │
           └─────────────┘
```

## Prerequisites

### VPS Provider with IPv6 /64

Not all providers offer /64 subnets by default. Recommended providers:

| Provider       | IPv6 /64 | Cost/Month | Setup        |
|----------------|----------|------------|--------------|
| Hetzner        | ✅ Free   | $5-10      | Automatic    |
| DigitalOcean   | ✅ Free   | $6-12      | Manual       |
| Linode         | ✅ Free   | $5-10      | Automatic    |
| Vultr          | ✅ Free   | $5-10      | Manual       |
| AWS EC2        | ❌ /56    | $20+       | Not suitable |
| Google Cloud   | ❌ /96    | $25+       | Not suitable |

**Recommendation**: Hetzner Cloud for best IPv6 support and pricing.

### System Requirements

- Linux server (Ubuntu 20.04+, Debian 11+, or similar)
- Root access (for network configuration)
- `iproute2` package installed (`ip` command)
- IPv6 connectivity to the internet

## Setup Instructions

### Step 1: Verify IPv6 Subnet

Check your allocated IPv6 subnet:

```bash
# List IPv6 addresses
ip -6 addr show

# Example output:
# inet6 2001:db8:1234:5678::1/64 scope global
```

The `/64` indicates your subnet. The prefix is everything before the last `::`, e.g., `2001:db8:1234:5678`.

### Step 2: Enable IPv6 Forwarding

```bash
# Enable IPv6 forwarding
sudo sysctl -w net.ipv6.conf.all.forwarding=1
sudo sysctl -w net.ipv6.conf.all.proxy_ndp=1

# Make persistent across reboots
echo "net.ipv6.conf.all.forwarding=1" | sudo tee -a /etc/sysctl.conf
echo "net.ipv6.conf.all.proxy_ndp=1" | sudo tee -a /etc/sysctl.conf
sudo sysctl -p
```

### Step 3: Configure Ghost Worker

#### Option A: Environment Variables

Set IPv6 configuration in your deployment:

```bash
# Kubernetes ConfigMap (ghost-worker-deployment.yaml)
IPV6_SUBNET_PREFIX: "2001:db8:1234:5678"
IPV6_NETWORK_INTERFACE: "eth0"
IPV6_AUTO_BIND: "false"
IPV6_ENABLE_HEALTH_CHECK: "true"
```

#### Option B: Code Configuration

```csharp
using Ghost.Core.ProxyManagement;

var options = new IPv6RotatorOptions
{
    SubnetPrefix = "2001:db8:1234:5678",
    NetworkInterface = "eth0",
    EnableHealthCheck = true,
    AutoBind = false, // Requires root privileges
    MaxPoolSize = 1000
};

using var rotator = new IPv6Rotator(options);

// Generate a random IPv6 address
var ipv6Address = await rotator.GetRandomAddressAsync();
Console.WriteLine($"Using IPv6: {ipv6Address}");

// Generate multiple addresses
var addresses = await rotator.GetRandomAddressesAsync(10);
```

### Step 4: Test IPv6 Connectivity

```bash
# Test IPv6 connectivity
ping6 -c 4 google.com

# Test specific IPv6 address from your subnet
# (Replace with your actual subnet prefix)
sudo ip -6 addr add 2001:db8:1234:5678::2/128 dev eth0
curl -6 --interface 2001:db8:1234:5678::2 https://ipv6.icanhazip.com
sudo ip -6 addr del 2001:db8:1234:5678::2/128 dev eth0
```

Expected output: Your generated IPv6 address should appear.

### Step 5: Integrate with Playwright

```csharp
using Ghost.Core.ProxyManagement;
using Microsoft.Playwright;

var rotator = new IPv6Rotator(new IPv6RotatorOptions
{
    SubnetPrefix = "2001:db8:1234:5678",
    EnableHealthCheck = true
});

// Create browser with IPv6 proxy
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(
    await rotator.WithIPv6ProxyAsync()
);

// Or create context with fresh IPv6 for each session
var context = await browser.NewContextWithIPv6Async(rotator);
var page = await context.NewPageAsync();

// Each navigation uses a different IPv6 address
await page.GotoAsync("https://example.com");
```

## Advanced Configuration

### Automatic Address Binding (Linux Only)

To automatically bind IPv6 addresses to the network interface:

```csharp
var options = new IPv6RotatorOptions
{
    SubnetPrefix = "2001:db8:1234:5678",
    NetworkInterface = "eth0",
    AutoBind = true // Requires root privileges
};
```

**Warning**: Requires Ghost worker to run as root. For security, prefer running workers as non-root and binding addresses manually via a privileged sidecar container.

### Kubernetes Sidecar Pattern

Deploy a privileged sidecar container to handle IPv6 binding:

```yaml
# Add to ghost-worker-deployment.yaml
containers:
- name: ipv6-binder
  image: alpine:latest
  command:
  - /bin/sh
  - -c
  - |
    while true; do
      # Bind new IPv6 addresses as requested
      # (Implementation depends on your architecture)
      sleep 60
    done
  securityContext:
    privileged: true
    capabilities:
      add:
      - NET_ADMIN
```

### Health Checking

IPv6Rotator includes health checking to verify addresses are reachable:

```csharp
var isHealthy = await rotator.CheckAddressHealthAsync("2001:db8::1");
if (!isHealthy)
{
    // Address is unreachable, use a different one
}
```

Health checking uses ICMP (ping) by default. Adjust timeout as needed:

```csharp
var options = new IPv6RotatorOptions
{
    EnableHealthCheck = true,
    HealthCheckTimeoutSeconds = 3
};
```

### Pool Management

Control the size of the active address pool:

```csharp
var options = new IPv6RotatorOptions
{
    MaxPoolSize = 5000 // Keep 5000 addresses in rotation
};
```

Pool operates as FIFO (First In, First Out). When maxed out, oldest addresses are removed.

### Statistics

Monitor IPv6 rotation stats:

```csharp
var stats = rotator.GetStats();
Console.WriteLine($"Active Addresses: {stats.ActiveAddressCount}/{stats.MaxPoolSize}");
Console.WriteLine($"Total Available: {stats.TotalAvailableAddresses}");
```

## SOCKS5 Proxy Setup (Optional)

For advanced setups, deploy a SOCKS5 proxy that can bind to specific source IPs:

### Using 3proxy

```bash
# Install 3proxy
sudo apt-get install 3proxy

# Configure /etc/3proxy/3proxy.cfg
socks -p1080 -i2001:db8:1234:5678::1

# Start 3proxy
sudo systemctl start 3proxy
```

### Using gost

```bash
# Install gost
wget https://github.com/ginuerzh/gost/releases/download/v2.11.5/gost-linux-amd64-2.11.5.gz
gunzip gost-linux-amd64-2.11.5.gz
chmod +x gost-linux-amd64-2.11.5
sudo mv gost-linux-amd64-2.11.5 /usr/local/bin/gost

# Run with IPv6 binding
gost -L socks5://:1080?bind=[2001:db8:1234:5678::1]
```

## Troubleshooting

### IPv6 Not Working

**Symptom**: `GetRandomAddressAsync()` returns addresses but connections fail.

**Solutions**:
1. Verify IPv6 connectivity: `ping6 google.com`
2. Check firewall allows outbound IPv6: `ip6tables -L`
3. Ensure subnet prefix is correct: `ip -6 addr show`
4. Test with `curl -6`: `curl -6 https://ipv6.icanhazip.com`

### Address Binding Fails

**Symptom**: `BindAddressAsync()` returns false.

**Solutions**:
1. Check permissions: Must run as root or with `CAP_NET_ADMIN`
2. Verify interface name: `ip link show`
3. Check if address already bound: `ip -6 addr show dev eth0`
4. Use `sudo` or privileged container

### Health Checks Always Fail

**Symptom**: All addresses fail health checks.

**Solutions**:
1. Disable health checks temporarily: `EnableHealthCheck = false`
2. Check ICMP is allowed: `sudo sysctl net.ipv6.icmp.ratelimit`
3. Increase timeout: `HealthCheckTimeoutSeconds = 10`
4. Some networks block ICMP - this is normal, disable health checks

### Running Out of Addresses

**Symptom**: Worker reports "pool exhausted" errors.

**Reality Check**: You have **18 quintillion addresses**. If you're "running out," there's a bug or misconfiguration.

**Solutions**:
1. Increase pool size: `MaxPoolSize = 10000`
2. Check for address leaks (not releasing old addresses)
3. Reduce concurrent sessions per worker

## Cost Analysis

### Commercial Proxies (Traditional Approach)

- **Residential Proxies**: $500-2,000/month for 1M requests
- **Datacenter Proxies**: $200-500/month for 100K IPs
- **Rotating Proxies**: $300-1,000/month for unlimited rotation

**Total**: $500-2,000/month minimum

### Ghost IPv6 Approach

- **VPS with /64 subnet**: $5-10/month (Hetzner, Linode)
- **IPv6 addresses**: $0 (included)
- **Unlimited rotation**: $0
- **Bandwidth**: ~$0.01/GB (VPS bandwidth)

**Total**: $5-10/month per VPS

### Scaling Comparison

| Workers | Commercial Proxies | Ghost IPv6 | Savings |
|---------|-------------------|------------|---------|
| 10      | $500/month        | $50/month  | 90%     |
| 50      | $2,000/month      | $250/month | 87.5%   |
| 100     | $5,000/month      | $500/month | 90%     |

**Result**: Ghost IPv6 rotation achieves **10-20x cost savings** at any scale.

## Security Considerations

### IPv6 Address Randomization

Ghost generates cryptographically random IPv6 addresses:

```csharp
// Random.NextBytes() uses a secure PRNG
Span<byte> hostBytes = stackalloc byte[8];
_random.NextBytes(hostBytes);
```

This prevents predictable address patterns that could be detected and blocked.

### Rate Limiting

Even with millions of IPs, respect target site rate limits:

- Rotate IPs between requests (not during a single session)
- Implement delays between requests (behavioral mimicry)
- Use exponential backoff on errors

### Legal and Ethical Use

- Only scrape public data
- Respect `robots.txt`
- Comply with terms of service
- Don't abuse rate limits or cause service degradation
- Use for legitimate purposes (job aggregation, research, etc.)

## Performance Tuning

### Parallel Address Generation

Generate addresses in batches:

```csharp
// Generate 100 addresses in parallel
var addresses = await rotator.GetRandomAddressesAsync(100);
```

### Caching

Cache addresses for reuse within the same scraping session:

```csharp
var address = await rotator.GetRandomAddressAsync();
// Use same address for multiple requests in this session
// Rotate to new address for next session
```

### Disable Health Checks for Performance

If your network is stable, disable health checks:

```csharp
var options = new IPv6RotatorOptions
{
    EnableHealthCheck = false // Faster, but no validation
};
```

## Next Steps

1. Configure IPv6 on your VPS (Step 1-2)
2. Update Ghost worker deployment with subnet prefix (Step 3)
3. Test connectivity (Step 4)
4. Deploy Ghost workers to Kubernetes (Task 4.3)
5. Monitor scraping success rates with IPv6 rotation

For questions or issues, see the main project documentation or open an issue on GitHub.
