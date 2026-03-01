# Team Onboarding Guide - Ghost Platform

**Welcome to the Ghost Platform Team!** 🎉

This guide will help you get up and running with the Ghost Platform infrastructure and development environment.

## Table of Contents

1. [Welcome](#welcome)
2. [First Day Setup](#first-day-setup)
3. [Development Environment](#development-environment)
4. [Infrastructure Access](#infrastructure-access)
5. [Tools and Services](#tools-and-services)
6. [Architecture Overview](#architecture-overview)
7. [Development Workflow](#development-workflow)
8. [Deployment Process](#deployment-process)
9. [Monitoring and Observability](#monitoring-and-observability)
10. [On-Call Rotation](#on-call-rotation)
11. [Resources and Learning](#resources-and-learning)

---

## Welcome

### Team Structure

**Platform Engineering Team:**
- **Team Lead:** Alice Johnson (@alice)
- **Senior Engineers:** Bob Wilson (@bob), Charlie Brown (@charlie)
- **Engineers:** Diana Prince (@diana), Eve Martinez (@eve)
- **DevOps:** Frank Castle (@frank)

**Communication Channels:**
- **Team Chat:** `#platform-engineering` (Slack)
- **Incidents:** `#incidents` (Slack)
- **Announcements:** `#platform-announcements` (Slack)
- **General:** `#engineering` (Slack)

**Meeting Schedule:**
- **Daily Standup:** 10:00 AM EST (Mon-Fri)
- **Sprint Planning:** Every other Monday, 2:00 PM EST
- **Retrospective:** Every other Friday, 3:00 PM EST
- **Architecture Review:** Weekly Wednesday, 11:00 AM EST
- **On-Call Handoff:** Monday, 9:00 AM EST

---

## First Day Setup

### 1. Administrative Tasks

- [ ] Complete HR onboarding paperwork
- [ ] Receive company laptop and equipment
- [ ] Set up email account
- [ ] Create accounts on company systems
- [ ] Sign NDA and security agreements
- [ ] Get building access badge

### 2. IT Setup

- [ ] Install company VPN
- [ ] Configure email client
- [ ] Join Slack workspace
- [ ] Set up 2FA for all accounts
- [ ] Install password manager (1Password/LastPass)
- [ ] Configure GitHub account with company email

### 3. Team Introduction

- [ ] Meet with team lead
- [ ] Introduction meeting with team
- [ ] Buddy assignment (your buddy: [Name])
- [ ] Review team charter and values
- [ ] Add yourself to team roster

---

## Development Environment

### Required Software

```bash
# Package managers
brew --version        # macOS: Homebrew 4.0+
choco --version       # Windows: Chocolatey 2.0+
apt --version         # Linux: APT

# Version control
git --version         # Git 2.40+

# Programming languages
node --version        # Node.js 20+
npm --version         # npm 10+
python3 --version     # Python 3.11+

# Containers and orchestration
docker --version      # Docker 24+
kubectl version       # kubectl 1.28+
helm version          # Helm 3.12+

# Infrastructure as Code
terraform version     # Terraform 1.5+
ansible --version     # Ansible 2.15+

# Cloud CLIs
aws --version         # AWS CLI 2.0+
gcloud version        # Google Cloud SDK (if using GCP)
az version            # Azure CLI (if using Azure)
```

### Installation Script

```bash
#!/bin/bash
# setup-dev-environment.sh

echo "Setting up Ghost Platform development environment..."

# macOS
if [[ "$OSTYPE" == "darwin"* ]]; then
    # Install Homebrew
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
    
    # Install tools
    brew install git node python docker kubectl helm terraform ansible awscli
    brew install --cask visual-studio-code
    brew install --cask slack
    brew install --cask 1password
fi

# Ubuntu/Debian
if [[ -f /etc/debian_version ]]; then
    sudo apt update
    sudo apt install -y git curl wget build-essential
    
    # Node.js
    curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
    sudo apt install -y nodejs
    
    # Docker
    curl -fsSL https://get.docker.com | sh
    sudo usermod -aG docker $USER
    
    # Kubectl
    curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
    sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
    
    # Terraform
    wget https://releases.hashicorp.com/terraform/1.5.0/terraform_1.5.0_linux_amd64.zip
    unzip terraform_1.5.0_linux_amd64.zip
    sudo mv terraform /usr/local/bin/
fi

echo "✓ Development environment setup complete!"
```

### IDE Setup

**Visual Studio Code Extensions:**

```json
{
  "recommendations": [
    "ms-azuretools.vscode-docker",
    "ms-kubernetes-tools.vscode-kubernetes-tools",
    "hashicorp.terraform",
    "redhat.ansible",
    "dbaeumer.vscode-eslint",
    "esbenp.prettier-vscode",
    "eamodio.gitlens",
    "github.copilot",
    "ms-python.python"
  ]
}
```

**VS Code Settings:**

```json
{
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": true
  },
  "files.autoSave": "onFocusChange",
  "terraform.experimentalFeatures.validateOnSave": true,
  "ansible.validation.enabled": true
}
```

### Clone Repositories

```bash
# Create workspace directory
mkdir -p ~/workspace/ghost
cd ~/workspace/ghost

# Clone main repository
git clone git@github.com:ghost/platform.git
cd platform

# Install dependencies
npm install

# Copy environment template
cp .env.example .env.local

# Edit .env.local with your configuration
vim .env.local
```

### Local Development Setup

```bash
# Navigate to miser-mode for local development
cd infrastructure/miser-mode

# Start local environment
docker compose up -d

# Verify services
docker compose ps

# Check logs
docker compose logs -f ghost-api

# Access services
# API: http://localhost:8080
# RabbitMQ: http://localhost:15672 (admin/admin)
# Grafana: http://localhost:3000 (admin/admin)
# Prometheus: http://localhost:9090
```

---

## Infrastructure Access

### VPN Access

```bash
# Install VPN client
# Download from: https://vpn.ghost.example.com

# Connect to VPN
# Corporate: vpn.ghost.example.com
# Username: [your-email]
# Password: [your-password] + [2FA code]
```

### Cloud Access

#### AWS Access

```bash
# Configure AWS CLI
aws configure --profile ghost-dev
# Access Key ID: [from 1Password]
# Secret Access Key: [from 1Password]
# Region: us-east-1
# Output: json

# Verify access
aws sts get-caller-identity --profile ghost-dev

# Use profile in commands
export AWS_PROFILE=ghost-dev
aws eks list-clusters
```

#### Google Cloud Access (if applicable)

```bash
# Authenticate
gcloud auth login

# Set project
gcloud config set project ghost-platform-dev

# Get cluster credentials
gcloud container clusters get-credentials ghost-dev --region us-east1
```

### Kubernetes Access

```bash
# Development cluster
aws eks update-kubeconfig --region us-east-1 --name ghost-dev --profile ghost-dev

# Verify access
kubectl cluster-info
kubectl get nodes
kubectl get pods -n ghost

# Set default namespace
kubectl config set-context --current --namespace=ghost

# Install k9s for easier cluster management
brew install derailed/k9s/k9s
k9s
```

### Database Access

```bash
# Development database (via bastion)
ssh -i ~/.ssh/ghost-dev.pem -L 5432:postgres-dev.internal:5432 bastion@bastion-dev.ghost.example.com

# Connect with psql
psql -h localhost -U ghost -d ghost

# Or use GUI tool
# DBeaver: https://dbeaver.io/
# pgAdmin: https://www.pgadmin.org/
```

---

## Tools and Services

### Development Tools

| Tool | Purpose | URL | Credentials |
|------|---------|-----|-------------|
| GitHub | Source control | https://github.com/ghost | SSO |
| Jira | Project management | https://ghost.atlassian.net | SSO |
| Confluence | Documentation | https://ghost.atlassian.net/wiki | SSO |
| Slack | Communication | https://ghost.slack.com | SSO |

### Infrastructure Tools

| Tool | Purpose | URL | Access |
|------|---------|-----|--------|
| AWS Console | Cloud infrastructure | https://console.aws.amazon.com | IAM |
| Terraform Cloud | IaC state management | https://app.terraform.io | SSO |
| Vault | Secrets management | https://vault.ghost.example.com | Token |
| ArgoCD | GitOps deployments | https://argocd.ghost.example.com | SSO |

### Observability Tools

| Tool | Purpose | URL | Credentials |
|------|---------|-----|-------------|
| Grafana | Dashboards | https://grafana.ghost.example.com | SSO |
| Prometheus | Metrics | https://prometheus.ghost.example.com | Basic Auth |
| Jaeger | Tracing | https://jaeger.ghost.example.com | SSO |
| Kibana | Logs | https://kibana.ghost.example.com | SSO |
| PagerDuty | On-call alerts | https://ghost.pagerduty.com | SSO |

### Security Tools

| Tool | Purpose | URL | Access |
|------|---------|-----|--------|
| Snyk | Dependency scanning | https://app.snyk.io | SSO |
| SonarQube | Code quality | https://sonar.ghost.example.com | SSO |
| Vault | Secrets | https://vault.ghost.example.com | Token |

---

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Users                                │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                   CDN / Load Balancer                        │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  API Gateway / Ingress                       │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼               ▼
┌──────────────┐ ┌──────────┐ ┌──────────────┐
│   Ghost API  │ │  Worker  │ │   Scraper    │
│  (Node.js)   │ │ (Python) │ │   (Python)   │
└──────┬───────┘ └────┬─────┘ └──────┬───────┘
       │              │               │
       └──────────────┼───────────────┘
                      │
        ┌─────────────┼─────────────┐
        ▼             ▼              ▼
┌──────────────┐ ┌─────────┐ ┌───────────┐
│  PostgreSQL  │ │  Redis  │ │ RabbitMQ  │
│  (Primary)   │ │ (Cache) │ │  (Queue)  │
└──────────────┘ └─────────┘ └───────────┘
```

### Key Components

1. **API Layer:**
   - Node.js/Express REST API
   - GraphQL endpoint
   - Authentication/Authorization
   - Rate limiting

2. **Worker Layer:**
   - Background job processing
   - Email sending
   - Report generation

3. **Scraper Layer:**
   - Job board scraping
   - Data extraction
   - Data enrichment

4. **Data Layer:**
   - PostgreSQL (primary database)
   - Redis (cache + sessions)
   - RabbitMQ (message queue)

5. **Infrastructure Layer:**
   - Kubernetes (orchestration)
   - Terraform (infrastructure)
   - ArgoCD (deployments)

---

## Development Workflow

### Git Workflow

```bash
# Create feature branch
git checkout main
git pull origin main
git checkout -b feature/JIRA-123-add-feature

# Make changes
# ... edit files ...

# Commit changes
git add .
git commit -m "feat: add new feature (JIRA-123)"

# Push to remote
git push origin feature/JIRA-123-add-feature

# Create pull request on GitHub
# Wait for CI/CD checks and code review
# Merge when approved
```

### Commit Message Convention

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Example:**
```
feat(api): add user profile endpoint

Implement GET /api/users/:id endpoint to retrieve user profiles.
Includes pagination and filtering support.

Closes JIRA-123
```

### Code Review Process

1. Create pull request with descriptive title and description
2. Link related Jira ticket
3. Ensure all CI checks pass
4. Request review from at least 2 team members
5. Address feedback and update PR
6. Get approval from reviewers
7. Merge using "Squash and merge"

### Testing Requirements

```bash
# Run unit tests
npm test

# Run integration tests
npm run test:integration

# Run E2E tests
npm run test:e2e

# Check code coverage
npm run test:coverage
# Target: > 80% coverage

# Run linter
npm run lint

# Run type checking
npm run type-check
```

---

## Deployment Process

### Environments

| Environment | Purpose | URL | Auto-Deploy |
|-------------|---------|-----|-------------|
| Development | Active development | https://dev.ghost.example.com | Yes (main branch) |
| Staging | Pre-production testing | https://staging.ghost.example.com | Yes (release branch) |
| Production | Live environment | https://ghost.example.com | Manual approval |

### Deployment Steps

1. **Development:**
   ```bash
   # Automatically deploys on merge to main
   git checkout main
   git pull
   # Wait for GitHub Actions to complete
   ```

2. **Staging:**
   ```bash
   # Create release branch
   git checkout -b release/v1.2.3
   git push origin release/v1.2.3
   # Automatically deploys to staging
   ```

3. **Production:**
   ```bash
   # Create release tag
   git tag -a v1.2.3 -m "Release v1.2.3"
   git push origin v1.2.3
   # Manual approval required in GitHub Actions
   # Follow deployment runbook
   ```

---

## Monitoring and Observability

### Dashboards

**Grafana Dashboards:**
- [Ghost API Overview](https://grafana.ghost.example.com/d/ghost-api/overview)
- [Infrastructure Metrics](https://grafana.ghost.example.com/d/infra/overview)
- [Database Performance](https://grafana.ghost.example.com/d/postgres/overview)

**Key Metrics to Watch:**
- API response time (target: p95 < 200ms)
- Error rate (target: < 0.5%)
- Database connections (target: < 80% of max)
- Queue depth (target: < 1000 messages)
- Cache hit rate (target: > 80%)

### Logging

```bash
# View application logs
kubectl logs -f deployment/ghost-api -n ghost

# Search logs in Kibana
# URL: https://kibana.ghost.example.com
# Query: application:ghost AND level:error

# Stream logs locally
stern ghost-api -n ghost
```

### Alerting

Alerts are configured in Prometheus and routed through PagerDuty.

**Alert Channels:**
- **Critical:** PagerDuty (24/7 on-call)
- **Warning:** Slack #platform-alerts
- **Info:** Slack #platform-monitoring

---

## On-Call Rotation

### Schedule

View current rotation: https://ghost.pagerduty.com/schedules

**Rotation:** Weekly (Monday 9 AM - Monday 9 AM EST)

### Responsibilities

- Respond to pages within 15 minutes
- Investigate and resolve incidents
- Escalate if needed
- Document incidents
- Conduct post-mortems for SEV-1/SEV-2 incidents

### Resources

- [Incident Response Runbook](./runbooks/incident-response.md)
- [Deployment Runbook](./runbooks/deployment.md)
- [Disaster Recovery Runbook](./runbooks/disaster-recovery.md)

### On-Call Handoff

Every Monday at 9 AM EST:
1. Review open incidents
2. Share tribal knowledge
3. Highlight recent changes
4. Test PagerDuty notifications
5. Update on-call calendar

---

## Resources and Learning

### Documentation

- [Architecture Documentation](../ARCHITECTURE.md)
- [API Documentation](https://api.ghost.example.com/docs)
- [Infrastructure README](../README.md)
- [Runbooks](./runbooks/)
- [ADRs](./adr/)

### Training Resources

**Internal:**
- [Platform Engineering Onboarding Course](https://learning.ghost.example.com/platform)
- [Security Training](https://learning.ghost.example.com/security)
- [Incident Response Training](https://learning.ghost.example.com/incident)

**External:**
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Terraform Tutorials](https://learn.hashicorp.com/terraform)
- [AWS Well-Architected Framework](https://aws.amazon.com/architecture/well-architected/)

### Recommended Reading

**Books:**
- "Site Reliability Engineering" by Google
- "The Phoenix Project" by Gene Kim
- "Accelerate" by Nicole Forsgren
- "Infrastructure as Code" by Kief Morris

**Blogs:**
- [Ghost Engineering Blog](https://blog.ghost.example.com/engineering)
- [AWS Architecture Blog](https://aws.amazon.com/blogs/architecture/)
- [Kubernetes Blog](https://kubernetes.io/blog/)

---

## Your First Week

### Day 1: Setup and Orientation

- [ ] Complete First Day Setup checklist
- [ ] Set up development environment
- [ ] Clone repositories
- [ ] Run local development environment
- [ ] Meet with buddy
- [ ] Attend team standup

### Day 2: Deep Dive

- [ ] Review architecture documentation
- [ ] Access all tools and services
- [ ] Review recent pull requests
- [ ] Shadow a deployment
- [ ] Attend architecture review meeting

### Day 3: First Contribution

- [ ] Pick a "good first issue" from Jira
- [ ] Create feature branch
- [ ] Make changes
- [ ] Submit pull request
- [ ] Respond to code review feedback

### Day 4: Infrastructure

- [ ] Review Terraform code
- [ ] Understand CI/CD pipelines
- [ ] Review monitoring dashboards
- [ ] Practice incident response procedures
- [ ] Review on-call runbooks

### Day 5: Integration

- [ ] Merge your first PR
- [ ] Deploy to development
- [ ] Verify in monitoring
- [ ] Team retrospective
- [ ] End of week sync with buddy

---

## Getting Help

### Buddy System

Your assigned buddy is your first point of contact for any questions.

**Buddy Responsibilities:**
- Daily check-ins during first week
- Answer questions
- Review first PRs
- Introduce to team members
- Share tribal knowledge

### Ask Questions

**Slack Channels:**
- `#platform-engineering` - Team channel
- `#platform-help` - Technical questions
- `#onboarding` - General onboarding questions

**Office Hours:**
- Team Lead: Tuesday/Thursday 2-3 PM EST
- Senior Engineers: Daily 11 AM - 12 PM EST

### Escalation

If you're blocked or need urgent help:
1. Ask your buddy
2. Post in `#platform-engineering`
3. Ping team lead
4. For emergencies, use PagerDuty

---

## Checklist Summary

### Week 1

- [ ] Complete all Day 1 setup tasks
- [ ] Access all required tools
- [ ] Run local development environment
- [ ] Submit first pull request
- [ ] Attend all team meetings
- [ ] Read all core documentation

### Week 2-4

- [ ] Merge 3+ pull requests
- [ ] Deploy to development environment
- [ ] Shadow on-call engineer
- [ ] Complete security training
- [ ] Present in team meeting

### Month 1-3

- [ ] Take on-call shift (with buddy)
- [ ] Lead feature implementation
- [ ] Present architecture design
- [ ] Mentor new team member
- [ ] Contribute to documentation

---

**Welcome aboard! We're excited to have you on the team!** 🚀

For any questions, reach out to your buddy or the team in `#platform-engineering`.

---

**Document Maintainer:** Platform Engineering Team  
**Last Updated:** 2026-02-03  
**Next Review:** Quarterly
