Run the next iteration strictly per docs/ITERATION_PROTOCOL.md. Do not ask questions. Pull the next READY items from docs/BACKLOG.md. Implement a vertical slice with code+tests+docs. Update docs/WORKLOG.md, docs/BACKLOG.md, docs/SELF_CHECK.md. If blocked, assume defaults and log them in docs/ASSUMPTIONS.md. Commit after every logical breakpoint.

## Server Management

**Ports (DO NOT CHANGE):**
- **8585** - Frontend (user-facing, Vite dev server)
- **5000** - Backend API (internal, proxied by Vite)

**Restart servers after each commit (STOP-WAIT-VERIFY-START):**

IMPORTANT: Vite auto-increments port if its configured port is busy (8585 → 8586 → 8587...).
Always verify ports are FREE before starting servers.

```bash
# 1. STOP - Kill app servers only
pkill -9 -f "Shortboxerr.Api" 2>/dev/null || true
pkill -9 -f "vite" 2>/dev/null || true

# 2. WAIT - Give processes time to release ports
sleep 3

# 3. VERIFY - Confirm ports are free (CRITICAL!)
lsof -i :5000 -i :8585 2>/dev/null | grep LISTEN && echo "ERROR: Ports still in use!" || echo "Ports are free"

# 4. START Backend first, wait for ready
cd /workspaces/shortboxerr/src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5000" &
sleep 3  # Wait for backend to bind

# 5. START Frontend
cd /workspaces/shortboxerr/ui && npm run dev &

# 6. FINAL VERIFY - Only these two ports should be in use
lsof -i :5000 -i :8585 -i :8586 2>/dev/null | grep LISTEN
# Expected: 5000 (Shortboxerr) and 8585 (node) ONLY
# If 8586 appears, something went wrong - repeat from step 1
```

**NEVER run:**
- `pkill -f "node"` - kills Cursor's internal server
- `pkill -f "dotnet"` - may kill other dotnet processes

Ensure there's exactly ONE instance of frontend (8585) and ONE instance of backend (5000).
