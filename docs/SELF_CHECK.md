# Self-Check: Iteration 162

## Summary
Implemented EPIC 14.10 (DDL Auto-Import Background Service). Created a background service that monitors completed DDL downloads and automatically triggers the import pipeline, closing the workflow gap identified in iteration 161.

## Checklist

### 14.10 DDL Auto-Import Background Service

| Item | Status | Notes |
|------|--------|-------|
| Create DdlImportBackgroundService | ✅ | Polls for pending imports every 30s |
| Integrate with DdlImportService | ✅ | Calls ProcessDownloadAsync |
| Configurable check interval | ✅ | `ddl_auto_import_interval_seconds` setting |
| Respect auto-import settings | ✅ | `ddl_auto_import_enabled` setting |
| Track pending downloads | ✅ | GetPendingImportDownloads() method |
| Mark downloads as imported | ✅ | MarkAsImported() method |
| Store candidate for matching | ✅ | Candidate stored in DdlDownloadHistoryEntry |
| Enable/disable setting | ✅ | `ddl_auto_import_enabled` |
| Confidence threshold | ✅ | `ddl_auto_import_min_confidence` |
| Manual review mode | ✅ | PendingManualReview flow supported |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Tests: 6 new tests added (DdlImportBackgroundServiceTests)
Note: Pre-existing test failures in GetComicsAdapterTests.cs (not related to this change)
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Infrastructure/BackgroundServices/DdlImportBackgroundService.cs` | New - Background service implementation |
| `src/Shortboxerr.Core/Ddl/IDdlDownloadService.cs` | Added GetPendingImportDownloads, MarkAsImported, import tracking fields |
| `src/Shortboxerr.Infrastructure/Ddl/DdlDownloadService.cs` | Implemented tracking methods |
| `src/Shortboxerr.Infrastructure/DependencyInjection.cs` | Registered background service |
| `tests/Shortboxerr.Tests/DdlImportBackgroundServiceTests.cs` | New - Unit tests |
| `docs/BACKLOG.md` | Marked 14.10 as complete |
| `docs/WORKLOG.md` | Added Iteration 162 details |

## Commits

1. `feat(ddl): add DdlImportBackgroundService for auto-import`
2. `test(ddl): add DdlImportBackgroundService tests`

## Implementation Details

### Background Service
- Starts 15 seconds after app launch (initial delay)
- Polls every 30 seconds (configurable via settings)
- Uses scoped services for proper DI
- Handles consecutive errors with backoff

### Download Tracking
- DdlDownloadHistoryEntry extended with:
  - `ImportProcessed` (bool)
  - `ImportProcessedAt` (DateTime?)
  - `Candidate` (DdlCandidate?) for import matching
- `GetPendingImportDownloads()` filters: Success=true, ImportProcessed=false, DestinationPath not empty

### Settings (generic API at /api/v1/settings/{key})
- `ddl_auto_import_enabled`: Enable/disable feature (default: true)
- `ddl_auto_import_interval_seconds`: Poll interval (default: 30)
- `ddl_auto_import`: Auto-import on match (default: true)
- `ddl_auto_import_min_confidence`: Threshold for auto-approve (default: 80)

---

# Self-Check: Iteration 159

## Summary
Implemented EPIC 11.21 (Upcoming Issues - Display Parity with Regular Issues). Enhanced the series detail view to display upcoming issues with the same metadata as regular issues, including proper list view integration.

## Checklist

### 11.21 Upcoming Issues Display Parity

| Item | Status | Notes |
|------|--------|-------|
| Issue number display | ✅ | Shows issueNumberText or issueNumber |
| Issue title | ✅ | Shows title or "TBA" if not available |
| Release timing indicator | ✅ | Uses backend releaseTiming ("In 3 days", "Tomorrow", etc.) |
| formatDaysUntilRelease helper | ✅ | Fallback frontend calculation |
| List view: same columns | ✅ | #, Title, Release Date, Status, Tags, Actions |
| List view: issue number | ✅ | Populated with styled number |
| List view: Upcoming badge | ✅ | Blue badge with clock icon |
| List view: Annual/Special tags | ✅ | Shown when applicable |
| Visual differentiation | ✅ | Subtle background on upcoming rows |

## Build & Test Results

```
Frontend Build: SUCCESS (0 warnings, 0 errors)
TypeScript: No errors
Bundle size: 602.79 kB (gzip: 153.03 kB)
```

## Files Changed

| File | Change |
|------|--------|
| `ui/src/pages/SeriesDetailPage.tsx` | Added formatDaysUntilRelease(), updated cover view release timing, implemented list view with mixed regular/upcoming issues |
| `docs/WORKLOG.md` | Added Iteration 159 details |
| `docs/BACKLOG.md` | Marked 11.21 as complete |

## Commits

1. `feat(ui): add upcoming issues display parity in series view (EPIC 11.21)`

## Implementation Details

### Cover View Changes
- Release timing now uses backend-provided `releaseTiming` field
- Falls back to `formatDaysUntilRelease()` if releaseTiming unavailable
- Styled in accent-info color for visual distinction

### List View Changes
- Replaced filtered IssueListView with inline table rendering
- Supports mixed DisplayIssue array (regular + upcoming)
- Upcoming rows:
  - No selection checkbox (can't mark as wanted)
  - Issue number displayed consistently
  - Title shows "TBA" for unknown titles
  - Status shows "Upcoming" badge with Clock icon
  - Tags show Annual/Special when applicable
  - No actions (can't search/mark)
- Regular rows: Use existing IssueListRow component

### Helper Function
```typescript
function formatDaysUntilRelease(releaseDate: string): string {
  // Returns: "Today", "Tomorrow", "In X days", "Next week", or formatted date
}
```

## Next Steps

- [ ] Consider adding publisher info to upcoming issue display
- [ ] Evaluate EPIC 14.8 (Series Deletion UX) priority
- [ ] Consider other P1 backlog items

## Notes
- UI-only changes, no backend modifications needed
- Backend already provides releaseTiming in UpcomingRelease response
- List view now properly integrates upcoming issues instead of filtering them out
