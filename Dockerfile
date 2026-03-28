# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Shortboxerr.sln ./
COPY src/Shortboxerr.Api/Shortboxerr.Api.csproj src/Shortboxerr.Api/
COPY src/Shortboxerr.Core/Shortboxerr.Core.csproj src/Shortboxerr.Core/
COPY src/Shortboxerr.Infrastructure/Shortboxerr.Infrastructure.csproj src/Shortboxerr.Infrastructure/

# Restore dependencies for the published app only (solution includes tests; test project is not copied into this image).
RUN dotnet restore src/Shortboxerr.Api/Shortboxerr.Api.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/Shortboxerr.Api/Shortboxerr.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r shortboxerr && useradd -r -g shortboxerr shortboxerr

# Create data directory
RUN mkdir -p /data && chown -R shortboxerr:shortboxerr /data

# Copy published app
COPY --from=build /app/publish .

# Set ownership
RUN chown -R shortboxerr:shortboxerr /app

# Switch to non-root user
USER shortboxerr

# Environment variables (can be overridden)
ENV ASPNETCORE_URLS=http://0.0.0.0:8585
ENV SHORTBOXERR_DB="Data Source=/data/shortboxerr.db"
ENV SHORTBOXERR_LIBRARY_ROOT=/data/library
ENV SHORTBOXERR_STAGING=/data/staging
ENV SHORTBOXERR_FAILED=/data/failed

# Expose port
EXPOSE 8585

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8585/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Shortboxerr.Api.dll"]

