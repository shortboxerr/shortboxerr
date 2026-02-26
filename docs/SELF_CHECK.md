# Self-Check: Iteration 163

## Summary
Fixed Manual Import UI issues, improved filename parser to handle DC's Absolute series line and "Issue #X" patterns, and added publisher folder support. All three Manual Import actions (reject, import, update match) now work correctly with proper folder organization.

## Checklist

### 15.19 Manual Import & Parser Improvements

| Item | Status | Notes |
|------|--------|-------|
| Add SuggestedSeriesTitle to model/DTO | ✅ | StagedItem and StagedItemDto updated |
| StagingService populates series title | ✅ | TryMatchSeriesAsync sets title |
| UpdateMatchAsync fetches series title | ✅ | Queries database for title |
| Frontend uses correct fields | ✅ | Uses suggestedSeriesId/Title |
| Recognize DC Absolute series line | ✅ | Regex detects 9 character names |
| Skip "absolute" in CollectionIndicators | ✅ | When part of series name |
| Parse "Issue #X" pattern | ✅ | IssueWordPattern regex added |
| Reject action works | ✅ | Moves to failed folder |
| Import action works | ✅ | Moves to library with publisher folder |
| Update match action works | ✅ | Updates and displays title |
| Publisher folder in path | ✅ | Default format: `{Publisher}/{Series Title} ({Year})` |
| Bulk import uses matched series | ✅ | Looks up series from staging scan |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Frontend Build: SUCCESS (0 warnings)
Parser Tests: 
  - "Absolute Wonder Woman #17 (2026).cbz" → series: "Absolute Wonder Woman", issue: 17 ✅
  - "Absolute Martian Manhunter Issue #9.cbz" → series: "Absolute Martian Manhunter", issue: 9 ✅
Import Test:
  - Destination: /library/DC Comics/Absolute Wonder Woman (2024)/Absolute Wonder Woman #19 (2026).cbz ✅
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Core/Models/StagedItem.cs` | Added SuggestedSeriesTitle property |
| `src/Shortboxerr.Api/Dtos/ManualImportDto.cs` | Added SuggestedSeriesTitle to DTO |
| `src/Shortboxerr.Infrastructure/Services/StagingService.cs` | Populate series title, folder format expansion, ISettingsService injection |
| `src/Shortboxerr.Core/Services/FilenameParser.cs` | Absolute line detection, Issue #X pattern |
| `src/Shortboxerr.Core/Services/ISettingsService.cs` | Updated default SeriesFolderFormat to include publisher |
| `src/Shortboxerr.Api/Endpoints/ManualImportEndpoints.cs` | Bulk import uses matched series from scan |
| `ui/src/api/client.ts` | Use suggestedSeriesId/Title fields |
| `docs/BACKLOG.md` | Added 15.19 as complete |
| `docs/WORKLOG.md` | Added Iteration 163 details |

## Commits

1. `fix(manualimport): fix matching display and parser improvements`
2. `feat(import): add publisher folder support in series folder format`

## Implementation Details

### Parser Changes
- Added regex to detect DC Absolute series line: `^absolute\s+(batman|wonder\s*woman|superman|flash|green\s*lantern|martian\s*manhunter|aquaman|cyborg|power\s*girl)`
- Added `IssueWordPattern()` regex: `\bIssue\s*#?\s*(\d+(?:\.\d+)?)`
- Skip "absolute" in CollectionIndicators when it's part of series name
- Parse "Issue #X" before standard hash pattern

### Backend Changes
- Extended `MatchOverride` record with `SeriesTitle` parameter
- `UpdateMatchAsync` queries database to fetch series title
- `TryMatchSeriesAsync` sets both SuggestedSeriesId and SuggestedSeriesTitle
- `ApplyMatchOverrides` applies both ID and title from override

### Folder Format Changes
- `StagingService` now uses `SeriesFolderFormat` setting via `ISettingsService`
- Added `ExpandSeriesFolderFormat()` method with token support
- Tokens: `{Publisher}`, `{Series Title}`, `{Year}`, `{Status}`
- "/" in format creates subdirectories
- Default changed from `{Series Title} ({Year})` to `{Publisher}/{Series Title} ({Year})`
- Bulk import endpoint now looks up matched series from staging scan

### Frontend Changes
- Client now uses `suggestedSeriesId` (number) instead of `suggestedSeries.id`
- Client uses `suggestedSeriesTitle` for display
- Falls back to `Series #${id}` if title unavailable

## Notes
- Environment variables required for proper path configuration:
  - `SHORTBOXERR_STAGING` - staging folder path
  - `SHORTBOXERR_FAILED` - failed folder path
  - `SHORTBOXERR_LIBRARY_ROOT` - library root path
- Default paths (`/data/...`) may require Docker volume configuration
- Series folder format configurable via Settings > General > Series Folder Format

---

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
