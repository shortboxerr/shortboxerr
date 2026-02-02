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

## OpenAPI / Swagger

- **Swagger UI**: `GET /swagger`
- **OpenAPI Spec**: `GET /swagger/v1/swagger.json`
