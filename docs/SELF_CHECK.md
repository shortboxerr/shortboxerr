# Self-Check: Iteration 158

## Summary
Implemented Phase 2 of EPIC 11.27 (Pull List Data Flow Refactoring) by creating the background upgrade service that periodically upgrades interim Metron data to authoritative ComicVine data.

## Checklist

### 11.27 Pull List Data Flow Refactoring - Phase 2

| Item | Status | Notes |
|------|--------|-------|
| DiscoveryUpgradeBackgroundService | ✅ | Periodically checks cached weeks for non-finalized issues |
| Re-query WalkSoftly | ✅ | Detects newly available CV issue IDs |
| Batch CV fetch | ✅ | Fetches full data for issues with new CV IDs |
| Update cached issues | ✅ | Marks upgraded issues as `ComicVineFinalized` |
| Settings: DiscoveryUpgradeEnabled | ✅ | Default: true |
| Settings: DiscoveryUpgradeIntervalHours | ✅ | Default: 4 (Mylar3 parity) |
| Settings: DiscoveryUpgradeWeeksAhead | ✅ | Default: 4 |
| DI registration | ✅ | Singleton hosted service |
| Unit tests | ✅ | 11 tests for settings and state transitions |

## Build & Test Results

```
Build: SUCCESS (0 warnings, 0 errors)

Targeted tests:
- DiscoveryUpgradeBackgroundServiceTests: 11 passed

Pre-existing failures (not introduced by this iteration):
- PullListServiceTests.GetDiscoveryPublishersAsync_* (GroupBy not supported by InMemory provider)
- DownloadHostResolverTests.Factory_CanResolve_ReturnsFalseForUnsupportedUrl
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Core/PullList/IPullListService.cs` | Added `DiscoveryUpgradeEnabled`, `DiscoveryUpgradeIntervalHours`, `DiscoveryUpgradeWeeksAhead` settings |
| `src/Shortboxerr.Infrastructure/BackgroundServices/DiscoveryUpgradeBackgroundService.cs` | New background service for MetronInterim→ComicVineFinalized upgrades |
| `src/Shortboxerr.Infrastructure/DependencyInjection.cs` | Registered DiscoveryUpgradeBackgroundService |
| `tests/Shortboxerr.Tests/DiscoveryUpgradeBackgroundServiceTests.cs` | 11 unit tests |
| `docs/WORKLOG.md` | Added Iteration 158 details |
| `docs/BACKLOG.md` | Updated 11.27 background upgrade service as complete |

## Commits

1. `feat(pulllist): add background discovery upgrade service (EPIC 11.27 Phase 2)`

## Algorithm

```
Every 4 hours (configurable):
  For each cached week (current + N weeks ahead):
    1. Deserialize cached issues from JSON
    2. Filter to non-finalized issues (Id <= 0 OR status != HasComicVineCover)
    3. Re-query WalkSoftly for that week
    4. Build lookup: (series title, issue number) → WalkSoftly release
    5. For each non-finalized issue:
       - If WalkSoftly now has a CV issue ID → add to upgrade list
    6. Batch fetch CV data for upgrade list
    7. Apply CV data, mark as finalized
    8. Save updated cache to database
```

## Next Steps

- [ ] Evaluate 11.26 (local cover caching routing issue) relevance
- [ ] Consider 11.21 (Upcoming Issues Display Parity) as next priority
- [ ] Add integration tests for Metron→ComicVine upgrade flow

## Notes
- This completes the core implementation of EPIC 11.27
- Background service respects settings and can be disabled
- 4-hour interval matches Mylar3's refresh behavior
- Upgrade only triggers when WalkSoftly provides new CV issue IDs
