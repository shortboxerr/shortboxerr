# Worklog

## Iteration 009 (2026-02-02)
**EPIC 4.2.4: DDL → Import Handoff - COMPLETED**

### Commits
1. `feat: add DDL import service with auto-match and manual review (EPIC 4.2.4)`
2. `chore: update docs for iteration 009 completion`

### Deliverables
- ✅ Import Service Interface:
  - IDdlImportService: Post-download processing and import handoff
  - ProcessDownloadAsync: Full pipeline from download to import
  - VerifyFileAsync: File validation (magic bytes, size, HTML detection)
  - MoveToStagingAsync: Move verified files to staging
  - AutoMatchAsync: Match candidates to series/issue
  - ExecuteImportAsync: Import to library with history events
- ✅ Post-Download Verification:
  - Magic bytes detection (ZIP/CBZ, RAR/CBR, PDF, 7z)
  - HTML error page detection (prevents saving error pages as comics)
  - File size validation (minimum thresholds for singles/collections)
  - Empty file detection
- ✅ Auto-Match System:
  - Series matching by normalized title
  - Issue matching by number
  - Edition matching for collections
  - Confidence scoring with reduction reasons
  - Year and publisher bonuses
- ✅ Auto-Import vs Manual Review:
  - Configurable auto-import threshold (default: 80%)
  - RequireSeriesMatch setting
  - RequireIssueMatch setting (for singles)
  - Pending import queue for manual review
- ✅ Pending Import Management:
  - GetPendingImportsAsync: List all pending
  - ApprovePendingImportAsync: Approve with series/issue override
  - RejectPendingImportAsync: Reject with optional file deletion
- ✅ History Integration:
  - FileAsset creation on import
  - HistoryEvent (DdlImportCompleted) on success
  - Download→Import chain tracking
- ✅ API Endpoints:
  - POST /api/v1/ddl/import/process
  - POST /api/v1/ddl/import/verify
  - POST /api/v1/ddl/import/stage
  - POST /api/v1/ddl/import/match
  - POST /api/v1/ddl/import/execute
  - GET /api/v1/ddl/import/pending
  - POST /api/v1/ddl/import/pending/{id}/approve
  - POST /api/v1/ddl/import/pending/{id}/reject
- ✅ 247 tests passing (18 new)

### Import States
| State | Value | Description |
|-------|-------|-------------|
| Pending | 0 | Initial state |
| Verifying | 1 | Verifying downloaded file |
| MovingToStaging | 2 | Moving to staging folder |
| Matching | 3 | Matching to series/issue |
| PendingReview | 4 | Awaiting manual review |
| Importing | 5 | Importing to library |
| Completed | 10 | Successfully imported |
| VerificationFailed | 20 | Verification failed |
| StagingFailed | 21 | Staging failed |
| MatchingFailed | 22 | Matching failed |
| ImportFailed | 23 | Import failed |
| Rejected | 30 | Rejected by user |

### File Format Detection
| Format | Magic Bytes | Supported |
|--------|-------------|-----------|
| CBZ/ZIP | 50 4B | ✅ |
| CBR/RAR | 52 61 72 | ✅ |
| CB7/7z | 37 7A BC AF | ✅ |
| PDF | 25 50 44 46 | ❌ |
| HTML | <!doctype, <html | ❌ (rejected) |

---

## Iteration 008 (2026-02-02)
**EPIC 4.2.3: DDL Download Execution - COMPLETED**

### Commits
1. `feat: add DDL download service with retry logic (EPIC 4.2.3)`
2. `chore: update docs for iteration 008 completion`

### Deliverables
- ✅ Download Service:
  - IDdlDownloadService: Download operations interface
  - DdlDownloadService: Full HTTP download implementation
  - Candidate-based downloads with automatic link selection
  - URL-based downloads with custom options
  - Active download tracking and cancellation
  - Download history (last 1000 entries)
- ✅ Download Features:
  - Configurable timeouts (default: 5 min, Mylar3-compatible)
  - User-Agent configuration
  - Cookie/session handling for authenticated sites
  - Resume support (HTTP Range headers)
  - Progress callbacks with ETA
- ✅ Retry Semantics:
  - Configurable retry count (default: 3, Mylar3 default)
  - Exponential backoff with jitter
  - Alternate mirror fallback on primary failure
  - Smart retry logic (only retries transient failures)
