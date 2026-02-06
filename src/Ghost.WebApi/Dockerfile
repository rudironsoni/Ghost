# Ubuntu-based .NET 10.0 ASP.NET Runtime (Playwright officially supports Ubuntu)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
EXPOSE 8080
# Port 8081 changed to 31337 for security
EXPOSE 31337

# SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Copy solution and package props first for better layer caching
COPY ["Directory.Packages.props", "."]
COPY ["Ghost.sln", "."]

# Copy all project files
COPY ["src/Ghost.WebApi/Ghost.WebApi.csproj", "src/Ghost.WebApi/"]
COPY ["src/Core/Ghost/Ghost.csproj", "src/Core/Ghost/"]
COPY ["src/Hosting/Ghost.Hosting/Ghost.Hosting.csproj", "src/Hosting/Ghost.Hosting/"]
COPY ["src/Contracts/Ghost.Contracts/Ghost.Contracts.csproj", "src/Contracts/Ghost.Contracts/"]
COPY ["src/Contracts/Ghost.Contracts.Jobs/Ghost.Contracts.Jobs.csproj", "src/Contracts/Ghost.Contracts.Jobs/"]
COPY ["src/Contracts/Ghost.Contracts.Social/Ghost.Contracts.Social.csproj", "src/Contracts/Ghost.Contracts.Social/"]
COPY ["src/Contracts/Ghost.Contracts.News/Ghost.Contracts.News.csproj", "src/Contracts/Ghost.Contracts.News/"]
COPY ["src/Contracts/Ghost.Contracts.Inference/Ghost.Contracts.Inference.csproj", "src/Contracts/Ghost.Contracts.Inference/"]
COPY ["src/Platforms/Ghost.Platform.Anthropic/Ghost.Platform.Anthropic.csproj", "src/Platforms/Ghost.Platform.Anthropic/"]
COPY ["src/Platforms/Ghost.Platform.Google/Ghost.Platform.Google.csproj", "src/Platforms/Ghost.Platform.Google/"]
COPY ["src/Platforms/Ghost.Platform.LinkedIn/Ghost.Platform.LinkedIn.csproj", "src/Platforms/Ghost.Platform.LinkedIn/"]
COPY ["src/Platforms/Ghost.Platform.OpenAI/Ghost.Platform.OpenAI.csproj", "src/Platforms/Ghost.Platform.OpenAI/"]
COPY ["src/Platforms/Ghost.Platform.Indeed/Ghost.Platform.Indeed.csproj", "src/Platforms/Ghost.Platform.Indeed/"]
COPY ["src/Platforms/Ghost.Platform.Glassdoor/Ghost.Platform.Glassdoor.csproj", "src/Platforms/Ghost.Platform.Glassdoor/"]
COPY ["src/Platforms/Ghost.Platform.InfoJobs/Ghost.Platform.InfoJobs.csproj", "src/Platforms/Ghost.Platform.InfoJobs/"]

# Copy full source
COPY . .

# Restore dependencies
RUN dotnet restore "src/Ghost.WebApi/Ghost.WebApi.csproj"

# Build stage - creates .playwright directory
FROM build AS built
RUN dotnet build "src/Ghost.WebApi/Ghost.WebApi.csproj" -c Release

# Playwright stage - install browsers
FROM built AS playwright
# Install Playwright CLI tool
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="${PATH}:/root/.dotnet/tools"
# Install Chromium browsers with system dependencies (CLI finds .playwright in artifacts)
RUN playwright install chromium --with-deps

# Publish stage
FROM built AS publish
RUN dotnet publish "src/Ghost.WebApi/Ghost.WebApi.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-build

# Final stage
FROM base AS final
WORKDIR /app

# Install Playwright runtime dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates fonts-liberation libasound2t64 libatk-bridge2.0-0 \
    libatk1.0-0 libc6 libcairo2 libcups2 libdbus-1-3 libexpat1 libfontconfig1 \
    libgbm1 libgcc1 libglib2.0-0 libgtk-3-0 libnspr4 libnss3 libpango-1.0-0 \
    libpangocairo-1.0-0 libstdc++6 libx11-6 libx11-xcb1 libxcb1 libxcomposite1 \
    libxcursor1 libxdamage1 libxext6 libxfixes3 libxi6 libxrandr2 libxrender1 \
    libxss1 libxtst6 libxkbcommon0 libxshmfence1 wget && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user with home directory
RUN adduser --disabled-password --gecos "" ghost-app-user && \
    mkdir -p /var/ghost/cache /var/ghost/dlq /home/ghost-app-user/.cache

# Copy published application
COPY --from=publish --chown=ghost-app-user:ghost-app-user /app/publish .

# Copy Playwright driver from build stage (it's in Ghost.Hosting output)
COPY --from=playwright --chown=ghost-app-user:ghost-app-user /src/artifacts/bin/Ghost.Hosting/Release/net10.0/.playwright /app/.playwright

# Copy Playwright browsers to user's home directory (where Playwright looks for them)
COPY --from=playwright --chown=ghost-app-user:ghost-app-user /root/.cache/ms-playwright /home/ghost-app-user/.cache/ms-playwright

# Set ownership
RUN chown -R ghost-app-user:ghost-app-user /app /var/ghost /home/ghost-app-user

# Switch to non-root user
USER ghost-app-user

# Health check using wget
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Ghost.WebApi.dll"]
