# Iteration Prompt

Run the next iteration strictly per `docs/ITERATION_PROTOCOL.md`. Do not ask questions. Pull the next READY items from `docs/BACKLOG.md`. Implement a vertical slice with code+tests+docs. Update `docs/WORKLOG.md`, `docs/BACKLOG.md`, `docs/SELF_CHECK.md`. If blocked, assume defaults and log them in `docs/ASSUMPTIONS.md`. Reference `docs/METRON_API_DOC.yaml` for Metron API details. Respect `docs/SECURITY.md` requirements. Commit after every logical breakpoint.

**Git:** Integrate via **pull request to `main`** (not direct push). Workflow steps: `docs/CONTRIBUTING.md` → *Typical workflow (merge to `main`)*.

**Repeat:** Re-read this file (CONTINUE.md) after every iteration. Continue until no READY or implementable backlog items remain. Do not stop until the backlog is exhausted (all items either done or explicitly deferred with reason in BACKLOG).

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

**CRITICAL**: ALL tests must pass before committing. If any tests fail (new or pre-existing), fix them FIRST, then commit the fix separately before proceeding with other work. Never commit code while tests are failing.

---

## Fixing Failing Tests (DO NOT MASK BUGS)

When a test fails, determine the root cause before changing anything:

### 1. Classify the Failure

| Type | Description | Correct Action |
|------|-------------|----------------|
| **Code Bug** | Test is correct, code is wrong | Fix the code, not the test |
| **Test Bug** | Test setup is broken (mocks, fixtures, isolation) | Fix the test infrastructure |
| **Stale Test** | Test expects old behavior after intentional change | Update test + document the intentional change |
| **Missing Feature** | Test expects unimplemented functionality | Mark test as `[Trait("Category", "NotImplemented")]` or remove |

### 2. Red Flags (STOP and investigate)

- Changing assertions to match "current behavior" without understanding why
- Removing test cases instead of fixing them
- Multiple tests failing for the same logical reason (indicates code bug)
- Test was passing before your changes (you broke it)

### 3. Required Documentation

When fixing a failing test, document in your commit message:
- **What** was failing
- **Why** it was failing (root cause)
- **Classification** (code bug, test bug, stale test, or missing feature)
- **What you changed** and why that's the correct fix

### 4. Example Commit Messages

```
# GOOD - Code bug fixed
fix: RemoveFromHistoryAsync now returns true when item removed from session

RemoveFromHistoryAsync was returning false even when successfully removing
from session history. Root cause: only checked persisted history for return value.
Classification: Code bug - test correctly caught the issue.

# GOOD - Test infrastructure bug
fix(tests): MetronClientTests mock setup for IServiceProvider

Mocks were not properly chaining IServiceProvider.GetService calls.
Classification: Test bug - mock setup was incorrect, not the code.

# BAD - Masking potential bug
fix(tests): update assertion to match current behavior

Changed expected value from "DC Comics" to "DC" to make test pass.
```

---

## Server Management

**Ports (DO NOT CHANGE):**
- **8585** - Frontend (Vite dev server, bound to 0.0.0.0)
- **5052** - Backend API (proxied by Vite)

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
lsof -i :5052 -i :8585 2>/dev/null | grep LISTEN && echo "ERROR: Ports still in use!" || echo "Ports are free"

# 4. START Backend first, wait for ready
cd /workspaces/shortboxerr/src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5052" &
sleep 3

# 5. START Frontend
cd /workspaces/shortboxerr/ui && npm run dev &

# 6. FINAL VERIFY - Only these two ports should be in use
lsof -i :5052 -i :8585 -i :8586 2>/dev/null | grep LISTEN
# Expected: 5052 (Shortboxerr) and 8585 (node) ONLY
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
