# Self-Check: Iteration 173

## Summary
Completed EPIC 19.1 - Year-Aware Matching. Implemented robust auto-matching with year tolerance to prevent series mismatches (e.g., "Deadman (2017)" files going to "Deadman (2006)"). This is critical P1 functionality affecting data integrity.

## Checklist

### 19.1 Year-Aware Matching

| Item | Status | Notes |
|------|--------|-------|
| AutoMatchSettings class | ✅ | Consolidated settings with year tolerance, confidence, ambiguity detection |
| Year mismatch rejection | ✅ | Rejects when diff > YearMatchTolerance (default: 2) |
| Year mismatch penalty | ✅ | Reduces confidence when within tolerance but not exact |
| Ambiguous series detection | ✅ | Detects multiple series with same normalized name |
| RequireYearForAmbiguousSeries | ✅ | Flags low confidence when year missing for ambiguous series |
| ISettingsService methods | ✅ | GetAutoMatchSettingsAsync, SetAutoMatchSettingsAsync |
| SettingsService implementation | ✅ | 6 persisted keys for auto-match settings |
| API GET endpoint | ✅ | GET /api/v1/settings/automatch |
| API PUT endpoint | ✅ | PUT /api/v1/settings/automatch with validation |
| Frontend API client | ✅ | AutoMatchSettings interface and methods |
| UI in Import settings | ✅ | Full settings form with all options |
| Warning banner | ✅ | Explains critical nature of settings |
| Unit tests | ✅ | 6 tests for year-aware matching logic |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Frontend Build: SUCCESS
Tests: 6 new tests added (GetComicsAdapter tests have pre-existing issues)
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Core/Services/ISettingsService.cs` | Added AutoMatchSettings class and interface methods |
| `src/Shortboxerr.Core/ComicVine/IAutoMatchService.cs` | Removed duplicate class, use Services namespace |
| `src/Shortboxerr.Core/Ddl/IDdlImportService.cs` | Added RequiresManualReview, MinConfidenceThreshold to DdlMatchResult |
| `src/Shortboxerr.Infrastructure/Services/SettingsService.cs` | Auto-match settings persistence |
| `src/Shortboxerr.Infrastructure/Ddl/DdlImportService.cs` | Year-aware matching logic |
| `src/Shortboxerr.Api/Endpoints/SettingsEndpoints.cs` | Auto-match settings endpoints |
| `ui/src/api/client.ts` | AutoMatchSettings types and API methods |
| `ui/src/pages/SettingsPage.tsx` | Full auto-match settings UI |
| `tests/Shortboxerr.Tests/DdlImportServiceTests.cs` | 6 year-aware matching tests |

## Commits

1. `feat(automatch): add year-aware matching logic (EPIC 19.1)`
2. `feat(ui): add auto-match settings UI in Import settings tab`
3. `test(automatch): add year-aware matching tests (EPIC 19.1)`

## Settings Summary

| Setting | Default | Purpose |
|---------|---------|---------|
| YearMatchTolerance | 2 | Max year difference before rejection |
| RejectMismatchedYears | true | Hard reject vs. penalty only |
| YearMismatchPenalty | 25 | Confidence reduction for mismatches |
| ConfidenceThreshold | 85 | Min confidence for auto-import |
| RequireYearForAmbiguousSeries | true | Require year when series name ambiguous |
| EnableAmbiguousSeriesDetection | true | Detect multiple same-name series |

## Notes
- Pre-existing test failures in GetComicsAdapter tests (unrelated to this change)
- AutoMatchSettings consolidated from ComicVine namespace to Services namespace
- Year already extracted by DdlReleaseParser; this iteration uses it for matching decisions

## Next Steps

- [ ] EPIC 19.2: Series Name Disambiguation
- [ ] EPIC 19.3: Release Parser Improvements
- [ ] EPIC 19.4: Match Verification & Confirmation
- [ ] EPIC 19.5: Matching Audit & Logging

---

# Self-Check: Iteration 172

## Summary
Completed EPIC 18.4 - File Rename Within Series. Enhanced OrganizeModal UI with view filtering tabs and file type grouping to provide clearer visibility of issue vs collection file renames.

## Checklist

### 18.4 File Rename Within Series

| Item | Status | Notes |
|------|--------|-------|
| View filter tabs (All/Folder/Files) | ✅ | Allows focused preview of changes |
| Group issues separately | ✅ | Shows issue number badge |
| Group collections/TPBs separately | ✅ | Shows TPB badge |
| Size per group | ✅ | Each section shows total size |
| Improved summary | ✅ | Shows issue/collection counts separately |
| Issue file format tokens | ✅ | Already implemented in Iteration 164 |
| Collection file format tokens | ✅ | Already implemented in Iteration 164 |
| Conflict detection | ✅ | Already implemented in Iteration 164 |

## Build & Test Results

```
Frontend TypeScript: SUCCESS (no lint errors)
```

## Files Changed

| File | Change |
|------|--------|
| `ui/src/pages/SeriesDetailPage.tsx` | Enhanced OrganizeModal with filtering and type grouping |

## Commits

1. `feat(ui): enhance file rename preview with filtering and type grouping (EPIC 18.4)`

## Implementation Details

### View Filter Tabs
- "All Changes" - Shows both folder and file changes (default)
- "Folder" - Shows only folder rename preview
- "Files" - Shows only file renames (grouped by type)
- Tabs disabled when no changes of that type exist

### File Type Grouping
- **Issues Section**: Blue badge with issue number (#001, #002, etc.)
- **Collections Section**: Orange TPB badge
- Each section has own size display and scrollable list
- Current → New filename clearly shown

### Summary Enhancement
- Shows "X issues and Y collections will be renamed"
- More informative than previous generic "N files will be organized"

## Notes
- Core file rename preview functionality was already in Iteration 164
- This iteration focused on UI enhancements for clarity
- EPIC 18.4 is now complete

## Next Steps

- [ ] EPIC 18.5: Scheduled organization option
- [ ] EPIC 18.6: Safety & Rollback (Dry-run mode, Atomic operations)
- [ ] EPIC 11.27: Update local cover caching (integrates 11.26)

---

# Self-Check: Iteration 171

## Summary
Completed EPIC 18.7 - Settings format change warning. Added a warning banner that appears in General Settings when the Series Folder Format is changed, alerting users that existing series may need reorganization.

## Checklist

### 18.7 UI Indicators - Settings Format Change Warning

| Item | Status | Notes |
|------|--------|-------|
| Track original format on load | ✅ | useState with null initial |
| Detect format change after save | ✅ | Compare to original |
| Warning banner UI | ✅ | AlertTriangle icon, explanation text |
| "Go to System Tasks" button | ✅ | Uses setSearchParams to navigate |
| Dismiss button | ✅ | Hides warning |

## Build & Test Results

```
Frontend TypeScript: SUCCESS
```

## Files Changed

| File | Change |
|------|--------|
| `ui/src/pages/SettingsPage.tsx` | Added format change tracking and warning banner |

## Commits

1. `feat(ui): add settings format change warning (EPIC 18.7)`

## Implementation Details

### State Management
- `originalSeriesFormat`: Tracks the format loaded from API (set once on initial load)
- `showFormatChangedWarning`: Controls visibility of the warning banner
- Warning triggered when save succeeds AND new format differs from original

### Warning Banner Features
- Orange/warning-themed styling
- AlertTriangle icon
- Title: "Series Folder Format Changed"
- Explanation text about reorganization
- "Go to System Tasks" button (navigates via URL param)
- "Dismiss" button to hide

## Notes
- Warning only shows when format actually changes from what was loaded
- URL-based navigation ensures proper tab change
- EPIC 18.7 is now fully complete

## Next Steps

- [x] EPIC 18.4: File Rename Within Series (Iteration 172)
- [ ] EPIC 18.5: Scheduled organization option
- [ ] EPIC 11.27: Update local cover caching (integrates 11.26)

---

# Self-Check: Iteration 170

## Summary
Implemented EPIC 18.7 (partial) - Series list path mismatch indicator. Added a column to the series list showing whether each series' folder path matches the configured naming format.

## Checklist

### 18.7 UI Indicators - Series List Path Mismatch

| Item | Status | Notes |
|------|--------|-------|
| Backend: GetPathMismatchStatusAsync method | ✅ | Efficient bulk path checking |
| Backend: PathMismatchInfo model | ✅ | HasMismatch, CurrentPath, ExpectedPath |
| API: includePathMismatch query parameter | ✅ | Optional on GET /api/v1/series |
| DTO: PathMismatch property | ✅ | Nullable bool in SeriesDto |
| DTO: ExpectedPath property | ✅ | Nullable string in SeriesDto |
| Frontend: Series interface updates | ✅ | pathMismatch, currentPath, expectedPath |
| Frontend: API method update | ✅ | includePathMismatch parameter |
| Frontend: Path column header | ✅ | Added to series table |
| Frontend: FolderX mismatch icon | ✅ | Warning color when mismatch |
| Frontend: Check match icon | ✅ | Success color when organized |
| Frontend: Tooltip with paths | ✅ | Shows current vs expected |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Frontend TypeScript: SUCCESS
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Core/Services/ILibraryOrganizationService.cs` | Added GetPathMismatchStatusAsync, PathMismatchInfo |
| `src/Shortboxerr.Infrastructure/Services/LibraryOrganizationService.cs` | Implemented path mismatch checking |
| `src/Shortboxerr.Api/Endpoints/SeriesEndpoints.cs` | Added includePathMismatch parameter |
| `src/Shortboxerr.Api/Dtos/SeriesDto.cs` | Added PathMismatch, ExpectedPath properties |
| `ui/src/api/client.ts` | Added path mismatch types and param |
| `ui/src/pages/SeriesPage.tsx` | Added Path column with indicator |

