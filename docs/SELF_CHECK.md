# Self-Check: Iteration 198

## Build Status
- [x] `dotnet build` succeeds
- [x] `npm run build` succeeds

## Test Status
- **Before**: 2538 passed, 3 failed (pre-existing network tests)
- **After**: 2538 passed, 3 failed (same pre-existing)
- [x] No NEW test failures introduced

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `ui/vite.config.ts` | Modified - Add version injection |
| `ui/src/vite-env.d.ts` | Modified - Add global declarations |
| `ui/src/components/Layout.tsx` | Modified - Add version display |
| `ui/src/App.css` | Modified - Add footer styles |
| `src/Shortboxerr.Api/Endpoints/SystemEndpoints.cs` | Modified - Add commit/branch |
| `docs/BACKLOG.md` | Modified - Mark 14.14, 14.15 complete |
| `docs/WORKLOG.md` | Modified - Add iteration 198 entry |

## Commits
1. `feat: add build-time version injection and enhance system status` - pending

## Summary
Implemented frontend build-time version embedding (14.14) and enhanced system status endpoint (14.15):
1. Frontend shows version in sidebar footer with commit/branch tooltip
2. Vite injects `__APP_VERSION__`, `__COMMIT_HASH__`, `__BUILD_TIME__`, `__BRANCH__` at build time
3. Backend `/api/v1/system/status` now includes commit hash and branch
4. Git info fetched dynamically at build/startup (graceful fallback if unavailable)
