# Base image with .NET 10.0 ASP.NET Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

# Build application
RUN dotnet build "src/Ghost.WebApi/Ghost.WebApi.csproj" -c Release -o /app/build --no-restore

# Publish application
FROM build AS publish
RUN dotnet publish "src/Ghost.WebApi/Ghost.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Install Playwright browsers (after publish so Microsoft.Playwright is built)
FROM publish AS playwright
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates fonts-liberation libasound2t64 libatk-bridge2.0-0 \
    libatk1.0-0 libc6 libcairo2 libcups2 libdbus-1-3 libexpat1 libfontconfig1 \
    libgbm1 libgcc1 libglib2.0-0 libgtk-3-0 libnspr4 libnss3 libpango-1.0-0 \
    libpangocairo-1.0-0 libstdc++6 libx11-6 libx11-xcb1 libxcb1 libxcomposite1 \
    libxcursor1 libxdamage1 libxext6 libxfixes3 libxi6 libxrandr2 libxrender1 \
    libxss1 libxtst6 libxkbcommon0 libxshmfence1 && \
    rm -rf /var/lib/apt/lists/*

# Install Playwright CLI and browsers (project is now built with Microsoft.Playwright)
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN playwright install chromium

# Final stage
FROM base AS final
WORKDIR /app

# Install Playwright runtime dependencies (required for Chromium to run)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates fonts-liberation libasound2t64 libatk-bridge2.0-0 \
    libatk1.0-0 libc6 libcairo2 libcups2 libdbus-1-3 libexpat1 libfontconfig1 \
    libgbm1 libgcc1 libglib2.0-0 libgtk-3-0 libnspr4 libnss3 libpango-1.0-0 \
    libpangocairo-1.0-0 libstdc++6 libx11-6 libx11-xcb1 libxcb1 libxcomposite1 \
    libxcursor1 libxdamage1 libxext6 libxfixes3 libxi6 libxrandr2 libxrender1 \
    libxss1 libxtst6 libxkbcommon0 libxshmfence1 curl && \
    rm -rf /var/lib/apt/lists/*

# Create directories for runtime data
RUN mkdir -p /var/ghost/cache /var/ghost/dlq && \
    chmod 755 /var/ghost /var/ghost/cache /var/ghost/dlq

# Copy published application
COPY --from=publish /app/publish .

# Copy Playwright browsers
COPY --from=playwright /root/.cache/ms-playwright /root/.cache/ms-playwright

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Ghost.WebApi.dll"]
