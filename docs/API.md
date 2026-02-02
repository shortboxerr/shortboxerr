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

## OpenAPI / Swagger

- **Swagger UI**: `GET /swagger`
- **OpenAPI Spec**: `GET /swagger/v1/swagger.json`