- ✅ Failure Handling:
  - 15+ failure reason classifications
  - HTTP status code tracking
  - File verification (magic bytes)
  - HTML error page detection
  - Detailed error messages
- ✅ 229 tests passing (15 new)

### Failure Reasons
| Code | Reason | Retryable |
|------|--------|-----------|
| 10 | Timeout | Yes |
| 11 | ConnectionFailed | Yes |
| 12 | DnsFailure | Yes |
| 20 | NotFound | No |
| 21 | Unauthorized | No |
| 22 | RateLimited | Yes |
| 23 | ServerError | Yes |
| 30-34 | Verification | No |
| 50 | Cancelled | No |
| 60 | MaxRetriesExceeded | No |

---

## Iteration 007 (2026-02-02)
**EPIC 4.2.1: DDL Discovery & Search - COMPLETED**

### Commits
1. `feat: add DDL site adapters and search service (EPIC 4.2.1)`
2. `chore: update docs for iteration 007 completion`

### Deliverables
- ✅ Site Adapter System:
  - IDdlSiteAdapter: Interface for site-specific adapters
  - BaseDdlSiteAdapter: Common HTTP client, rate limiting, configuration
  - MockDdlSiteAdapter: Testing adapter with sample data
  - GettyComicsSiteAdapter: Real-site adapter pattern implementation
- ✅ Search Service:
  - IDdlSearchService: Multi-site search coordination
  - DdlSearchService: Aggregation, deduplication, rate limiting
  - Site-specific and global search support
  - Link extraction and verification
- ✅ Supporting Types:
  - DdlSearchQuery: Series, issue, year, collections filter
  - DdlSearchResult: Candidates, pagination, errors
  - DdlAggregatedSearchResult: Multi-site merged results
  - DdlSiteConfiguration: URL, auth, rate limits, timeout
  - DdlSiteCredentials: Username, password, API key, cookies
  - DdlSiteTestResult: Connection health testing
- ✅ Adapter Factory:
  - IDdlSiteAdapterFactory: Registry interface
  - DdlSiteAdapterFactory: Built-in adapter registration
  - Site type detection from URLs
- ✅ 214 tests passing (31 new)

### Site Adapters
| Adapter | Site Type | Rate Limit | Auth Required |
|---------|-----------|------------|---------------|
| MockDdlSiteAdapter | MockDdl | 60/min | No |
| GettyComicsSiteAdapter | GettyComics | 10/min | No |

---

## Iteration 006 (2026-02-02)
**EPIC 4.2.2: DDL Candidate Normalization - COMPLETED**

### Commits
1. `feat: add DDL candidate normalization (EPIC 4.2.2)`
2. `test: add golden test fixtures for DDL parsing (EPIC 4.2.2)`
3. `chore: update docs for iteration 006 completion`

### Deliverables
- ✅ Core Models:
  - DdlCandidate (release candidate with DDL-specific fields)
  - DdlParsedInfo (structured metadata from release titles)
  - DdlDownloadLink (download links with type and priority)
  - DdlFilterSettings (configurable filtering rules)