## Commits

1. `feat(ui): add path mismatch indicator to series list (EPIC 18.7)`

## Implementation Details

### Backend Service
- `GetPathMismatchStatusAsync` method efficiently queries series without loading file details
- Creates mock Series objects for format expansion (only needs metadata fields)
- Returns dictionary mapping series ID to PathMismatchInfo
- Compares current path to expected path (case-insensitive)

### API Changes
- Series list endpoint accepts `includePathMismatch=true` query param
- Path mismatch info fetched separately (not cached) since depends on settings
- Only included in response when requested (performance optimization)

### Frontend UI
- New "Path" column in series table
- FolderX icon in warning color for mismatched paths
- Check icon in success color for organized paths
- Tooltip shows "Current: X, Expected: Y" on hover
- Dash displayed when mismatch status unknown

## Notes
- Path mismatch is computed fresh (not cached) because it depends on SeriesFolderFormat setting
- Only series with non-null paths are checked (new series without paths show as match)
- Lightweight compared to full preview - doesn't scan files

## Next Steps

- [x] EPIC 18.7: Settings format change warning (Iteration 171)
- [ ] EPIC 18.5: Scheduled organization option
- [ ] EPIC 11.27: Update local cover caching (integrates 11.26)

---

# Self-Check: Iteration 169

