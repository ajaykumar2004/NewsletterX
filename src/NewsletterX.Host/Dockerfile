# =============================================================================
# NewsletterX Dockerfile - Multi-Stage Build
# =============================================================================
# ARCHITECTURAL DECISION: Multi-Stage Build
# ─────────────────────────────────────────
# Stage 1 (build): Full SDK, compiles code, produces DLLs
# Stage 2 (publish): Full SDK, creates optimized publish output
# Stage 3 (final): Runtime-only image, minimal attack surface
#
# WHY MULTI-STAGE?
# - Final image is small (~200MB vs ~700MB with SDK)
# - No SDK/build tools in production = smaller attack surface
# - Build artifacts don't leak into production image
#
# TRANSFERABLE PATTERN: This Dockerfile structure works for any ASP.NET Core API.
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Build
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching optimization)
# If only code changes, NuGet restore is cached
COPY ["NewsletterX.sln", "./"]
COPY ["src/NewsletterX.Host/NewsletterX.Host.csproj", "src/NewsletterX.Host/"]
COPY ["src/NewsletterX.Api/NewsletterX.Api.csproj", "src/NewsletterX.Api/"]
COPY ["src/NewsletterX.Providers/NewsletterX.Providers.csproj", "src/NewsletterX.Providers/"]
COPY ["src/NewsletterX.Repositories/NewsletterX.Repositories.csproj", "src/NewsletterX.Repositories/"]
COPY ["src/NewsletterX.EntityModels/NewsletterX.EntityModels.csproj", "src/NewsletterX.EntityModels/"]
COPY ["src/NewsletterX.ContractModels/NewsletterX.ContractModels.csproj", "src/NewsletterX.ContractModels/"]
COPY ["src/NewsletterX.Types/NewsletterX.Types.csproj", "src/NewsletterX.Types/"]

# Restore NuGet packages (cached if .csproj files unchanged)
RUN dotnet restore "NewsletterX.sln"

# Copy all source code
COPY . .

# Build in Release mode
WORKDIR "/src/src/NewsletterX.Host"
RUN dotnet build "NewsletterX.Host.csproj" -c Release -o /app/build

# -----------------------------------------------------------------------------
# Stage 2: Publish
# -----------------------------------------------------------------------------
FROM build AS publish
RUN dotnet publish "NewsletterX.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 3: Final Runtime Image
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
# SECURITY: Running as root in containers is a bad practice
# - Limits damage if container is compromised
# - Required by many container security policies
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy published output from build stage
COPY --from=publish /app/publish .

# Expose port (Kestrel default)
EXPOSE 8080

# Health check for container orchestrators (ECS, Kubernetes)
# Checks if the /health endpoint responds with 200 OK
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "NewsletterX.Host.dll"]

# =============================================================================
# BUILD & RUN:
# =============================================================================
# Build:
#   docker build -t newsletterx-api -f src/NewsletterX.Host/Dockerfile .
#
# Run (standalone):
#   docker run -p 8080:8080 --name newsletterx newsletterx-api
#
# Run (with docker-compose network):
#   docker run -p 8080:8080 --network deployment_default newsletterx-api
#
# Environment variables:
#   docker run -e "ConnectionStrings__DefaultConnection=..." newsletterx-api
# =============================================================================