- ✅ DDL Release Parser:
  - Series title extraction (handles hyphenated names)
  - Issue number extraction (#001, 001, Issue 1)
  - Volume number extraction (Vol. 1, v1)
  - Year extraction (parentheses and trailing)
  - Collection detection (TPB, HC, Omnibus, Deluxe, etc.)
  - Publisher detection (Marvel, DC, Image, etc.)
  - Quality tags (Digital, Webrip, etc.)
  - Release group extraction
  - Confidence scoring
- ✅ DDL Filtering (Mylar3 Defaults):
  - Banned words (sample, preview)
  - Required words enforcement
  - Format filtering (blocked/preferred)
  - Size limits (singles: 1-200MB, collections: 5MB-2GB)
  - Parse confidence threshold
  - Series title requirement
  - Blocked release groups
- ✅ Services Registered:
  - IDdlReleaseParser / DdlReleaseParser
  - IDdlFilter / DdlFilter
- ✅ Golden Test Fixtures:
  - 14 parsing test cases (Mylar3 parity)
  - 10 filtering test cases
- ✅ 183 tests passing (86 new DDL tests)

### DDL Link Types
| Type | Value | Description |
|------|-------|-------------|
| Direct | 0 | Direct download to file |
| Redirect | 1 | Redirect to actual download |
| Hoster | 2 | File hosting service |
| Magnet | 3 | Magnet link (future) |

---

## Iteration 005 (2026-02-02)
**EPIC 4.1: Provider Abstractions - COMPLETED**

### Commits
1. `feat: add provider abstractions and CRUD endpoints (EPIC 4.1)`
2. `chore: update docs for iteration 005 completion (EPIC 4.1)`

### Deliverables
- ✅ Core Interfaces:
  - IProvider (base abstraction)
  - IIndexerProvider (search/discovery)
  - IDownloadProvider (acquisition)
  - IProviderManager (registry/CRUD)
  - IProviderFactory (implementation factory)
- ✅ Entity & Persistence:
  - ProviderDefinition entity
  - AddProviders migration
  - DbContext integration
- ✅ Infrastructure:
  - ProviderManager implementation
  - ProviderFactory with implementation registry
  - Placeholder providers (DdlProvider, RssIndexer, HttpDownloadClient)
- ✅ API Endpoints (14 total):
  - GET /api/v1/providers (all)
  - GET /api/v1/providers/indexers
  - GET /api/v1/providers/downloadclients
  - GET /api/v1/providers/implementations
  - GET /api/v1/providers/{id}
  - POST /api/v1/providers/indexers (create)
  - POST /api/v1/providers/downloadclients (create)
  - PUT /api/v1/providers/{id} (update)
  - DELETE /api/v1/providers/{id}
  - POST /api/v1/providers/{id}/test
  - POST /api/v1/providers/test (test before save)
  - POST /api/v1/providers/{id}/enable
  - POST /api/v1/providers/indexers/reorder
  - POST /api/v1/providers/downloadclients/reorder
- ✅ Test Infrastructure:
  - CustomWebApplicationFactory with in-memory SQLite
  - Isolated test databases for parallel execution
- ✅ 97 tests passing (14 new provider tests)

### Enums Defined
| Enum | Values |
|------|--------|
| ProviderType | Ddl, Rss, Newznab, Torznab, HttpDownload, Torrent, Usenet |
| ProviderCategory | Indexer, DownloadClient |
| HealthStatus | Healthy, Degraded, Unhealthy, Unknown, Disabled |
| DownloadState | Queued, Downloading, Paused, Completed, Failed, Cancelled, Retrying, Processing |

---

## Iteration 004 (2026-02-02)
**EPIC 3: DecisionEngine - COMPLETED**

### Commits
1. `feat: add DecisionEngine with candidate evaluation and ranking (EPIC 3)`
2. `test: add golden test harness for DecisionEngine (EPIC 3)`
3. `chore: update docs for iteration 004 completion`

### Deliverables
- ✅ Core Models:
  - Candidate (release candidate with metadata)
  - CandidateEvaluation (evaluation result with score)
  - CandidateTarget (what we're matching against)
  - RejectionReason (enum of all rejection types)
  - DecisionExplanation (detailed scoring breakdown)
  - ScoringFactor/CheckResult (individual evaluation components)
  - DecisionEngineSettings (configurable thresholds and preferences)
- ✅ Decision Engine Service:
  - Evaluate(): Single candidate evaluation
  - EvaluateAndRank(): Batch evaluation with deterministic ranking
  - GetBestCandidate(): Returns top acceptable candidate
  - CheckAutoGrab(): Threshold and margin checks
- ✅ Rejection Checks:
  - Banned words filter (sample, preview)
  - Required words enforcement
  - Format validation (cbz, cbr)
  - Size limits (min/max for singles and collections)
- ✅ Scoring Factors:
  - Format preference (cbz > cbr, configurable order)
  - Exact/partial series match
  - Exact issue number match
  - Year match (exact and close)
  - Source priority (configurable priority list)
- ✅ API Endpoints:
  - POST /api/v1/decision/evaluate (batch evaluate)
  - POST /api/v1/decision/evaluate/single
  - POST /api/v1/decision/explain (verbose explanations)
- ✅ Golden Test Harness:
  - Fixture-based testing with JSON test cases
  - Individual tests for each golden scenario
  - Settings configurable per fixture file
- ✅ 83 passing tests (38 new DecisionEngine tests)

### Configuration (DecisionEngineSettings)
| Setting | Default | Description |
|---------|---------|-------------|
| AutoGrabThreshold | 80 | Min score for auto-grab |
| ManualChoiceMargin | 10 | Score gap requiring manual review |
| FormatPreferenceOrder | [cbz, cbr] | Format ranking |
| BannedWords | [sample, preview] | Reject if found |
| MinSizeBytesSingles | 1MB | Min file size |
| MaxSizeBytesSingles | 200MB | Max file size |

---

## Iteration 003 (2026-02-02)
**EPIC 2: Import Pipeline - COMPLETED**

### Commits
1. `chore: change default port from 7878 to 8585`
2. `chore: update CI workflow port from 7878 to 8585`
3. `feat: add staging folder service and manual import endpoints (EPIC 2)`
4. `test: add filename parser and manual import endpoint tests`
5. `chore: update docs for iteration 003 completion`

### Deliverables
- ✅ Core Models:
  - StagedItem (file in staging folder)
  - ParsedComicInfo (metadata from filename)
  - ImportPreview/ImportResult (import operation models)
- ✅ Services:
  - FilenameParser: parses comic filenames for singles and collections
  - StagingService: scans staging folder, previews, executes imports
- ✅ Manual Import Endpoints:
  - GET /api/v1/manualimport (scan staging)
  - POST /api/v1/manualimport/preview
  - POST /api/v1/manualimport (execute)
  - POST /api/v1/manualimport/failed
- ✅ Features:
  - Filename parsing (issue numbers, volumes, years, publishers)
  - Collection detection (TPB, HC, Omnibus, etc.)
  - Automatic series matching
  - Atomic file moves
  - History event logging
- ✅ 45 passing tests (29 new tests)

---

## Iteration 002 (2026-02-02)
**EPIC 1: Domain + Persistence - COMPLETED**

### Commits
1. `chore: verify EPIC 0 hygiene (Makefile + commit-msg hook)`
2. `feat: add domain entities for EPIC 1`
3. `feat: add CRUD endpoints for Series and Editions`
4. `chore: update docs for iteration 002 completion`

### Deliverables
- ✅ Domain Entities:
  - Series (comic book series with metadata)
  - Issue (single issues with release tracking)
  - EditionTitle (collected editions as first-class entities)
  - EditionContent (maps issues to editions)
  - FileAsset (file on disk with hash tracking)
  - HistoryEvent (audit log)
- ✅ EF Core mappings with full relationship configuration
- ✅ AddDomainEntities migration
- ✅ CRUD endpoints:
  - GET/POST/PUT/DELETE /api/v1/series
  - GET/POST/PUT/DELETE /api/v1/editions
- ✅ Paged results (Arr-like pattern)
- ✅ Sorting and filtering support
- ✅ DTOs with entity mapping
- ✅ 16 passing tests (12 new endpoint tests)

### EPIC 0 Hygiene Verification
- ✅ Makefile functional (`make build`, `make test`)
- ✅ commit-msg hook enforcing conventional commits

---

## Iteration 001 (2026-02-02)
**EPIC 0: Repo Skeleton - COMPLETED**

### Commits
1. `feat: create .NET solution with project structure`
2. `feat: add health endpoint, Swagger, and system status API`
3. `feat: add EF Core SQLite migrations scaffold`
4. `chore: add .gitignore and remove build artifacts from tracking`
5. `feat: add production Dockerfile with multi-stage build`
6. `chore: add GitHub Actions CI workflow`
7. `chore: update docs for iteration 001 completion`

### Deliverables
- ✅ .NET solution: Shortboxerr.sln
  - src/Shortboxerr.Api (ASP.NET Core Web API)
  - src/Shortboxerr.Core (domain entities)
  - src/Shortboxerr.Infrastructure (EF Core + SQLite)
  - tests/Shortboxerr.Tests (xUnit integration tests)
- ✅ Health endpoint: GET /health (JSON response with status)
- ✅ Swagger UI: /swagger with OpenAPI v1 spec
- ✅ System status: GET /api/v1/system/status
- ✅ Ping endpoint: GET /ping
- ✅ SQLite migrations scaffold (InitialCreate with SystemSettings)
- ✅ Auto-migration on startup
- ✅ Database health check integration
- ✅ Production Dockerfile (multi-stage, non-root user)
- ✅ docker-compose.yml for deployment
- ✅ GitHub Actions CI workflow (build + test + Docker)
- ✅ 4 passing integration tests

### Assumptions Made
- None new (used existing assumptions from docs/ASSUMPTIONS.md)

### Notes
- Dev Container verified working (dotnet 8.0.417)
- All development done inside container as per protocol

---

## Iteration 000
- Seeded repo docs and churn protocol.
