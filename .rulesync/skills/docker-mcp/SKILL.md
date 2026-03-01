---
name: docker-mcp
description:
  'Docker MCP Server integration for container management, image builds, and Docker Compose operations. Triggers on:
  docker mcp, container, image, dockerfile, docker compose, build, registry, volume, network.'
license: MIT
targets: ['*']
tags: ['docker', 'mcp', 'container', 'skill', 'devops/docker']
version: '0.0.1'
author: 'dotnet-harness'
invocable: true
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
---

# Docker MCP

Manage containers, images, networks, and volumes via the Model Context Protocol (MCP).

## Overview

The Docker MCP Server enables AI agents to interact with Docker programmatically. It provides tools for container
lifecycle management, image building, and Docker Compose operations.

## Configuration

The MCP server is configured in `.rulesync/mcp.json`:

```json
{
  "mcpServers": {
    "docker-mcp": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "-i", "--rm", "-v", "/var/run/docker.sock:/var/run/docker.sock", "mcp/docker"],
      "env": {}
    }
  }
}
```

> **Prerequisites:** Docker daemon must be running. On Windows/Mac, Docker Desktop is required.

## Capabilities

### Container Management

- List running and stopped containers
- Create and start containers
- Stop, start, restart containers
- Remove containers
- Get container logs
- Execute commands in running containers
- Inspect container configuration

### Image Operations

- List local images
- Pull images from registries
- Build images from Dockerfiles
- Tag and push images
- Remove images
- Inspect image layers

### Docker Compose

- Start services with `docker-compose up`
- Stop services with `docker-compose down`
- Scale services
- View service logs
- Build Compose services

### Networks

- List networks
- Create custom networks
- Connect/disconnect containers
- Inspect network configuration
- Remove networks

### Volumes

- List volumes
- Create named volumes
- Mount volumes in containers
- Backup and restore volume data
- Remove volumes

### Registry Operations

- Login to registries
- Push images to registries
- Pull images from registries
- Search public registries

## Security Considerations

- Container runs with access to Docker socket (privileged access)
- Use named volumes over bind mounts for data persistence
- Scan images for vulnerabilities before deployment
- Use multi-stage builds to minimize attack surface
- Never commit sensitive data in Dockerfiles

## Best Practices

- Use specific image tags instead of `latest`
- Implement health checks in containers
- Use `.dockerignore` to reduce build context
- Optimize layer caching with proper Dockerfile ordering
- Clean up unused images and volumes regularly

## Use Cases

- Automate container deployments
- Build and push images in CI/CD pipelines
- Debug running containers
- Manage multi-container applications
- Inspect resource usage

## References

- [Docker MCP Server](https://github.com/anthropics/anthropic-cookbook/tree/main/mcp/docker)
- [Docker Documentation](https://docs.docker.com/)
- [Dockerfile Best Practices](https://docs.docker.com/develop/dev-best-practices/)