## Summary
Completed EPIC 18.5 (partial) - "Organize All" System Task. Added ability to preview and execute organization for all series in the library via System > Settings > System Tasks.

## Checklist

### 18.5 Bulk Organization Tools

| Item | Status | Notes |
|------|--------|-------|
| "Organize All" preview endpoint | ✅ | GET /api/v1/system/tasks/organize-all/preview |
| "Organize All" execute endpoint | ✅ | POST /api/v1/system/tasks/organize-all |
| Summary response DTOs | ✅ | OrganizeAllPreviewResponse, OrganizeAllResultResponse |
| System Tasks tab in Settings | ✅ | New tab with Wrench icon |
| Preview modal with stats | ✅ | Total series, changes needed, files, size |
| Series changes list | ✅ | Scrollable list with path transitions |
| "All organized" state | ✅ | Message when no changes needed |
| Error handling | ✅ | Shows series with errors |
| Execution with progress | ✅ | Loading spinner during execution |
| Success/failure summary | ✅ | Shows counts after completion |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Frontend TypeScript: SUCCESS
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Api/Endpoints/SystemEndpoints.cs` | Added organize-all endpoints and DTOs |
| `ui/src/api/client.ts` | Added types and API methods |
| `ui/src/pages/SettingsPage.tsx` | Added System Tasks tab and component |

## Commits

1. `feat(tasks): add 'Organize All' system task (EPIC 18.5)`

## Implementation Details

### Backend Endpoints
- Preview analyzes all series, returns summary + list of series with changes
- Execute runs organization for all series, invalidates caches
- Uses ILoggerFactory for logging (static class workaround)

### Frontend UI
- "System Tasks" tab added to Settings navigation
- Task card shows "Organize All Series" with description
- Preview modal shows:
  - 4-stat grid: Total, Need Changes, Files to Rename, Total Size
  - List of series with current→new path transitions
  - Error indicators for problematic series
- Execute button disabled when no changes needed

## Notes
- Excludes linked annual series (organized with parent)
- Preview only shows series with actual changes
- Result summary persists after modal closes

## Next Steps

- [ ] EPIC 18.5: Scheduled organization option (auto-organize on format change)
- [ ] EPIC 11.27: Update local cover caching (integrates 11.26)
- [ ] EPIC 18.4: File Rename Within Series

---

# Self-Check: Iteration 168

## Summary
Completed EPIC 14.8 - Series Deletion UX Improvements. Added a confirmation modal for series deletion that shows what will be deleted, including linked annual series that cascade delete.

## Checklist

### 14.8 Series Deletion UX Improvements

| Item | Status | Notes |
|------|--------|-------|
| Confirmation modal for series deletion | ✅ | DeleteSeriesModal component |
| Deletion progress indicator | ✅ | Spinner during deletion |
| List refresh after deletion | ✅ | Navigate to /series list |
| Backend: Cascade delete linked annual series | ✅ | DELETE endpoint updated |
| Delete preview endpoint | ✅ | GET /api/v1/series/{id}/delete/preview |
| Show linked annuals in preview | ✅ | Warning with count |
| Issue/edition counts in preview | ✅ | Detailed breakdown |
| Danger alert about irreversibility | ✅ | Warning message |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Frontend TypeScript: SUCCESS
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Api/Endpoints/SeriesEndpoints.cs` | Added delete preview endpoint, cascade delete |
| `src/Shortboxerr.Api/Dtos/SeriesDto.cs` | Added SeriesDeletePreviewDto, LinkedSeriesDto, SeriesDeleteResultDto |
| `ui/src/api/client.ts` | Added types and getSeriesDeletePreview method |
| `ui/src/pages/SeriesDetailPage.tsx` | Added DeleteSeriesModal component |

