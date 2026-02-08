#!/bin/bash
set -euo pipefail

# Ghost k3s Worker Node Installation
# This script joins a worker node to an existing k3s cluster

echo "=== Ghost k3s Worker Node Installation ==="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root or with sudo"
    exit 1
fi

# Check required environment variables
if [ -z "${K3S_TOKEN:-}" ]; then
    echo "ERROR: K3S_TOKEN environment variable is required"
    echo "Get the token from the master node: cat /var/lib/rancher/k3s/server/node-token"
    exit 1
fi

if [ -z "${K3S_URL:-}" ]; then
    echo "ERROR: K3S_URL environment variable is required"
    echo "Format: https://<master-ip>:6443"
    exit 1
fi

# Configuration
K3S_VERSION="${K3S_VERSION:-v1.29.1+k3s2}"

echo "Joining k3s cluster at ${K3S_URL}..."

# Install k3s agent (worker) with optimizations
curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="${K3S_VERSION}" \
    K3S_TOKEN="${K3S_TOKEN}" \
    K3S_URL="${K3S_URL}" \
    sh -s - agent \
    --kubelet-arg="max-pods=250" \
    --kubelet-arg="image-gc-high-threshold=85" \
    --kubelet-arg="image-gc-low-threshold=80"

# Wait for k3s agent to start
echo "Waiting for k3s agent to start..."
sleep 10

# Check if k3s agent is running
systemctl status k3s-agent --no-pager

echo ""
echo "=== Worker Node Installation Complete ==="
echo ""
echo "This node has joined the cluster at ${K3S_URL}"
echo ""
echo "Verify on the master node with:"
echo "  kubectl get nodes"
echo ""
