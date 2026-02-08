#!/bin/bash
set -euo pipefail

# Ghost k3s Master Node Installation
# This script sets up a k3s master node for Ghost distributed scraping

echo "=== Ghost k3s Master Node Installation ==="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root or with sudo"
    exit 1
fi

# Configuration
K3S_VERSION="${K3S_VERSION:-v1.29.1+k3s2}"
CLUSTER_CIDR="${CLUSTER_CIDR:-10.42.0.0/16}"
SERVICE_CIDR="${SERVICE_CIDR:-10.43.0.0/16}"

echo "Installing k3s ${K3S_VERSION}..."

# Install k3s with options optimized for Ghost workloads
curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="${K3S_VERSION}" sh -s - server \
    --write-kubeconfig-mode 644 \
    --disable traefik \
    --disable servicelb \
    --cluster-cidr="${CLUSTER_CIDR}" \
    --service-cidr="${SERVICE_CIDR}" \
    --kube-apiserver-arg="max-requests-inflight=2000" \
    --kube-apiserver-arg="max-mutating-requests-inflight=1000" \
    --kubelet-arg="max-pods=250"

# Wait for k3s to be ready
echo "Waiting for k3s to be ready..."
sleep 10

# Check if k3s is running
systemctl status k3s --no-pager

# Get node token for workers
NODE_TOKEN=$(cat /var/lib/rancher/k3s/server/node-token)
MASTER_IP=$(hostname -I | awk '{print $1}')

echo ""
echo "=== Installation Complete ==="
echo ""
echo "Master Node IP: ${MASTER_IP}"
echo "Node Token: ${NODE_TOKEN}"
echo ""
echo "To add worker nodes, run on each worker:"
echo "  export K3S_TOKEN='${NODE_TOKEN}'"
echo "  export K3S_URL='https://${MASTER_IP}:6443'"
echo "  curl -sfL https://get.k3s.io | sh -"
echo ""
echo "Or use the k3s-worker-install.sh script with:"
echo "  sudo K3S_TOKEN='${NODE_TOKEN}' K3S_URL='https://${MASTER_IP}:6443' ./k3s-worker-install.sh"
echo ""
echo "Save this token securely - you'll need it for worker nodes!"
echo ""

# Export kubeconfig for current user
if [ -n "${SUDO_USER:-}" ]; then
    echo "Setting up kubectl for user ${SUDO_USER}..."
    mkdir -p /home/${SUDO_USER}/.kube
    cp /etc/rancher/k3s/k3s.yaml /home/${SUDO_USER}/.kube/config
    chown -R ${SUDO_USER}:${SUDO_USER} /home/${SUDO_USER}/.kube
    sed -i "s/127.0.0.1/${MASTER_IP}/g" /home/${SUDO_USER}/.kube/config
fi

echo "Verifying cluster status..."
kubectl get nodes
kubectl get pods -A

echo ""
echo "Next steps:"
echo "1. Add worker nodes using k3s-worker-install.sh"
echo "2. Deploy Ghost workers: kubectl apply -f ghost-worker-deployment.yaml"
echo "3. Setup auto-scaling: kubectl apply -f hpa.yaml"
