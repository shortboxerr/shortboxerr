# Self-Check: Iteration 191

## Build Status
- [x] `dotnet build` succeeds
- [x] `npm run build` succeeds

## Test Status
- **Before**: 2529 passed, 0 failed
- **After**: 2541 passed, 0 failed (+12 restored tests)
- [x] No NEW test failures introduced

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `src/Shortboxerr.Infrastructure/Ddl/GetComicsAdapter.cs` | Modified - Add 6 methods |
| `tests/Shortboxerr.Tests/GetComicsAdapterTests.cs` | Modified - Add 12 tests |
| `docs/BACKLOG.md` | Modified - Mark 21.4 done |
| `docs/WORKLOG.md` | Modified - Add Iteration 191 |
| `docs/DECISIONS.md` | Modified - Mark AUDIT-001 fixed |
| `docs/SELF_CHECK.md` | Modified - Iteration 191 status |

## Commits
1. `fix: restore GetComicsAdapter RSS/category methods (AUDIT-001)` - pending

## Summary
Fixed AUDIT-001: Restored 6 methods lost during GetComicsAdapter V2 rename:
- `GetRssFeedAsync`, `GetCategoryAsync`, `GetCategoryRssFeedAsync`
- `GetPublisherRssFeedAsync`, `GetPublisherAsync`, `GetAvailableCategories`

Restored 12 deleted tests. GetComicsAdapter now has full feature parity with ReadComicOnlineAdapter.
