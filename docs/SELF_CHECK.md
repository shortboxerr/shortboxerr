# Self Check - Iteration 139

## Summary
**EPIC 11.12: Show Upcoming Releases on Series View** - Completed

Implemented feature to display upcoming releases from WalkSoftly on the series detail page. Shows issues that WalkSoftly reports but ComicVine hasn't indexed yet.

## Recent Iterations
- **139**: Show Upcoming Releases on Series View (EPIC 11.12)
- **138**: WalkSoftly Pull List Integration (EPIC 11.10)
- **137**: Pull List Data Accuracy Investigation (EPIC 15.9)
- **136**: Telegram Notification Provider
- **135**: Compiler Warning Cleanup

## Implementation Checklist
- [x] SeriesUpcomingReleasesResult model
- [x] UpcomingRelease model
- [x] GetSeriesUpcomingReleasesAsync() in PullListService
- [x] Title normalization for matching
- [x] Publisher validation
- [x] Issue number filtering (> max local)
- [x] API endpoint: GET /api/v1/series/{id}/upcoming
- [x] Frontend API client function
- [x] TypeScript interfaces
- [x] "Upcoming" section in SeriesDetailPage
- [x] Cover and list view support
- [x] Unit tests (6 tests)
- [x] Documentation updates

## Bug Fixes (Same Session)
- [x] Fixed duplicate ComicVineId crash in `ToDictionaryAsync`
- [x] Fixed library matching when WalkSoftly has incorrect volume IDs
- [x] Fixed invisible button (missing `--accent` CSS variable)

## Test Results
```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 44ms
(GetSeriesUpcomingReleases tests)
```

## Build Health
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## New Files
- None (all changes to existing files)

## Modified Files
- `src/Shortboxerr.Core/PullList/IPullListService.cs` - New models
- `src/Shortboxerr.Infrastructure/PullList/PullListService.cs` - New method + bug fixes
- `src/Shortboxerr.Api/Endpoints/SeriesEndpoints.cs` - New endpoint
- `ui/src/api/client.ts` - API client + types
- `ui/src/pages/SeriesDetailPage.tsx` - Upcoming section UI
- `ui/src/App.css` - Added --accent CSS variable
- `tests/Shortboxerr.Tests/PullListServiceTests.cs` - 6 new tests
- `docs/BACKLOG.md` - Marked 11.12 as complete
- `docs/WORKLOG.md` - Added iteration 139

## New API Endpoint
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/v1/series/{id}/upcoming?weeksAhead=4 | Get upcoming releases from WalkSoftly cache |

## Example Response
```json
{
  "seriesId": 25,
  "seriesTitle": "Absolute Wonder Woman",
  "releases": [{
    "issueNumber": 17,
    "issueNumberText": "17",
    "releaseDate": "2026-02-25T00:00:00",
    "publisher": "DC Comics",
    "daysUntilRelease": 1,
    "releaseTiming": "Tomorrow"
  }],
  "maxLocalIssueNumber": 16,
  "weeksSearched": 4
}
```
