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
| **5052** | Backend | Internal API (proxied by Vite) |

Access the app at **http://localhost:8585**

### Starting Dev Servers
```bash
# Backend (terminal 1)
cd src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5052"

# Frontend (terminal 2)
cd ui && npm run dev
```

### Restarting Servers (STOP-WAIT-VERIFY-START)

**Important:** Vite auto-increments port if 8585 is busy (→ 8586, 8587...). Always verify ports are free!

```bash
# 1. STOP
pkill -9 -f "Shortboxerr.Api" 2>/dev/null || true
pkill -9 -f "vite" 2>/dev/null || true

# 2. WAIT
sleep 3

# 3. VERIFY ports are free
lsof -i :5052 -i :8585 2>/dev/null | grep LISTEN || echo "OK"

# 4. START (backend first, then frontend)
cd src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5052" &
sleep 3
cd ui && npm run dev &

# 5. FINAL CHECK - should show ONLY 5052 and 8585
lsof -i :5052 -i :8585 -i :8586 | grep LISTEN
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
