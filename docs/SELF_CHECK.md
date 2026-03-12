# Self-Check: Iteration 223

## Build Status
- [x] `dotnet build` succeeds (dev container)
- [ ] `npm run build` (not required for this iteration)

## Test Status
- **Before**: 2608 passed, 0 failed
- **After**: 2608 passed, 0 failed
- [x] No NEW test failures introduced

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `Persistence/Migrations/20260311240000_AddSeriesFts5.cs` | new |
| `Persistence/SeriesFtsHelper.cs` | new |
| `SeriesEndpoints.cs` | FTS + LIKE fallback |
| `docs/BACKLOG.md` | 14.17 series FTS |
| `docs/WORKLOG.md` | Iteration 223 |
| `docs/SELF_CHECK.md` | Iteration 223 |

## Commits
1. `feat(search): add SQLite FTS5 for series list search (14.17)` – (pending)

## Summary
Series list search uses FTS5 when SQLite and Series_fts exists; falls back to title/sort title LIKE when FTS returns no IDs or table is missing. Migration and triggers keep FTS in sync.
