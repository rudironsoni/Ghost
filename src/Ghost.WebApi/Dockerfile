# Base image with Playwright dependencies and .NET Runtime
# We use the official Playwright dotnet image which matches the Patchright version ideally, 
# or a recent one. Assuming v1.56.0 exists or we use a recent stable tag.
FROM mcr.microsoft.com/playwright/dotnet:v1.56.0-noble AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "."]
# Copy project files
COPY ["src/Ghost.WebApi/Ghost.WebApi.csproj", "src/Ghost.WebApi/"]
COPY ["src/Core/Ghost/Ghost.csproj", "src/Core/Ghost/"]
COPY ["src/Hosting/Ghost.Hosting/Ghost.Hosting.csproj", "src/Hosting/Ghost.Hosting/"]
COPY ["src/Contracts/Ghost.Contracts/Ghost.Contracts.csproj", "src/Contracts/Ghost.Contracts/"]
COPY ["src/Contracts/Ghost.Contracts.Jobs/Ghost.Contracts.Jobs.csproj", "src/Contracts/Ghost.Contracts.Jobs/"]
COPY ["src/Contracts/Ghost.Contracts.Social/Ghost.Contracts.Social.csproj", "src/Contracts/Ghost.Contracts.Social/"]
COPY ["src/Contracts/Ghost.Contracts.News/Ghost.Contracts.News.csproj", "src/Contracts/Ghost.Contracts.News/"]
COPY ["src/Contracts/Ghost.Contracts.Inference/Ghost.Contracts.Inference.csproj", "src/Contracts/Ghost.Contracts.Inference/"]
# Platform projects
COPY ["src/Platforms/Ghost.Platform.LinkedIn/Ghost.Platform.LinkedIn.csproj", "src/Platforms/Ghost.Platform.LinkedIn/"]
COPY ["src/Platforms/Ghost.Platform.Google/Ghost.Platform.Google.csproj", "src/Platforms/Ghost.Platform.Google/"]
COPY ["src/Platforms/Ghost.Platform.OpenAI/Ghost.Platform.OpenAI.csproj", "src/Platforms/Ghost.Platform.OpenAI/"]
COPY ["src/Platforms/Ghost.Platform.Anthropic/Ghost.Platform.Anthropic.csproj", "src/Platforms/Ghost.Platform.Anthropic/"]

RUN dotnet restore "src/Ghost.WebApi/Ghost.WebApi.csproj"

# Copy full source
COPY . .
WORKDIR "/src/src/Ghost.WebApi"
RUN dotnet build "Ghost.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ghost.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Ensure browsers are installed (Patchright might need its own install)
# Attempt to run playwright install in case the base image doesn't match perfectly
# or if Patchright needs to download its own modified browsers.
# RUN pwsh playwright.ps1 install --with-deps
# For now, we assume the base image provides necessary dependencies.

ENTRYPOINT ["dotnet", "Ghost.WebApi.dll"]
