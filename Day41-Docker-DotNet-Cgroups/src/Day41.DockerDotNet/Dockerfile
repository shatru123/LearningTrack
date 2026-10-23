# ==========================================
# Multi-stage Dockerfile for Production .NET 8 Service
# Features: Layer Caching, Non-Root Chiseled Runtime, Cgroup GC Auto-tuning
# ==========================================

# Stage 1: Build & Restore with Layer Caching
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Step 1: Copy only project file to maximize Docker layer cache hit on dependencies
COPY Day41.DockerDotNet.csproj .
RUN dotnet restore Day41.DockerDotNet.csproj

# Step 2: Copy remaining source and build
COPY . .
RUN dotnet build Day41.DockerDotNet.csproj -c Release --no-restore

# Stage 2: Publish optimized binary
FROM build AS publish
RUN dotnet publish Day41.DockerDotNet.csproj -c Release -o /app/publish --no-build /p:UseAppHost=false

# Stage 3: Ultra-lightweight secure chiseled runtime (Zero shell, zero root)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS final
WORKDIR /app

# Switch to standard unprivileged non-root user (UID 1654: app)
USER app

# Production container memory & GC optimizations
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=1 \
    DOTNET_GCHeapHardLimitPercent=75 \
    ASPNETCORE_URLS=http://+:8080

COPY --from=publish /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Day41.DockerDotNet.dll"]