## Commits

1. `feat(series): add deletion confirmation modal with cascade delete`

## Implementation Details

### DeleteSeriesModal Features
- Fetches deletion preview via `getSeriesDeletePreview(seriesId)`
- Displays main series with issue/edition counts
- Lists linked annual series that will also be deleted
- Warning alert when linked annuals exist
- Danger alert about action being irreversible
- Delete button shows count of total series to delete

### Backend Changes
- `GET /api/v1/series/{id}/delete/preview` returns `SeriesDeletePreviewDto`
- `DELETE /api/v1/series/{id}` now returns `SeriesDeleteResultDto` instead of 204 No Content
- Cascade deletes all linked annual series
- Records history events for each deleted series
- Invalidates caches for all deleted series

## Notes
- Files on disk are NOT deleted (data loss prevention)
- List refresh happens via navigation to /series after deletion
- Success toast shows summary of what was deleted

## Next Steps

- [x] EPIC 18.5: Bulk Organization Tools ("Organize All" - Iteration 169)
- [ ] EPIC 11.27: Update local cover caching (integrates 11.26)
- [ ] EPIC 18.4: File Rename Within Series

---

# Self-Check: Iteration 167

## Summary
Fixed EPIC 11.27 - Discovery cover endpoint parameter naming. Renamed the misleading `comicVineIssueId` parameter to generic `coverId` since the endpoint accepts various ID types (Metron ID, DB issue ID, etc.).

## Checklist

### 11.27 Discovery Cover Endpoint Fix

