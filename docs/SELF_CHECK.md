# Self Check - Iteration 150

## Summary
**EPIC 11.14: Metron Settings UI + EPIC 11.15: Hide Internal Data Source Names** - Complete

Added Settings UI for Metron backup cover service and removed all customer-facing references to internal data source names (WalkSoftly, Metron).

## Recent Iterations
- **150**: Metron Settings UI + Hide Internal Data Source Names (EPIC 11.14/11.15)
- **149**: Metron Integration Implementation (EPIC 11.14)
- **148**: Backup Cover Research - Metron Evaluation (EPIC 11.14)
- **147**: Ignored Publishers UI (EPIC 11.10)
- **146**: Cover Image Fallback System (EPIC 11.13)

## Implementation Summary

### Files Modified
| File | Change |
|------|--------|
| `ui/src/api/client.ts` | Added MetronSettings, MetronSettingsUpdate, MetronTestResult types and API functions |
| `ui/src/pages/SettingsPage.tsx` | Added "Cover Service" tab with MetronSettingsTab component |
| `ui/src/pages/SeriesDetailPage.tsx` | Changed "from WalkSoftly" → "Upcoming" badge |
| `src/Shortboxerr.Api/Endpoints/SeriesEndpoints.cs` | Updated API description to generic language |
| `src/Shortboxerr.Api/Endpoints/PullListEndpoints.cs` | Updated diagnostic notes to generic language |

## Implementation Checklist

### Metron Settings UI
- [x] Add Settings tab for Cover Service ✅
- [x] Enable/disable toggle ✅
- [x] Username input field ✅
- [x] Password input field (masked, shows "configured" status) ✅
- [x] Save Credentials button ✅
- [x] Test Connection button ✅
- [x] Max Requests Per Minute config ✅
- [x] Cache TTL Hours config ✅
- [x] Request Timeout Seconds config ✅
- [x] Link to metron.cloud for registration ✅

### Hide Internal Data Source Names
- [x] Audit all .tsx files for WalkSoftly/Metron/LOCG references ✅
- [x] Audit API response DTOs ✅
- [x] Replace "from WalkSoftly" in SeriesDetailPage → "Upcoming" ✅
- [x] Replace "from WalkSoftly" in Settings → "from the release schedule" ✅
- [x] Update API descriptions to use generic language ✅
- [x] Update diagnostic notes in PullListEndpoints ✅
- [x] Keep internal field names (walkSoftlyVolumeId) for API compatibility ✅
- [x] Keep specific names in logs for debugging ✅

## Build Health
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Test Results
```
Passed: 34 related tests (Metron + CoverFallback)
Total project tests: 2387+ passing
```

## Customer-Facing Changes

### Settings UI
- New "Cover Service" tab in Settings (between ComicVine and Pull List)
- Configuration for backup cover image lookups
- Test connection functionality

### UI Text Changes
| Before | After |
|--------|-------|
| "from WalkSoftly" (series detail) | "Upcoming" |
| "from WalkSoftly" (settings) | "from the release schedule" |

## Remaining Tasks
- [ ] **Marvel API** (optional) - Marvel-only backup cover source (Priority 3)

## Commits
1. `feat: add Metron settings UI and hide internal data source names`
