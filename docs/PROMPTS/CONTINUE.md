Run the next iteration strictly per docs/ITERATION_PROTOCOL.md. Do not ask questions. Pull the next READY items from docs/BACKLOG.md. Implement a vertical slice with code+tests+docs. Update docs/WORKLOG.md, docs/BACKLOG.md, docs/SELF_CHECK.md. If blocked, assume defaults and log them in docs/ASSUMPTIONS.md. Commit after every logical breakpoint.

## Server Management

**Ports (DO NOT CHANGE):**
- **8585** - Frontend (user-facing, Vite dev server)
- **5000** - Backend API (internal, proxied by Vite)

**Restart servers after each commit:**
```bash
# Kill app servers only (NOT all node processes - that kills Cursor)
pkill -f "Shortboxerr" 2>/dev/null || true
pkill -f "vite" 2>/dev/null || true
sleep 2

# Start backend (port 5000)
cd /workspaces/shortboxerr/src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5000"

# Start frontend (port 8585)
cd /workspaces/shortboxerr/ui && npm run dev
```

**NEVER run:**
- `pkill -f "node"` - kills Cursor's internal server
- `pkill -f "dotnet"` - may kill other dotnet processes

Ensure there's a single instance of the frontend and backend servers.