| Item | Status | Notes |
|------|--------|-------|
| Renamed endpoint parameter | ✅ | `comicVineIssueId` → `coverId` |
| Updated ICoverService interface | ✅ | Added documentation |
| Updated CoverService implementation | ✅ | Generic parameter name |
| Improved OpenAPI descriptions | ✅ | Clarifies ID is cache key |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Api/Endpoints/CoverEndpoints.cs` | Renamed parameter, updated descriptions |
| `src/Shortboxerr.Core/Services/ICoverService.cs` | Updated signature with docs |
| `src/Shortboxerr.Infrastructure/Services/CoverService.cs` | Updated implementation |

## Commits

1. `fix(covers): clarify discovery cover endpoint parameter naming (EPIC 11.27)`

## Notes
- The endpoint `/api/v1/covers/discovery/{coverId}` now correctly documents that:
  - `coverId` is a cache key, not necessarily a ComicVine ID
  - May be Metron ID (for external enrichment) or DB issue ID (for known issues)
- No functional changes - only naming and documentation improvements

## Next Steps

- [ ] EPIC 11.27: Update local cover caching (integrates 11.26)
- [ ] EPIC 18.5: Bulk Organization Tools
- [x] EPIC 14.8: Series Deletion UX Improvements (Iteration 168)

---

# Self-Check: Iteration 166

## Summary
Completed EPIC 18.3 (Library Organization - Mass Editor Integration). Added "Organize" bulk action to Series page for organizing files across multiple selected series.

## Checklist

### 18.3 Mass Editor Integration

| Item | Status | Notes |
|------|--------|-------|
| Series page bulk "Organize" button | ✅ | FolderSync icon in toolbar |
| BulkOrganizeModal component | ✅ | Preview and execute bulk organization |
| Preview summary stats | ✅ | Series count, file count, total size |
| Per-series change list | ✅ | Shows folder path changes |
| Error handling | ✅ | Per-series errors displayed |
| Execution results | ✅ | Success/failure counts shown |
| "No changes needed" state | ✅ | Success checkmark when organized |

## Build & Test Results

```
Frontend Build: SUCCESS
Backend Build: SUCCESS (unchanged)
```

## Files Changed

| File | Change |
|------|--------|
| `ui/src/pages/SeriesPage.tsx` | Added BulkOrganizeModal, FolderSync button |
| `src/Shortboxerr.Api/wwwroot/` | Rebuilt frontend assets |

## Commits

1. `feat(ui): add bulk Organize action to Series page (EPIC 18.3)`

## Implementation Details

### BulkOrganizeModal Flow
1. Load preview via `api.getBulkOrganizePreview(seriesIds)`
2. Display summary stats (series/files/size)
3. Show per-series changes in scrollable list
4. Execute via `api.executeBulkOrganize(seriesIds)`
5. Display execution results
6. Invalidate cache on success

### UI States
- Loading: Shows spinner with series count
- Preview: Summary cards + series list
- No changes: Success checkmark
- Executing: Spinning loader
- Results: Success/failure counts with errors

## Notes
- Button only appears when series are selected
- Uses existing bulk API endpoints from iteration 164
- Completes EPIC 18.3 (both single and bulk)

## Next Steps

- [ ] 18.5 Bulk Organization Tools ("Organize All" system task)
- [ ] 18.4 File Rename Within Series (individual file rename preview)
- [ ] Consider 11.27 completion (Pull List endpoint fix)

---

# Self-Check: Iteration 165

## Summary
Implemented EPIC 18.3 (Library Organization - Series Detail Page UI). Added "Organize Files" button to Series Detail header with OrganizeModal for preview and execution.

## Checklist

### 18.3 Series Detail Page "Organize" Button

| Item | Status | Notes |
|------|--------|-------|
| Add FolderSync icon button to toolbar | ✅ | Next to delete button |
| OrganizeModal component | ✅ | Shows preview before execution |
| API client organize methods | ✅ | getSeriesOrganizePreview, executeSeriesOrganize |
| Preview loading state | ✅ | Spinner while analyzing files |
| Show folder rename preview | ✅ | Current → New path display |
| Show file rename preview | ✅ | Scrollable list of changes |
| Error display | ✅ | Alerts for errors/warnings |
| "No changes needed" state | ✅ | Success checkmark when organized |
| Execute button disabled states | ✅ | When errors, pending, or no changes |
| Cache invalidation on success | ✅ | Refetches series data |

## Build & Test Results

```
Frontend Build: SUCCESS
Backend Build: SUCCESS (unchanged)
Unit Tests: 13 LibraryOrganizationService tests passing
```

## Files Changed

| File | Change |
|------|--------|
| `ui/src/api/client.ts` | Added organize types and API methods |
| `ui/src/pages/SeriesDetailPage.tsx` | Added OrganizeModal component and button |
| `src/Shortboxerr.Api/wwwroot/` | Rebuilt frontend assets |

## Commits

1. `feat(ui): add Organize button to Series Detail Page (EPIC 18.3)`

## Implementation Details

### OrganizeModal Flow
1. Load preview via `api.getSeriesOrganizePreview(seriesId)`
2. Display folder change (if any)
3. Display file changes list (scrollable)
4. Show errors/warnings from preview
5. Execute via `api.executeSeriesOrganize(seriesId)`
6. Invalidate cache and close modal on success

### API Types Added
- `SeriesRenamePreview` - Full preview with files array
- `FileRenamePreview` - Individual file rename info
- `SeriesRenameResult` - Execution result
- `OrganizePreviewResponse` - Bulk preview response
- `OrganizeExecuteResponse` - Bulk execute response

## Notes
- Button uses FolderSync icon from lucide-react
- Modal shows "Files are already organized" when no changes needed
- Total file size displayed in file changes header
- Errors block execution (button disabled)

## Next Steps

- [ ] 18.3 Mass Editor "Organize" action
- [ ] Consider 18.5 bulk organization tools
- [ ] Evaluate 11.26/11.27 completion

---

# Self-Check: Iteration 164

## Summary
Implemented EPIC 18.1 and 18.2 (Library Organization & Rename - Core Service and API Endpoints). Created `ILibraryOrganizationService` with preview/execute capabilities and API endpoints for Sonarr/Radarr parity file organization.

## Checklist

### 18.1 Series Folder Rename Service

| Item | Status | Notes |
|------|--------|-------|
| ILibraryOrganizationService interface | ✅ | Preview and execute methods defined |
| SeriesRenamePreview model | ✅ | Includes file previews, errors, warnings |
| FileRenamePreview model | ✅ | Tracks current/new paths, rename flags |
| SeriesRenameResult model | ✅ | Execution results with file counts |
| LibraryOrganizationService implementation | ✅ | Full preview and execute logic |
| Folder format token expansion | ✅ | {Publisher}, {Series Title}, {Year}, {Status} |
| Issue file format tokens | ✅ | {Series Title}, {Issue}, {Year}, {Publisher}, {Issue Title} |
| Collection file format tokens | ✅ | {Series Title}, {Edition Type}, {Volume}, {Year} |
| Empty directory cleanup | ✅ | Removes empty dirs after move |
| Conflict detection | ✅ | Duplicate destinations flagged as errors |
| DI registration | ✅ | Scoped service in DependencyInjection.cs |

### 18.2 Series Rename API Endpoints

| Item | Status | Notes |
|------|--------|-------|
| POST /api/v1/series/organize/preview | ✅ | Batch preview with summary stats |
| POST /api/v1/series/organize/execute | ✅ | Batch execute with cache invalidation |
| GET /api/v1/series/{id}/organize/preview | ✅ | Single series preview |
| POST /api/v1/series/{id}/organize | ✅ | Single series execute |

## Build & Test Results

```
Backend Build: SUCCESS (0 warnings, 0 errors)
Note: Pre-existing test failures in GetComicsAdapterTests.cs (unrelated to this change)
Unit Tests: 12 tests added for LibraryOrganizationService
```

## Files Changed

| File | Change |
|------|--------|
| `src/Shortboxerr.Core/Services/ILibraryOrganizationService.cs` | New - Interface and models |
| `src/Shortboxerr.Infrastructure/Services/LibraryOrganizationService.cs` | New - Implementation |
| `src/Shortboxerr.Infrastructure/DependencyInjection.cs` | Registered ILibraryOrganizationService |
| `src/Shortboxerr.Api/Endpoints/SeriesEndpoints.cs` | Added organization endpoints and DTOs |
| `tests/Shortboxerr.Tests/LibraryOrganizationServiceTests.cs` | New - Unit tests |
| `docs/BACKLOG.md` | Marked 18.1, 18.2 as complete |
| `docs/WORKLOG.md` | Added Iteration 164 details |

## Commits

1. `feat(organize): add library organization service for Sonarr/Radarr parity (EPIC 18.1-18.2)`

## Implementation Details

### Preview Flow
1. Load series with issues and editions
2. Query FileAssets for all related files
3. Calculate new path from folder format + series metadata
4. Build file previews with new filenames
5. Detect conflicts (duplicate destinations)
6. Return preview with CanRename flag

### Execute Flow
1. Generate preview
2. Create destination directory if needed
3. Move each file (update FileAsset.Path in DB)
4. Update series.Path
5. Remove old empty directories
6. Invalidate relevant caches

### Format Token Support
- Series folder: `{Publisher}/{Series Title} ({Year})` → `DC Comics/Batman (2016)`
- Issue file: `{Series Title} #{Issue} ({Year})` → `Batman #001 (2016).cbz`
- Collection file: `{Series Title} - {Edition Type} Vol. {Volume} ({Year})` → `Batman - TPB Vol. 1 (2016).cbz`

### API Response Models
```csharp
OrganizePreviewResponse {
  Previews, TotalSeries, SeriesWithChanges, TotalFiles, FilesWithChanges, HasErrors
}

OrganizeExecuteResponse {
  Results, TotalSeries, Successful, Failed, TotalFilesRenamed, TotalFilesFailed
}
```

## Notes
- Excludes linked annual series (they're organized with parent)
- Library roots configurable via MediaManagement:RootFolders
- Sanitizes invalid characters from paths
- Handles decimal issue numbers (54.1 → 054.1)

## Next Steps

- [ ] 18.3 Mass Editor / Series Detail Page UI integration
- [ ] Consider 11.26/11.27 completion (Pull List cover caching)
- [ ] Evaluate remaining EPIC 14 items

---

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
