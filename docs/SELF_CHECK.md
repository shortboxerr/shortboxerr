# Self Check - Iteration 105

## EPIC 9: Cover Cache Size Limits & LRU Eviction

### Checklist

| Item | Status | Notes |
|------|--------|-------|
| Frontend compiles | ✅ | No frontend changes |
| Backend compiles | ✅ | `dotnet build` |
| Tests pass | ✅ | All tests pass (21 new tests) |
| Git commits | ✅ | 1 commit |

### Acceptance Criteria Status

| AC | Status |
|----|--------|
| Configurable maximum cache size (default: 500MB) | ✅ |
| LRU eviction when limit exceeded | ✅ |
| Enforce RetentionDays via background cleanup | ✅ |
| Background service for periodic cleanup | ✅ |
| API endpoint for manual cleanup | ✅ |
| Breakdown by size in stats | ✅ |
| Settings UI for configuration | Deferred (frontend) |

### New CoverSettings Properties

| Setting | Default | Description |
|---------|---------|-------------|
| MaxCacheSizeBytes | 500MB | Maximum cache size (0 = unlimited) |
| CleanupTargetPercent | 80 | Target size after cleanup |
| CleanupIntervalHours | 24 | Background cleanup interval |
| AutoCleanupEnabled | true | Auto-cleanup after downloads |

### API Endpoints (2 new)

| Endpoint | Status |
|----------|--------|
| GET /api/v1/covers/cache/stats/detailed | ✅ |
| POST /api/v1/covers/cleanup | ✅ |

### Unit Tests (21 tests)

| Test Category | Count | Status |
|---------------|-------|--------|
| Settings Tests | 4 | ✅ |
| Detailed Stats Tests | 4 | ✅ |
| LRU Eviction Tests | 3 | ✅ |
| Retention Policy Tests | 2 | ✅ |
| Combined Cleanup Tests | 4 | ✅ |
| CleanupResult Tests | 4 | ✅ |

### Files Changed

| File | Type |
|------|------|
| src/Shortboxerr.Core/Services/ICoverService.cs | Modified |
| src/Shortboxerr.Infrastructure/Services/CoverService.cs | Modified |
| src/Shortboxerr.Infrastructure/BackgroundServices/CoverCacheCleanupBackgroundService.cs | New |
| src/Shortboxerr.Infrastructure/DependencyInjection.cs | Modified |
| src/Shortboxerr.Api/Endpoints/CoverEndpoints.cs | Modified |
| tests/Shortboxerr.Tests/CoverCacheCleanupTests.cs | New |
