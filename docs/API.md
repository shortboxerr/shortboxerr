# API (Living Contract)

All endpoints are versioned under `/api/v1`.

## System Endpoints

### Health Check
```
GET /health
```
Returns detailed health status with database connectivity check.

**Response (200 OK)**
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": null,
      "duration": 1.234
    }
  ],
  "totalDuration": 2.345
}
```

### Ping (Liveness)
```
GET /ping
```
Simple liveness check returning "pong".

### System Status
```
GET /api/v1/system/status
```
Returns application info.

---

## Series Endpoints

### List Series
```
GET /api/v1/series?page=1&pageSize=20&sortKey=title&sortDir=asc
```
Returns paginated list of series.

**Query Parameters:**
- `page` (int, default: 1)
- `pageSize` (int, default: 20)
- `sortKey` (string: title|startyear|createdat)
- `sortDir` (string: asc|desc)

**Response (200 OK)**
```json
{
  "records": [
    {
      "id": 1,
      "title": "Amazing Spider-Man",
      "sortTitle": "Amazing Spider-Man",
      "publisher": "Marvel",
      "startYear": 1963,
      "status": 0,
      "monitored": true,
      "issueCount": 0,
      "issueFileCount": 0,
      "editionCount": 0
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalRecords": 1,
  "totalPages": 1
}
```

### Get Series
```
GET /api/v1/series/{id}
```
Returns a single series by ID.

### Create Series
```
POST /api/v1/series
Content-Type: application/json

{
  "title": "Amazing Spider-Man",
  "publisher": "Marvel",
  "startYear": 1963,
  "monitored": true
}
```

### Update Series
```
PUT /api/v1/series/{id}
Content-Type: application/json

{
  "monitored": false
}
```

### Delete Series
```
DELETE /api/v1/series/{id}
```

---

## Edition Endpoints

### List Editions
```
GET /api/v1/editions?page=1&pageSize=20&seriesId=1&sortKey=title&sortDir=asc
```
Returns paginated list of collected editions.

**Query Parameters:**
- `page` (int, default: 1)
- `pageSize` (int, default: 20)
- `seriesId` (int, optional): Filter by series
- `sortKey` (string: title|releasedate|createdat|volumenumber)
- `sortDir` (string: asc|desc)

**Response (200 OK)**
```json
{
  "records": [
    {
      "id": 1,
      "seriesId": 1,
      "seriesTitle": "Amazing Spider-Man",
      "title": "Amazing Spider-Man Vol. 1",
      "editionType": 0,
      "volumeNumber": 1,
      "isbn": "978-1234567890",
      "monitored": true,
      "hasFile": false,
      "contentCount": 0
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalRecords": 1,
  "totalPages": 1
}
```

### Get Edition
```
GET /api/v1/editions/{id}
```

### Create Edition
```
POST /api/v1/editions
Content-Type: application/json

{
  "seriesId": 1,
  "title": "Amazing Spider-Man Vol. 1",
  "editionType": 0,
  "volumeNumber": 1,
  "isbn": "978-1234567890",
  "monitored": true
}
```

### Update Edition
```
PUT /api/v1/editions/{id}
Content-Type: application/json

{
  "monitored": false
}
```

### Delete Edition
```
DELETE /api/v1/editions/{id}
```

---

## Manual Import Endpoints

### Scan Staging Folder
```
GET /api/v1/manualimport
```
Scans the staging folder and returns all importable files with parsed metadata.

**Response (200 OK)**
```json
[
  {
    "path": "/data/staging/Batman #001.cbz",
    "fileName": "Batman #001.cbz",
    "size": 52428800,
    "extension": "cbz",
    "lastModified": "2026-02-02T03:00:00Z",
    "parsedInfo": {
      "seriesTitle": "Batman",
      "issueNumber": 1,
      "volumeNumber": null,
      "year": null,
      "publisher": null,
      "editionIndicator": null,
      "issueRange": null,
      "tags": []
    },
    "parseConfidence": 45,
    "suggestedSeriesId": 1,
    "suggestedEditionId": null,
    "isCollection": false,
    "rejectionReason": null
  }
]
```

### Get Import Preview
```
POST /api/v1/manualimport/preview
Content-Type: application/json

{
  "sourcePath": "/data/staging/Batman #001.cbz",
  "seriesId": 1,
  "issueId": 1,
  "editionId": null
}
```
Returns a preview of the import operation.

**Response (200 OK)**
```json
{
  "sourcePath": "/data/staging/Batman #001.cbz",
  "destinationPath": "/data/library/Batman/Batman #001.cbz",
  "newFileName": "Batman #001.cbz",
  "willRename": false,
  "willMove": true,
  "seriesId": 1,
  "seriesTitle": "Batman",
  "issueId": 1,
  "issueNumber": 1,
  "editionId": null,
  "editionTitle": null,
  "isCollection": false,
  "warnings": [],
  "canImport": true,
  "blockReason": null
}
```

### Execute Import
```
POST /api/v1/manualimport
Content-Type: application/json

{
  "sourcePath": "/data/staging/Batman #001.cbz",
  "seriesId": 1,
  "issueId": 1,
  "editionId": null
}
```
Executes the import, moving file to library and creating database records.

**Response (200 OK)**
```json
{
  "success": true,
  "sourcePath": "/data/staging/Batman #001.cbz",
  "destinationPath": "/data/library/Batman/Batman #001.cbz",
  "errorMessage": null,
  "fileAssetId": 1,
  "historyEventId": 1
}
```

### Move to Failed
```
POST /api/v1/manualimport/failed?sourcePath=/data/staging/bad.cbz&reason=Corrupt file
```
Moves a file to the failed folder with a reason.

---

## Decision Engine Endpoints

### Evaluate Candidates
```
POST /api/v1/decision/evaluate
Content-Type: application/json

{
  "candidates": [
    {
      "id": "candidate-1",
      "releaseTitle": "Amazing Spider-Man #001 (2022).cbz",
      "source": "preferred-source",
      "seriesTitle": "Amazing Spider-Man",
      "issueNumber": 1,
      "year": 2022,
      "format": "cbz",
      "size": 15000000,
      "isCollection": false
    }
  ],
  "target": {
    "seriesTitle": "Amazing Spider-Man",
    "issueNumber": 1,
    "year": 2022,
    "isCollection": false
  }
}
```
Evaluates and ranks candidates against a target. Returns ranked list with detailed explanations.

**Response (200 OK)**
```json
{
  "rankedCandidates": [
    {
      "candidate": { ... },
      "accepted": true,
      "score": 95,
      "rejectionReason": null,
      "explanation": {
        "summary": "Accepted with score 95 (base: 95, penalties: 0)",
        "baseScore": 95,
        "penalties": 0,
        "finalScore": 95,
        "scoringFactors": [
          { "name": "Format", "points": 20, "reason": "Preferred format: cbz" },
          { "name": "SeriesMatch", "points": 30, "reason": "Exact series title match" },
          { "name": "IssueMatch", "points": 25, "reason": "Exact issue match: #1" },
          { "name": "YearMatch", "points": 10, "reason": "Year match: 2022" }
        ],
        "checks": [
          { "checkName": "BannedWords", "passed": true, "details": "No banned words found" },
          { "checkName": "Size", "passed": true, "details": "Size 15.0MB within limits" }
        ]
      }
    }
  ],
  "bestCandidate": { ... },
  "shouldAutoGrab": true,
  "autoGrabReason": "Auto-grab approved with score 95",
  "totalCandidates": 1,
  "acceptedCandidates": 1,
  "rejectedCandidates": 0
}
```

### Evaluate Single Candidate
```
POST /api/v1/decision/evaluate/single
Content-Type: application/json

{
  "candidate": { ... },
  "target": { ... }
}
```
Evaluates a single candidate against a target. Returns detailed explanation.

### Explain Decisions
```
POST /api/v1/decision/explain
Content-Type: application/json

{
  "candidates": [ ... ],
  "target": { ... }
}
```
Returns verbose explanations for all candidate evaluations (debugging).

---

## Rejection Reasons (Enum)
- 10: UnsupportedFormat
- 11: FormatNotPreferred
- 20: TooSmall
- 21: TooLarge
- 30: BannedWordFound
- 31: MissingRequiredWord
- 40: SeriesMismatch
- 41: IssueMismatch
- 42: YearMismatch
- 50: QualityTooLow
- 51: DuplicateExists
- 52: BetterVersionExists
- 60: SourceDisabled
- 61: SourceNotTrusted
- 90: ManuallyRejected
- 99: Unknown

---

## Edition Types (Enum)
- 0: TradesPaperback
- 1: Hardcover
- 2: Omnibus
- 3: Compendium
- 4: AbsoluteEdition
- 5: DeluxeEdition
- 99: Other

## Series Status (Enum)
- 0: Continuing
- 1: Ended
- 2: Hiatus

---

## Provider Endpoints

### List All Providers
```
GET /api/v1/providers
```
Returns all configured providers (indexers and download clients).

### List Indexers
```
GET /api/v1/providers/indexers
```
Returns all indexer providers.

### List Download Clients
```
GET /api/v1/providers/downloadclients
```
Returns all download client providers.

### Get Provider by ID
```
GET /api/v1/providers/{id}
```
Returns a single provider.

### Get Available Implementations
```
GET /api/v1/providers/implementations
```
Returns all available provider implementations.

**Response (200 OK)**
```json
[
  {
    "name": "DdlProvider",
    "displayName": "DDL (Direct Download)",
    "description": "Direct download link provider for comic sites (Mylar3-compatible)",
    "category": "Indexer",
    "type": "Ddl",
    "requiresBaseUrl": true,
    "requiresApiKey": false,
    "requiresCredentials": false
  }
]
```

### Create Indexer
```
POST /api/v1/providers/indexers
Content-Type: application/json

{
  "name": "My DDL Provider",
  "implementation": "DdlProvider",
  "isEnabled": true,
  "baseUrl": "https://example.com",
  "apiKey": "optional-api-key",
  "username": "optional-user",
  "password": "optional-pass",
  "settings": "{\"customSetting\": \"value\"}",
  "tags": "ddl,comics"
}
```

### Create Download Client
```
POST /api/v1/providers/downloadclients
Content-Type: application/json

{
  "name": "HTTP Downloader",
  "implementation": "HttpDownloadClient",
  "isEnabled": true
}
```

### Update Provider
```
PUT /api/v1/providers/{id}
Content-Type: application/json

{
  "name": "Updated Name",
  "isEnabled": false
}
```

### Delete Provider
```
DELETE /api/v1/providers/{id}
```

### Enable/Disable Provider
```
POST /api/v1/providers/{id}/enable?enabled=true
```

### Reorder Providers
```
POST /api/v1/providers/indexers/reorder
POST /api/v1/providers/downloadclients/reorder
Content-Type: application/json

{
  "orderedIds": [3, 1, 2]
}
```

### Test Provider
```
POST /api/v1/providers/{id}/test
```
Test an existing provider's connection.

**Response (200 OK)**
```json
{
  "success": true,
  "message": "Connection successful",
  "sampleResultCount": 10,
  "latencyMs": 245,
  "errors": []
}
```

### Test New Provider (Before Saving)
```
POST /api/v1/providers/test
Content-Type: application/json

{
  "name": "Test Provider",
  "implementation": "DdlProvider",
  "baseUrl": "https://example.com"
}
```

---

## Provider Types (Enum)
- 1: Ddl (Direct Download)
- 2: Rss (RSS/Atom Feed)
- 3: Newznab
- 4: Torznab
- 10: HttpDownload
- 11: Torrent (future)
- 12: Usenet (future)

## Provider Categories (Enum)
- 1: Indexer
- 2: DownloadClient

## Health Status (Enum)
- 0: Healthy
- 1: Degraded
- 2: Unhealthy
- 3: Unknown
- 4: Disabled

---

## DDL Candidate Normalization (Internal)

### DdlCandidate Model
DDL candidates represent releases discovered from DDL sites:

```json
{
  "id": "unique-id",
  "releaseTitle": "Amazing Spider-Man 001 (2022) (Digital) (Zone-Empire).cbz",
  "sourceSite": "GettyComics",
  "sourceUrl": "https://example.com/page/12345",
  "parsedInfo": {
    "seriesTitle": "Amazing Spider-Man",
    "issueNumber": 1,
    "volumeNumber": null,
    "year": 2022,
    "publisher": null,
    "format": "cbz",
    "isCollection": false,
    "editionType": null,
    "issueRange": null,
    "releaseGroup": "Zone-Empire",
    "quality": "Digital",
    "confidence": 75
  },
  "downloadLinks": [
    {
      "url": "https://download.example.com/file.cbz",
      "linkType": 0,
      "hostName": null,
      "isVerified": false,
      "priority": 0
    }
  ],
  "size": 15000000,
  "dateFound": "2026-02-02T04:00:00Z",
  "qualityScore": 75,
  "tags": ["Digital", "2022"],
  "isFiltered": false,
  "filterReason": null
}
```

### DDL Filtering Rules (Mylar3 Defaults)
- **Banned Words**: sample, preview (instant rejection)
- **Size Limits**:
  - Singles: 1MB - 200MB
  - Collections: 5MB - 2GB
- **Blocked Formats**: pdf
- **Preferred Formats**: cbz, cbr

### DDL Link Types (Enum)
- 0: Direct
- 1: Redirect
- 2: Hoster
- 3: Magnet (future)

---

## DDL Download Service (Internal)

### Download Options
```json
{
  "destinationFolder": "/downloads",
  "customFilename": null,
  "maxRetries": 3,
  "retryDelayMs": 1000,
  "maxRetryDelayMs": 30000,
  "timeoutSeconds": 300,
  "enableResume": true,
  "userAgent": "Mozilla/5.0...",
  "customHeaders": {},
  "cookies": {},
  "verifyDownload": true,
  "minExpectedSize": null,
  "maxExpectedSize": null
}
```

### Download Result
```json
{
  "downloadId": "abc-123",
  "success": true,
  "filePath": "/downloads/Batman_001.cbz",
  "fileName": "Batman_001.cbz",
  "fileSize": 52428800,
  "duration": "00:02:30",
  "bytesPerSecond": 349525.33,
  "retryAttempts": 0,
  "failureReason": 0,
  "errorMessage": null,
  "httpStatusCode": null,
  "sourceUrl": "https://...",
  "wasResumed": false
}
```

### Download Failure Reasons (Enum)
| Value | Name | Description |
|-------|------|-------------|
| 0 | None | No failure |
| 10 | Timeout | Network timeout |
| 11 | ConnectionFailed | Connection failed |
| 12 | DnsFailure | DNS resolution failed |
| 20 | NotFound | HTTP 404 |
| 21 | Unauthorized | HTTP 401/403 |
| 22 | RateLimited | HTTP 429 |
| 23 | ServerError | HTTP 5xx |
| 30 | EmptyFile | Downloaded file is empty |
| 31 | FileTooSmall | File below minimum size |
| 32 | FileTooLarge | File exceeds maximum size |
| 33 | HtmlErrorPage | File is HTML error page |
| 34 | VerificationFailed | Magic bytes check failed |
| 40 | DiskError | Disk full or write error |
| 50 | Cancelled | Download cancelled |
| 60 | MaxRetriesExceeded | All retries exhausted |
| 70 | NoValidLinks | No download links available |
| 99 | Unknown | Unknown error |

### Download States (Enum)
| Value | Name |
|-------|------|
| 0 | Queued |
| 1 | Connecting |
| 2 | Downloading |
| 3 | Paused |
| 4 | Retrying |
| 5 | Verifying |
| 10 | Completed |
| 11 | Failed |
| 12 | Cancelled |

---

## DDL Search Service (Internal)

### Site Adapter System
DDL site adapters provide site-specific parsing for comic DDL sources.

#### Registered Adapters
| Adapter | Site Type | Default Rate Limit |
|---------|-----------|-------------------|
| MockDdlSiteAdapter | MockDdl | 60/min |
| GettyComicsSiteAdapter | GettyComics | 10/min |

### Search Query
```json
{
  "seriesTitle": "Amazing Spider-Man",
  "issueNumber": 1,
  "volumeNumber": null,
  "year": 2022,
  "rawQuery": null,
  "collectionsOnly": false,
  "limit": 50,
  "offset": 0
}
```

### Search Result
```json
{
  "candidates": [...],
  "success": true,
  "errorMessage": null,
  "totalResults": 25,
  "hasMore": false,
  "duration": "00:00:01.234",
  "sourceSite": "MockDdl"
}
```

### Aggregated Search Result (Multi-Site)
```json
{
  "allCandidates": [...],
  "resultsBySite": {
    "MockDdl": {...},
    "GettyComics": {...}
  },
  "successfulSites": ["MockDdl"],
  "failedSites": ["GettyComics"],
  "totalRawCandidates": 30,
  "duplicatesRemoved": 5,
  "totalDuration": "00:00:02.500",
  "warnings": []
}
```

### Site Test Result
```json
{
  "success": true,
  "message": "Connection successful",
  "authenticationPassed": null,
  "sampleResultCount": 10,
  "latencyMs": 245,
  "warnings": [],
  "errorDetails": null
}
```

---

## DDL Import Service (EPIC 4.2.4)

The DDL Import Service handles post-download processing and import handoff, bridging the DDL download pipeline to the import pipeline.

### Process Download
```
POST /api/v1/ddl/import/process
Content-Type: application/json

{
  "filePath": "/downloads/Batman_001.cbz",
  "candidate": {
    "id": "candidate-123",
    "releaseTitle": "Batman 001 (2016) (Digital).cbz",
    "sourceSite": "GettyComics",
    "parsedInfo": {
      "seriesTitle": "Batman",
      "issueNumber": 1,
      "year": 2016,
      "format": "cbz",
      "isCollection": false
    }
  },
  "options": {
    "autoImportEnabled": true,
    "autoImportMinConfidence": 80,
    "requireSeriesMatch": true,
    "requireIssueMatch": true
  }
}
```
Processes a completed download: verifies file, moves to staging, auto-matches to series/issue, and either auto-imports or queues for manual review.

**Response (200 OK)**
```json
{
  "importId": "import-456",
  "success": true,
  "state": "Completed",
  "libraryPath": "/library/Batman/Batman_001.cbz",
  "seriesId": 1,
  "seriesTitle": "Batman",
  "issueId": 1,
  "issueNumber": 1,
  "fileAssetId": 1,
  "historyEventId": 1,
  "matchConfidence": 95,
  "pendingManualReview": false,
  "processedAt": "2026-02-02T05:00:00Z"
}
```

### Verify File
```
POST /api/v1/ddl/import/verify
Content-Type: application/json

{
  "filePath": "/downloads/Batman_001.cbz",
  "candidate": { ... }
}
```
Verifies a downloaded file is valid for import (checks magic bytes, size, detects HTML error pages).

**Response (200 OK)**
```json
{
  "isValid": true,
  "filePath": "/downloads/Batman_001.cbz",
  "fileSize": 52428800,
  "detectedFormat": "cbz",
  "formatSupported": true,
  "errorMessage": null,
  "warnings": []
}
```

### Move to Staging
```
POST /api/v1/ddl/import/stage
Content-Type: application/json

{
  "sourcePath": "/downloads/Batman_001.cbz",
  "candidate": { ... }
}
```
Moves a verified file to the staging folder.

### Auto-Match Candidate
```
POST /api/v1/ddl/import/match
Content-Type: application/json

{
  "candidate": { ... }
}
```
Auto-matches a candidate to existing series/issue in the database.

**Response (200 OK)**
```json
{
  "matchFound": true,
  "confidence": 95,
  "seriesId": 1,
  "seriesTitle": "Batman",
  "issueId": 1,
  "issueNumber": 1,
  "isCollection": false,
  "explanation": "Matched to issue: Batman #1",
  "confidenceReductions": []
}
```

### Execute Import
```
POST /api/v1/ddl/import/execute
Content-Type: application/json

{
  "stagedFilePath": "/staging/Batman_001.cbz",
  "candidate": { ... },
  "seriesId": 1,
  "issueId": 1
}
```
Executes import for a staged file with manually specified series/issue.

### Get Pending Imports
```
GET /api/v1/ddl/import/pending
```
Returns all imports awaiting manual review.

**Response (200 OK)**
```json
[
  {
    "id": "pending-789",
    "stagingPath": "/staging/Unknown_Comic.cbz",
    "filename": "Unknown_Comic.cbz",
    "fileSize": 25000000,
    "suggestedSeriesId": null,
    "suggestedSeriesTitle": null,
    "isCollection": false,
    "stagedAt": "2026-02-02T04:30:00Z",
    "reviewReason": "No series found matching 'Unknown Comic'"
  }
]
```

### Approve Pending Import
```
POST /api/v1/ddl/import/pending/{id}/approve
Content-Type: application/json

{
  "pendingImportId": "pending-789",
  "seriesId": 1,
  "issueId": 1
}
```
Approves a pending import with specified series/issue mapping.

### Reject Pending Import
```
POST /api/v1/ddl/import/pending/{id}/reject
Content-Type: application/json

{
  "pendingImportId": "pending-789",
  "reason": "Not wanted",
  "deleteFile": true
}
```
Rejects a pending import and optionally deletes the file.

---

## Import States (Enum)
| Value | Name | Description |
|-------|------|-------------|
| 0 | Pending | Initial state |
| 1 | Verifying | Verifying downloaded file |
| 2 | MovingToStaging | Moving to staging folder |
| 3 | Matching | Matching to series/issue |
| 4 | PendingReview | Awaiting manual review |
| 5 | Importing | Importing to library |
| 10 | Completed | Import completed successfully |
| 20 | VerificationFailed | Verification failed |
| 21 | StagingFailed | Staging failed |
| 22 | MatchingFailed | Matching failed |
| 23 | ImportFailed | Import failed |
| 30 | Rejected | Rejected by user |

---

## Mylar3 Import Service (EPIC 4.3)

The Mylar3 Import Service handles parsing and importing Mylar3 config.ini files to create DDL provider configurations.

### Parse Mylar3 Config
```
POST /api/v1/mylar3/parse
Content-Type: application/json

{
  "configContent": "[DDL-1]\nname = My Provider\nsite_type = GettyComics\nurl = https://example.com\nenabled = true"
}
```
Parses Mylar3 config.ini content and extracts DDL provider configurations.

**Response (200 OK)**
```json
{
  "success": true,
  "ddlProviders": [
    {
      "name": "My Provider",
      "siteType": "GettyComics",
      "baseUrl": "https://example.com",
      "isEnabled": true,
      "priority": 1,
      "hasPassword": false,
      "hasApiKey": false,
      "settings": {
        "siteType": "GettyComics",
        "rateLimitPerMinute": 10,
        "timeoutSeconds": 30,
        "maxRetries": 3
      }
    }
  ],
  "generalSettings": null,
  "unmappedSections": [],
  "unmappedSettings": {},
  "warnings": []
}
```

### Parse Config from File
```
POST /api/v1/mylar3/parse/file
Content-Type: application/json

{
  "filePath": "/path/to/mylar3/config.ini"
}
```

### Validate Import
```
POST /api/v1/mylar3/validate
Content-Type: application/json

{
  "configContent": "..."
}
```
Validates Mylar3 config import against current system state.

**Response (200 OK)**
```json
{
  "isValid": true,
  "errors": [],
  "warnings": ["Provider 'Existing' already exists"],
  "providersToCreate": ["New Provider"],
  "existingProviders": ["Existing"],
  "settingDeviations": ["New Provider: Custom rate limit 5/min (default: 10)"]
}
```

### Execute Import
```
POST /api/v1/mylar3/import
Content-Type: application/json

{
  "configContent": "...",
  "overwriteExisting": false,
  "importDisabled": true,
  "importCredentials": true,
  "namePrefix": "[Mylar3] ",
  "validateFirst": true
}
```
Executes Mylar3 config import, creating DDL providers in the database.

**Response (200 OK)**
```json
{
  "success": true,
  "providersCreated": 2,
  "providersUpdated": 0,
  "providersSkipped": 1,
  "createdProviderIds": [5, 6],
  "details": [
    { "name": "[Mylar3] Provider One", "action": "Created", "providerId": 5 },
    { "name": "[Mylar3] Provider Two", "action": "Created", "providerId": 6 },
    { "name": "[Mylar3] Existing", "action": "Skipped" }
  ]
}
```

### Get DDL Provider Defaults
```
GET /api/v1/mylar3/defaults
```
Returns Mylar3-compatible default settings for all supported DDL site types.

**Response (200 OK)**
```json
[
  {
    "siteType": "GettyComics",
    "settings": {
      "siteType": "GettyComics",
      "rateLimitPerMinute": 10,
      "timeoutSeconds": 30,
      "downloadTimeoutSeconds": 300,
      "maxRetries": 3,
      "retryDelayMs": 1000,
      "useExponentialBackoff": true,
      "requiresAuth": false,
      "enableCookies": true
    }
  },
  {
    "siteType": "ReadComicOnline",
    "settings": {
      "siteType": "ReadComicOnline",
      "rateLimitPerMinute": 5,
      "timeoutSeconds": 45,
      "downloadTimeoutSeconds": 600,
      "maxRetries": 3
    }
  }
]
```

### Get Defaults for Site Type
```
GET /api/v1/mylar3/defaults/{siteType}
```
Returns Mylar3-compatible default settings for a specific DDL site type.

---

## DDL Provider Settings

### DdlProviderSettings Model
```json
{
  "siteType": "GettyComics",
  "rateLimitPerMinute": 10,
  "timeoutSeconds": 30,
  "downloadTimeoutSeconds": 300,
  "maxRetries": 3,
  "retryDelayMs": 1000,
  "useExponentialBackoff": true,
  "userAgent": null,
  "enableCookies": true,
  "customCookies": null,
  "customHeaders": null,
  "requiresAuth": false,
  "authMethod": "None",
  "loginUrl": null,
  "autoGrabEnabled": true,
  "autoGrabMinScore": 80,
  "searchCollections": true,
  "searchSingles": true,
  "formatPreference": ["cbz", "cbr"],
  "bannedWords": ["sample", "preview"],
  "requiredWords": [],
  "minSizeSingles": 1000000,
  "maxSizeSingles": 200000000,
  "minSizeCollections": 5000000,
  "maxSizeCollections": 2000000000
}
```

### DDL Auth Methods (Enum)
| Value | Name | Description |
|-------|------|-------------|
| 0 | None | No authentication |
| 1 | Basic | HTTP Basic auth |
| 2 | Cookie | Cookie-based (login form) |
| 3 | ApiKey | API key auth |
| 4 | OAuth2 | OAuth2 auth |

### Supported Site Types
| Site Type | Default Rate Limit | Auth Required | Notes |
|-----------|-------------------|---------------|-------|
| GettyComics | 10/min | No | Standard defaults |
| ReadComicOnline | 5/min | No | More restrictive |
| GetComics | 10/min | No | Standard defaults |
| Generic | 10/min | Varies | Fallback type |

---

## Settings Endpoints

### Get UI Settings
```
GET /api/v1/settings/ui
```
Returns UI-specific settings including theme preferences.

**Response (200 OK)**
```json
{
  "theme": "dark",
  "pageSize": 50,
  "showFileSizes": true,
  "relativeTimestamps": true
}
```

### Update UI Settings
```
PUT /api/v1/settings/ui
Content-Type: application/json

{
  "theme": "light",
  "pageSize": 100,
  "showFileSizes": true,
  "relativeTimestamps": true
}
```
Valid theme values: `dark`, `light`, `system`
Valid pageSize range: 10-500

### Get General Settings
```
GET /api/v1/settings/general
```
Returns general application settings including naming formats and folder paths.

**Response (200 OK)**
```json
{
  "seriesFolderFormat": "{Series Title} ({Year})",
  "issueFileFormat": "{Series Title} #{Issue} ({Year})",
  "collectionFileFormat": "{Series Title} - {Edition Type} Vol. {Volume} ({Year})",
  "comicLibraryPath": "/comics",
  "downloadFolder": "/downloads",
  "stagingFolder": "/staging",
  "autoMoveToStaging": true
}
```

### Update General Settings
```
PUT /api/v1/settings/general
Content-Type: application/json

{
  "seriesFolderFormat": "{Publisher}/{Series Title}",
  "issueFileFormat": "{Series Title} - #{Issue}",
  "collectionFileFormat": "{Series Title} - {Edition Type}",
  "comicLibraryPath": "/my/comics",
  "downloadFolder": "/my/downloads",
  "stagingFolder": "/my/staging",
  "autoMoveToStaging": false
}
```

### Get Folder Settings
```
GET /api/v1/settings/folders
```
Returns folder-specific settings as a convenience endpoint.

**Response (200 OK)**
```json
{
  "comicLibraryPath": "/comics",
  "downloadFolder": "/downloads",
  "stagingFolder": "/staging",
  "autoMoveToStaging": true
}
```

### Update Folder Settings
```
PUT /api/v1/settings/folders
Content-Type: application/json

{
  "downloadFolder": "/new/downloads",
  "autoMoveToStaging": false
}
```
Supports partial updates - only specified fields are changed.

### Get Naming Format Tokens
```
GET /api/v1/settings/naming/tokens
```
Returns available tokens for naming format configuration.

**Response (200 OK)**
```json
{
  "seriesFolderTokens": [
    { "token": "{Series Title}", "description": "The title of the series", "example": "Batman" },
    { "token": "{Series Year}", "description": "The year the series started", "example": "2020" },
    { "token": "{Publisher}", "description": "The publisher name", "example": "DC" },
    { "token": "{Status}", "description": "Series status (Continuing, Ended, Hiatus)", "example": "Continuing" }
  ],
  "issueFileTokens": [
    { "token": "{Series Title}", "description": "The title of the series", "example": "Batman" },
    { "token": "{Issue}", "description": "Issue number (padded)", "example": "001" },
    { "token": "{Issue Title}", "description": "Title of the specific issue", "example": "The Court of Owls" },
    { "token": "{Year}", "description": "Release year of the issue", "example": "2020" },
    { "token": "{Publisher}", "description": "The publisher name", "example": "DC" },
    { "token": "{Quality}", "description": "Quality tag (Digital, Webrip, etc.)", "example": "Digital" }
  ],
  "collectionFileTokens": [
    { "token": "{Series Title}", "description": "The title of the series", "example": "Batman" },
    { "token": "{Edition Type}", "description": "Type of collection (TPB, HC, Omnibus)", "example": "TPB" },
    { "token": "{Volume}", "description": "Volume number", "example": "01" },
    { "token": "{Collection Title}", "description": "Title of the collection", "example": "Court of Owls" },
    { "token": "{Year}", "description": "Release year of the collection", "example": "2020" },
    { "token": "{Publisher}", "description": "The publisher name", "example": "DC" }
  ]
}
```

### Get Setting by Key
```
GET /api/v1/settings/{key}
```
Returns a specific setting value by key.

**Response (200 OK)**
```json
{
  "key": "custom.setting.key",
  "value": "setting-value"
}
```

**Response (404 Not Found)** if setting doesn't exist.

### Set Setting by Key
```
PUT /api/v1/settings/{key}
Content-Type: application/json

{
  "value": "new-value"
}
```
Creates or updates a setting.

### Delete Setting by Key
```
DELETE /api/v1/settings/{key}
```
Removes a setting. Returns 204 on success, 404 if not found.

---

## OpenAPI / Swagger

- **Swagger UI**: `GET /swagger`
- **OpenAPI Spec**: `GET /swagger/v1/swagger.json`
