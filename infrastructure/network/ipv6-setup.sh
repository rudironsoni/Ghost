#!/bin/bash
# IPv6 Proxy Rotation Setup Script
# Ghost - Stealth Browser Automation Framework
# Configure IPv6 /64 subnet for proxy rotation (millions of IPs from single VPS)

set -e

echo "=========================================="
echo "Ghost IPv6 Proxy Rotation Setup"
echo "=========================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root (use sudo)"
    exit 1
fi

# Check if IPv6 is available
if ! command -v ip &> /dev/null; then
    echo "ERROR: 'ip' command not found. Install iproute2."
    exit 1
fi

# Detect primary network interface
INTERFACE=${INTERFACE:-$(ip route | grep default | awk '{print $5}' | head -1)}
if [ -z "$INTERFACE" ]; then
    echo "ERROR: Could not detect network interface. Set INTERFACE environment variable."
    exit 1
fi

echo "Network Interface: $INTERFACE"
echo ""

# Check existing IPv6 configuration
echo "Current IPv6 addresses on $INTERFACE:"
ip -6 addr show dev "$INTERFACE"
echo ""

# Get IPv6 /64 subnet from user or environment
if [ -z "$IPV6_SUBNET" ]; then
    echo "Enter your IPv6 /64 subnet prefix (e.g., 2001:db8:1234:5678):"
    echo "(Get this from your VPS provider - Hetzner, OVH, Vultr, Linode)"
    read -r IPV6_SUBNET
fi

# Validate subnet format (4 groups of hex)
if ! echo "$IPV6_SUBNET" | grep -qE '^([0-9a-fA-F]{1,4}:){3}[0-9a-fA-F]{1,4}$'; then
    echo "ERROR: Invalid IPv6 subnet format. Expected format: xxxx:xxxx:xxxx:xxxx"
    exit 1
fi

echo ""
echo "IPv6 Subnet: $IPV6_SUBNET::/64"
echo "This provides 2^64 = 18,446,744,073,709,551,616 unique IP addresses!"
echo ""

# Enable IPv6 forwarding
echo "Enabling IPv6 forwarding..."
sysctl -w net.ipv6.conf.all.forwarding=1 > /dev/null
sysctl -w net.ipv6.conf."$INTERFACE".forwarding=1 > /dev/null

# Make it persistent
if ! grep -q "net.ipv6.conf.all.forwarding" /etc/sysctl.conf; then
    echo "net.ipv6.conf.all.forwarding=1" >> /etc/sysctl.conf
fi

# Disable IPv6 duplicate address detection (speeds up address binding)
echo "Configuring IPv6 parameters..."
sysctl -w net.ipv6.conf."$INTERFACE".accept_dad=0 > /dev/null
sysctl -w net.ipv6.conf.all.accept_dad=0 > /dev/null

# Add base address from subnet (::1)
BASE_ADDR="${IPV6_SUBNET}::1"
echo "Adding base address: $BASE_ADDR/64"

if ip -6 addr show dev "$INTERFACE" | grep -q "$BASE_ADDR"; then
    echo "Base address already exists, skipping..."
else
    ip -6 addr add "$BASE_ADDR"/64 dev "$INTERFACE"
    echo "✅ Base address added"
fi

# Test IPv6 connectivity
echo ""
echo "Testing IPv6 connectivity..."
if ping6 -c 3 -I "$INTERFACE" google.com > /dev/null 2>&1; then
    echo "✅ IPv6 connectivity working!"
else
    echo "⚠️  IPv6 connectivity test failed. Check with your VPS provider."
    echo "   Some providers require additional routing configuration."
fi

# Generate test addresses
echo ""
echo "Generating 5 random test addresses..."
for i in {1..5}; do
    RANDOM_HOST=$(printf "%04x:%04x:%04x:%04x" $RANDOM $RANDOM $RANDOM $RANDOM)
    TEST_ADDR="${IPV6_SUBNET}:${RANDOM_HOST}"
    echo "  - $TEST_ADDR"
done

echo ""
echo "=========================================="
echo "✅ IPv6 Proxy Rotation Setup Complete"
echo "=========================================="
echo ""
echo "Configuration saved:"
echo "  - Interface: $INTERFACE"
echo "  - Subnet: $IPV6_SUBNET::/64"
echo "  - Available IPs: 18 quintillion"
echo ""
echo "Update your Ghost configuration:"
echo ""
echo "  services.AddSingleton(new IPv6RotatorOptions"
echo "  {"
echo "      SubnetPrefix = \"$IPV6_SUBNET\","
echo "      NetworkInterface = \"$INTERFACE\","
echo "      EnableHealthCheck = true,"
echo "      AutoBind = true  // Requires root in production"
echo "  });"
echo ""
echo "Cost: ~\$5/month VPS vs \$500+/month commercial proxies"
echo ""
echo "To test manually:"
echo "  curl --interface ${IPV6_SUBNET}::1234 https://ipv6.icanhazip.com"
echo ""
