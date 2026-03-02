# Iteration Prompt

Run the next iteration strictly per `docs/ITERATION_PROTOCOL.md`. Do not ask questions. Pull the next READY items from `docs/BACKLOG.md`. Implement a vertical slice with code+tests+docs. Update `docs/WORKLOG.md`, `docs/BACKLOG.md`, `docs/SELF_CHECK.md`. If blocked, assume defaults and log them in `docs/ASSUMPTIONS.md`. Reference `docs/METRON_API_DOC.yaml` for Metron API details. Respect `docs/SECURITY.md` requirements. Commit after every logical breakpoint.

---

## Quality Gates (MANDATORY)

Before starting work, check current state:
```bash
# 1. Build check
dotnet build --verbosity quiet

# 2. Test baseline - record current failures
dotnet test --no-build --verbosity quiet 2>&1 | tail -5
```

After completing work, verify no regression:
```bash
# 1. Build must pass
dotnet build --verbosity quiet

# 2. Frontend must compile
cd ui && npm run build

# 3. Tests must not regress (no NEW failures)
dotnet test --no-build --verbosity quiet

# 4. Lints should be clean on changed files
# (ReadLints tool on modified files)
```

**CRITICAL**: If you introduce NEW test failures, fix them before committing. Pre-existing failures are acceptable but must not increase.

---

## Server Management

**Ports (DO NOT CHANGE):**
- **8585** - Frontend (Vite dev server, bound to 0.0.0.0)
- **5000** - Backend API (proxied by Vite)

**Restart servers after each commit (STOP-WAIT-VERIFY-START):**

IMPORTANT: Vite auto-increments port if busy (8585 → 8586 → 8587...).
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
sleep 3

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

---

## SELF_CHECK.md Template

Update `docs/SELF_CHECK.md` with:
```markdown
# Self-Check: Iteration N

## Build Status
- [ ] `dotnet build` succeeds
- [ ] `npm run build` succeeds

## Test Status
- **Before**: X passed, Y failed
- **After**: X passed, Y failed
- [ ] No NEW test failures introduced

## Lint Status
- [ ] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| ... | ... |

## Commits
1. `type: message` - hash

## Summary
...
```

---

## When to Write Tests

Write tests when:
- Adding new service/domain logic
- Adding new API endpoints
- Fixing a bug (regression test)
- Adding parsing/transformation logic

Skip tests when:
- Pure UI changes (visual only)
- Documentation updates
- Configuration changes
