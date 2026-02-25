# Self-Check: Iteration 156

## Summary
Implemented EPIC 11.25 by adding confidence-scored ID-less Metron matching for upcoming issues that do not yet have ComicVine issue IDs.

## Checklist

### 11.25 ID-Less Upcoming Issue Matching

| Item | Status | Notes |
|------|--------|-------|
| ID-less candidate matching pipeline | ✅ | Uses Metron search by series + issue number |
| Confidence scoring and threshold | ✅ | Title similarity + publisher match + store-date proximity |
| Configurable threshold | ✅ | `MetronSettings.MinMatchConfidence` (50-100, default 85) |
| Persist match metadata | ✅ | `CoverMatchMethod`, `CoverMatchConfidence` on `ComicVineIssue` |
| Rejection safeguards | ✅ | Low-confidence results keep volume fallback |
| Observability counters | ✅ | `idless matched` / `idless rejected` in enrichment logs |
| Tests | ✅ | Added threshold rejection and positive ID-less match coverage |

## Build & Test Results

```
Build: SUCCESS (1 warning, 0 errors)

Targeted tests:
- CoverFallbackServiceTests + SettingsEndpointTests
- Passed: 44
- Failed: 0
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Core/Metron/IMetronClient.cs` | Added `MinMatchConfidence` setting |
| `src/Shortboxerr.Core/Services/ICoverFallbackService.cs` | Added expected store date input + match metadata on result |
| `src/Shortboxerr.Core/ComicVine/IComicVineClient.cs` | Added `CoverMatchMethod` and `CoverMatchConfidence` fields |
| `src/Shortboxerr.Infrastructure/Services/CoverFallbackService.cs` | Added confidence scoring, threshold rejection, and ID-less match metadata |
| `src/Shortboxerr.Infrastructure/BackgroundServices/DiscoveryCoverEnrichmentService.cs` | Wired ID-less lookup date input, metadata persistence, idless counters, and no-ID Metron cover application |
| `src/Shortboxerr.Api/Endpoints/SettingsEndpoints.cs` | Exposed `MinMatchConfidence` via Metron settings API |
| `tests/Shortboxerr.Tests/CoverFallbackServiceTests.cs` | Added ID-less threshold rejection and positive-match tests |
| `tests/Shortboxerr.Tests/SettingsEndpointTests.cs` | Added clamping test for `MinMatchConfidence` |
| `docs/BACKLOG.md` | Marked 11.25 completed |
| `docs/WORKLOG.md` | Updated Iteration 156 details |

## Notes
- One transient MSBuild file-lock warning occurred during build retry, but build and tests succeeded.
