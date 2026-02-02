FROM mcr.microsoft.com/devcontainers/dotnet:8.0

# Optional: node for future UI work (keeps it ready for EPIC 5)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates git \
  && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
  && apt-get install -y --no-install-recommends nodejs \
  && apt-get clean && rm -rf /var/lib/apt/lists/*
