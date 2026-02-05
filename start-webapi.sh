#!/bin/bash
export Ghost__Extensions__LinkedIn__Enabled=true
export Ghost__Extensions__Google__Enabled=true
export Ghost__Extensions__Google__Jobs__Enabled=true
export Ghost__Extensions__Glassdoor__Enabled=true
export Ghost__Extensions__Indeed__Enabled=false
export Ghost__Extensions__InfoJobs__Enabled=false
export Ghost__Kernel__Headless=true
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://localhost:8080

cd /home/rrj/src/github/rudironsoni/Ghost
dotnet run --project src/Ghost.WebApi/Ghost.WebApi.csproj --no-build --configuration Release
