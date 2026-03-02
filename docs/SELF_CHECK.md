# Self-Check: Iteration 192

## Build Status
- [x] `dotnet build` succeeds
- [x] `npm run build` succeeds

## Test Status
- **Before**: 2541 passed, 0 failed
- **After**: 2541 passed, 0 failed
- [x] No NEW test failures introduced

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `src/Shortboxerr.Core/Ddl/DdlReleaseParser.cs` | Modified - Fix regex + reorder |
| `tests/Shortboxerr.Tests/DdlReleaseParserTests.cs` | Modified - Restore expectations |
| `docs/BACKLOG.md` | Modified - Mark 21.5 done |
| `docs/DECISIONS.md` | Modified - Mark AUDIT-002 fixed |
| `docs/WORKLOG.md` | Modified - Add Iteration 192 |
| `docs/SELF_CHECK.md` | Modified - Iteration 192 status |

## Commits
1. `fix: DdlReleaseParser release group regex for hyphenated names (AUDIT-002)` - pending

## Summary
Fixed AUDIT-002: DdlReleaseParser now correctly extracts hyphenated release groups like "DC-Empire":
1. Changed regex to allow hyphens in capture group
2. Reordered pipeline: extract release group BEFORE publisher extraction
3. `ReleaseGroupPublishers` dictionary lookup now works correctly

Both audit bugs (AUDIT-001 and AUDIT-002) from EPIC 21.3 are now resolved.
