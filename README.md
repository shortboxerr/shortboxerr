# Shortboxerr

Arr-like comic automation platform with Mylar3 behavioral parity and first-class collected editions.

## Development (Dev Container)
Open this repo in Cursor and use the Dev Container:
- Command Palette → "Dev Containers: Reopen in Container"
All development should happen in-container (dotnet, node, etc.).

## Dev Server Ports

| Port | Service | Description |
|------|---------|-------------|
| **8585** | Frontend | User-facing (Vite dev server) |
| **5000** | Backend | Internal API (proxied by Vite) |

Access the app at **http://localhost:8585**

### Starting Dev Servers
```bash
# Backend (terminal 1)
cd src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5000"

# Frontend (terminal 2)
cd ui && npm run dev
```

### Restarting Servers
```bash
# Kill app servers only
pkill -f "Shortboxerr" 2>/dev/null || true
pkill -f "vite" 2>/dev/null || true
```

> **Warning:** Never run `pkill -f "node"` - it kills Cursor's internal server.

## Dev Commands (inside container)
- dotnet build
- dotnet test
- dotnet run --project src/Shortboxerr.Api

## Docker (runtime)
- docker compose up --build

## Docs
- docs/PLAN.md is source of truth
- docs/BACKLOG.md drives implementation
- docs/ITERATION_PROTOCOL.md defines autonomous iteration rules
- .cursor/rules.md enforces Cursor behavior and git commit discipline
