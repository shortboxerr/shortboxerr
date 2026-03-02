# Self-Check: Iteration 195

## Build Status
- [x] `dotnet build` succeeds
- [x] `npm run build` succeeds

## Test Status
- **Before**: 2541 passed, 0 failed (1 flaky failure on first run)
- **After**: 2541 passed, 0 failed
- [x] No NEW test failures introduced

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `ui/src/pages/PullListPage.tsx` | Modified - Parallel API calls |
| `ui/src/pages/ActivityPage.tsx` | Modified - Pause polling when hidden |
| `ui/src/pages/LogsPage.tsx` | Modified - Pause polling when hidden |
| `ui/src/pages/SettingsPage.tsx` | Modified - Pause polling when hidden |
| `docs/BACKLOG.md` | Modified - Update 20.7 status |
| `docs/WORKLOG.md` | Modified - Add Iteration 195 |
| `docs/SELF_CHECK.md` | Modified - Iteration 195 status |

## Commits
1. `feat(ui): optimize API call patterns (EPIC 20.7)` - pending

## Summary
Optimized frontend API call patterns:
1. **Parallel fetching**: PullListPage now fetches 4 weeks in parallel via `Promise.all`
2. **Smart polling**: All refetchInterval queries pause when tab not visible
3. **Deferred**: Server-side pagination for SeriesDetailPage (larger scope, separate iteration)
