# Self-Check: Iteration 206

## Build Status
- [x] `dotnet build` succeeds
- [ ] `npm run build` skipped (npm network issues)

## Test Status
- **Before**: 2585 passed, 0 failed
- **After**: 2589 passed, 0 failed (+4 new tests)
- [x] All tests pass

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `src/Shortboxerr.Api/Endpoints/SeriesEndpoints.cs` | Modified - Add sort options |
| `tests/Shortboxerr.Tests/SeriesFilterTests.cs` | Modified - 4 new tests |
| `docs/TEST_BASELINE.md` | Modified - Update to 2589 |
| `scripts/hooks/pre-commit` | Modified - Update TEST_MINIMUM |
| `docs/BACKLOG.md` | Modified - Add 14.21 |
| `docs/WORKLOG.md` | Modified - Add iteration 206 entry |

## Summary
Added series release date sorting (14.21):
1. `latestrelease` sort option (most recent issue release date)
2. `nextrelease` sort option (soonest upcoming issue)
3. Uses StoreDate with fallback to ReleaseDate
4. Added 4 unit tests for sorting functionality
