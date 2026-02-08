# Ghost Worker Dockerfile
# Multi-stage build for optimized container size

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Ghost.sln .
COPY src/Ghost.Worker/Ghost.Worker.csproj src/Ghost.Worker/
COPY src/Core/Ghost/Ghost.csproj src/Core/Ghost/
COPY src/Contracts/Ghost.Contracts/Ghost.Contracts.csproj src/Contracts/Ghost.Contracts/
COPY src/Contracts/Ghost.Contracts.Jobs/Ghost.Contracts.Jobs.csproj src/Contracts/Ghost.Contracts.Jobs/

# Restore dependencies
RUN dotnet restore src/Ghost.Worker/Ghost.Worker.csproj

# Copy all source code
COPY . .

# Build and publish the worker
WORKDIR /src/src/Ghost.Worker
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install Playwright dependencies and browsers
# This is required for headless browser automation
RUN apt-get update && apt-get install -y \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libdbus-1-3 \
    libxkbcommon0 \
    libatspi2.0-0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    libxshmfence1 \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Install Playwright browsers
ENV PLAYWRIGHT_BROWSERS_PATH=/app/browser-data
RUN dotnet tool install --global Microsoft.Playwright.CLI && \
    export PATH="$PATH:/root/.dotnet/tools" && \
    playwright install chromium --with-deps

# Create non-root user (optional - Playwright often needs root for full features)
# RUN useradd -m -u 1000 ghostworker
# USER ghostworker

# Health check endpoint (if we add HTTP health checks later)
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD pgrep -f Ghost.Worker || exit 1

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Entry point
ENTRYPOINT ["dotnet", "Ghost.Worker.dll"]
