# Self-Check: Iteration 227

## Build Status
- [x] `dotnet build` succeeds
- [x] `npm run build` succeeds

## Test Status
- **Before**: widespread failures (`401` on API routes; `SettingsEndpointTests` races after regenerate)
- **After**: 2610 passed, 0 failed
- [x] No NEW test failures introduced

## Lint Status
- [x] No new lint errors on changed files

## Files Changed
| File | Type |
|------|------|
| `tests/Shortboxerr.Tests/PullListCacheTierTests.cs` | `CreateAuthenticatedClient` |
| `tests/Shortboxerr.Tests/SystemEndpointsTests.cs` | factory + authenticated client |
| `tests/Shortboxerr.Tests/BaseEndpointTest.cs` | `Factory` |
| `tests/Shortboxerr.Tests/CustomWebApplicationFactory.cs` | `ResetApiKeyToTestDefault` |
| `tests/Shortboxerr.Tests/SettingsEndpointTests.cs` | collection, finally reset, assertions |
| `tests/Shortboxerr.Tests/SettingsEndpointTestsCollection.cs` | new |
| `src/Shortboxerr.Api/wwwroot/**` | UI build |

## Commits
1. `fix(tests): stabilize integration tests for API key middleware` — (this iteration)

## Summary
Integration tests now send the seeded test API key where required; settings tests no longer leave the DB key out of sync with the shared client, and no longer run in parallel against one mutable key.
