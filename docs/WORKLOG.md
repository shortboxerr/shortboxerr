# Worklog

## Iteration 016 (2026-02-02)
**EPIC 6: Settings Persistence & UI Enhancements - PARTIAL**

### Commits
1. `feat: add settings persistence with theme support (EPIC 6)`
2. `test: add settings endpoint tests (14 new tests)`

### Deliverables
- ✅ Settings Service:
  - ISettingsService interface with key-value storage
  - SettingsService implementation using SystemSetting entity
  - Generic Get/Set/Delete operations for any key
  - Typed helpers for UiSettings and GeneralSettings
- ✅ Theme Persistence:
  - Theme stored in database: "dark", "light", "system"
  - Loaded on app startup via React Query
  - ThemeContext provider with useTheme hook
  - CSS variables dynamically applied for light/dark modes
  - System theme detection via matchMedia
- ✅ UI Settings API:
  - GET /api/v1/settings/ui (returns theme, pageSize, showFileSizes, relativeTimestamps)
  - PUT /api/v1/settings/ui (validates theme and pageSize)
- ✅ General Settings API:
  - GET /api/v1/settings/general (naming formats, folder paths)
  - PUT /api/v1/settings/general
- ✅ Folder Settings API (convenience endpoints):
  - GET /api/v1/settings/folders
  - PUT /api/v1/settings/folders (partial updates supported)
  - Separate downloadFolder and stagingFolder
  - autoMoveToStaging flag
- ✅ Naming Format Tokens API:
  - GET /api/v1/settings/naming/tokens
  - Returns available tokens for Series, Issue, and Collection formats
  - Includes description and example for each token
- ✅ Generic Settings API:
  - GET /api/v1/settings/{key}
  - PUT /api/v1/settings/{key}
  - DELETE /api/v1/settings/{key}
- ✅ 373 tests passing (14 new settings tests)

### New/Modified Files
| File | Purpose |
|------|---------|
| `src/Shortboxerr.Core/Services/ISettingsService.cs` | Settings service interface |
| `src/Shortboxerr.Infrastructure/Services/SettingsService.cs` | Settings service implementation |
| `src/Shortboxerr.Api/Endpoints/SettingsEndpoints.cs` | Settings API endpoints |
| `ui/src/App.tsx` | ThemeContext and theme provider |
| `ui/src/api/client.ts` | Settings API client functions |
| `ui/src/pages/SettingsPage.tsx` | Theme dropdown with persistence |
| `tests/Shortboxerr.Tests/SettingsEndpointTests.cs` | Settings endpoint tests |
| `docs/API.md` | Settings endpoint documentation |

### Remaining in EPIC 6
- API key management (display, copy, regenerate)

### Notes
- Theme changes are saved to database and apply immediately
- Light theme uses CSS variables for colors (invertable)
- Folder settings support partial updates for flexibility
- Naming tokens API ready for UI token picker implementation

---

## Iteration 015 (2026-02-02)
**EPIC 4.5: DDL UI (Arr-Style) - COMPLETED**

### Commits
1. `feat: add DDL provider UI with list, add/edit modal, and test`
2. `feat: add DDL activity feed to Activity page`

### Deliverables
- ✅ DDL Provider List Page:
  - Table with name, type, status badge, priority
  - Enable/disable toggle with instant feedback
  - Drag handle for reordering (visual indicator)
  - Test button with live result display
  - Edit and delete actions
- ✅ DDL Provider Add/Edit Modal:
  - Dynamic form based on implementation requirements
  - Fields: Name, Implementation type, Base URL, API Key, Credentials
  - Enable/disable checkbox
  - Test button before saving
  - Success/error result display with latency
- ✅ DDL Provider Test Endpoint:
  - Existing POST /api/v1/providers/{id}/test integrated
  - POST /api/v1/providers/test for new providers
  - Returns success, message, errors, latencyMs
- ✅ DDL Activity Feed:
  - Tab navigation: Queue / DDL Activity
  - Filter dropdown: All, Searches, Downloads, Failed
  - Event cards with type icons and status badges
  - Event types: search, download_started, download_complete, download_failed, candidate_found
  - Auto-refresh every 10 seconds
- ✅ Settings Enhancements:
  - Renamed "Staging Folder" to "Download Folder"
  - API key show/hide toggle with copy button
  - Download Clients tab with same provider management
- ✅ API Client Extensions:
  - Provider CRUD functions
  - Test functions
  - Enable/disable and reorder functions

### New/Modified Files
| File | Purpose |
|------|---------|
| `ui/src/pages/SettingsPage.tsx` | DDL provider management UI |
| `ui/src/pages/ActivityPage.tsx` | DDL activity feed |
| `ui/src/api/client.ts` | Provider API functions |

### Tests
- 359 backend tests passing
- UI TypeScript compilation passes
- Production build successful

### Notes
- EPIC 4 is now 100% complete
- DDL activity feed ready for real data when DDL endpoints are implemented
- Provider test endpoint already existed in backend

---

## Iteration 014 (2026-02-02)
**EPIC 5: UI (ARR-LIKE UI) - COMPLETED**

### Commits
1. `feat: add React UI shell with Arr-style navigation`

### Deliverables
- ✅ React SPA Setup:
  - Vite + React 18 + TypeScript
  - React Query for data fetching
  - React Router v6 for navigation
  - Lucide React for icons
  - Custom CSS with CSS variables (no framework)
- ✅ UI Shell + Navigation:
  - Sidebar with collapsible sections
  - Dark theme inspired by Sonarr/Radarr
  - Inter font family (Google Fonts)
  - Responsive layout
- ✅ Dashboard Page:
  - Stats cards (Series, Collections, Issues, Files)
  - System status cards (Database, Indexers, Queue)
  - Recent activity feed
- ✅ Series Page:
  - Table with search and pagination
  - Bulk selection and delete
  - Status badges (Continuing, Ended, Hiatus)
  - Row actions (Edit, More)
- ✅ Collections Page:
  - Table with search and filters
  - Type badges (TPB, HC, Omnibus, Deluxe)
  - Status badges (Have, Missing)
- ✅ Wanted Page:
  - Tab toggle: Issues / Collections
  - Search for download actions
- ✅ Activity Page:
  - Download queue with progress bars
  - Pause/Resume/Remove controls
- ✅ History Page:
  - Event type filter dropdown
  - Color-coded event icons
- ✅ Manual Import Page:
  - Stats summary (total, matched, needs review)
  - Parsed info ↔ Match preview
  - Bulk import selected files
- ✅ Settings Page:
  - Tabbed navigation (General, Indexers, Download, Import, UI, Security)
  - Form fields with labels and descriptions
- ✅ API Client:
  - Typed fetch wrapper
  - Graceful error handling
  - Relative time formatting
- ✅ Build Configuration:
  - Dev server on :3000 with API proxy to :8585
  - Production build outputs to wwwroot
  - SPA fallback routing in ASP.NET

### New Files
| File | Purpose |
|------|---------|
| `ui/` | React frontend project |
| `ui/src/App.tsx` | Main app with routing |
| `ui/src/App.css` | Global styles with CSS variables |
| `ui/src/components/Layout.tsx` | Sidebar navigation layout |
| `ui/src/pages/Dashboard.tsx` | Dashboard with stats |
| `ui/src/pages/SeriesPage.tsx` | Series table with bulk actions |
| `ui/src/pages/CollectionsPage.tsx` | Collections table |
| `ui/src/pages/WantedPage.tsx` | Wanted issues/collections |
| `ui/src/pages/ActivityPage.tsx` | Download queue |
| `ui/src/pages/HistoryPage.tsx` | Event history |
| `ui/src/pages/ManualImportPage.tsx` | Staged file review |
| `ui/src/pages/SettingsPage.tsx` | Tabbed settings |
| `ui/src/api/client.ts` | API client with React Query |
| `src/Shortboxerr.Api/wwwroot/` | Built UI assets |

### Dependencies Added
- `react-router-dom` - Client-side routing
- `@tanstack/react-query` - Data fetching and caching
- `lucide-react` - Icon library
- `clsx` - Class name utility

### Assumptions Made
- None new (used existing assumptions from docs/ASSUMPTIONS.md)

### Notes
- UI connects to backend API at :8585 (configurable via VITE_API_URL)
- Dark theme only for now (light theme can be added later)
- Settings page is UI-only (no backend persistence yet)
- EPIC 4.5 DDL UI can now be implemented (dependency on EPIC 5 resolved)

---

## Iteration 013 (2026-02-02)
**EPIC 4.6: Generic Indexer/Download Client Support - COMPLETED**

### Commits
1. `feat: add RSS indexer and HTTP download client`

### Deliverables
- ✅ RSS/Atom Indexer Adapter:
  - `IRssIndexer` interface extending `IIndexerProvider`
  - `RssIndexer` implementation using `System.ServiceModel.Syndication`
  - Support for RSS 2.0 and Atom 1.0 feeds
  - Feed polling with configurable intervals
  - Category filtering support
  - Basic authentication support
  - Enclosure link extraction for direct downloads
  - 10 unit tests for feed parsing and candidate conversion
- ✅ Generic HTTP Download Client:
  - `IHttpDownloadClient` interface extending `IDownloadProvider`
  - `HttpDownloadClient` implementation with full feature set
  - Retry logic with exponential backoff
  - Concurrent download support via semaphore
  - Resume support for partial downloads
  - Progress reporting capability
  - Custom headers, cookies, and auth support
  - File size checking via HEAD requests
  - Reachability checking
  - 12 unit tests covering download operations
- ✅ Torrent Client Abstraction (Interface Only):
  - `ITorrentClient` interface for future torrent support
  - Complete type definitions: `TorrentAddResult`, `TorrentInfo`, `TorrentState`
  - Configuration types: `TorrentClientSettings`, `TorrentAddOptions`
  - No implementation per EPIC 4 spec
- ✅ 359 tests passing (22 new tests)

### New Files
| File | Purpose |
|------|---------|
| `src/Shortboxerr.Core/Indexers/IRssIndexer.cs` | RSS indexer interface and models |
| `src/Shortboxerr.Core/DownloadClients/IHttpDownloadClient.cs` | HTTP client interface and models |
| `src/Shortboxerr.Core/DownloadClients/ITorrentClient.cs` | Torrent client interface (placeholder) |
| `src/Shortboxerr.Infrastructure/Indexers/RssIndexer.cs` | RSS indexer implementation |
| `src/Shortboxerr.Infrastructure/DownloadClients/HttpDownloadClient.cs` | HTTP client implementation |
| `tests/Shortboxerr.Tests/RssIndexerTests.cs` | RSS indexer tests |
| `tests/Shortboxerr.Tests/HttpDownloadClientTests.cs` | HTTP client tests |

### Dependencies Added
- `System.ServiceModel.Syndication` - RSS/Atom feed parsing
- `Moq` - Mocking framework for tests

### Assumptions Made
- None new (used existing assumptions from docs/ASSUMPTIONS.md)

### Notes
- RSS indexer converts feed items to Candidates using existing FilenameParser
- HTTP client supports all common download scenarios
- Torrent interface is ready for future implementation (qBittorrent, Transmission, etc.)

---

## Iteration 012 (2026-02-02)
**EPIC 4.7: DDL Parser Enhancements (Mylar3 Parity) - COMPLETED**

### Commits
1. `feat: enhance DDL release parser for Mylar3 parity`

### Deliverables
- ✅ Publisher extraction improvement:
  - Extract publisher from parentheses when followed by year
  - Handle multiple parenthetical metadata groups in any order
  - `Wolverine 0001 (Marvel) (2024).cbz` → Publisher: Marvel
- ✅ Quality tag extraction:
  - Extract quality tags reliably: Webrip, Digital, Scan, c2c, HD
  - Handle quality tags in parentheses and as standalone tokens
  - `Action Comics 1050 (2023) (Webrip).cbz` → Quality: Webrip
- ✅ Separator normalization:
  - Normalize underscores to spaces: `Wonder_Woman_001_(DC)_(2023).cbz`
  - Normalize periods to spaces (preserving file extension and decimals)
  - `Aquaman.001.2023.Digital.cbz` → Aquaman 001 2023 Digital
- ✅ Hyphen-separated subtitles:
  - Handle `Series - Subtitle` patterns properly
  - `Star Wars - Darth Vader 001 (Marvel) (2020).cbz` → works correctly
- ✅ Aspirational tests promoted to main tests:
  - 5 new test cases added to ddl_parsing_golden.json
  - Total: 29 parsing golden tests, all passing
  - Removed aspirationalTests section (no longer needed)
- ✅ 337 tests passing (5 new parser tests)

### Technical Details
- Added `NormalizeSeparators()` preprocessing step
- Protected decimal issue numbers (1.5) during normalization
- Added `YearAnywhereRegex` for scene-style year positions
- Added `ExtractAllParenGroups()` for multiple parenthetical metadata
- Added `ExtractPublisherFromParenGroups()` for publisher in parens
- Added `ExtractQualityFromParenGroups()` for quality in parens
- Reordered extraction pipeline: quality extraction now before issue extraction

### Mylar3 Parity Status
| Feature | Status | Notes |
|---------|--------|-------|
| Publisher in parens | ✅ PASS | `(Marvel)` extracted correctly |
| Quality tags | ✅ PASS | Digital, Webrip, Scan, c2c |
| Underscore separator | ✅ PASS | Normalized before parsing |
| Period separator | ✅ PASS | Preserves decimals |
| Hyphen subtitles | ✅ PASS | Preserved in series title |

### Assumptions Made
- None new (used existing assumptions from docs/ASSUMPTIONS.md)

### Notes
- All parser enhancements backward-compatible with existing tests
- Scene-style naming (Aquaman.001.2023.Digital.cbz) now fully supported
- DDL parser now achieves 100% Mylar3 parity for documented cases

---

## Iteration 011 (2026-02-02)
**EPIC 4.4: DDL Conformance Tests (Mylar3 Parity) - COMPLETED**

### Commits
1. `feat: expand DDL parsing and filtering golden test fixtures`
2. `feat: add required words filtering test cases`
3. `chore: update docs for iteration 011 completion`

### Deliverables
- ✅ DDL Parsing Fixture Tests:
  - 24 comprehensive golden test cases for release title parsing
  - Covers: singles, collections, TPB, HC, Omnibus, Deluxe, Compendium
  - Covers: issue numbers, volumes, years, publishers, release groups
  - Aspirational tests documented for future parser enhancements
  - Must pass 100% to claim Mylar3 parity
- ✅ DDL Filtering Fixture Tests:
  - 21 test cases in main fixture
  - 4 required words test cases
  - Banned words (sample, preview, promo, demo, watermark)
  - Size limits for singles and collections
  - Format blocking (PDF)
  - Edge cases (exact min/max boundaries)
- ✅ DDL Retry/Failure Fixture Tests:
  - 11 retry behavior scenarios
  - 3 exponential backoff tests
  - 3 failure state transition tests
  - 5 file verification tests
  - Covers: timeout, connection failures, 404, 401/403, rate limiting
  - Distinguishes transient vs non-transient failures
- ✅ DDL Integration Tests:
  - 12 end-to-end scenarios
  - 3 multi-site aggregation tests
  - Happy path: search → candidate → filter → download → import
  - Rejection paths: banned words, size limits
  - Retry paths: succeed after retries, max retries exceeded
  - Verification: HTML error page detection
  - Auto-match: high confidence auto-import, low confidence manual review
- ✅ 332 tests passing (65 new conformance tests)

### Test Coverage Summary
| Category | Test Count | Description |
|----------|------------|-------------|
| Parsing | 24 | Release title parsing |
| Filtering | 25 | Filter rule application |
| Retry/Failure | 24 | Retry semantics and failure handling |
| Integration | 17 | End-to-end scenarios |

### Mylar3 Parity Status
| Feature | Status | Notes |
|---------|--------|-------|
| Basic release parsing | ✅ | Series, issue, year, format |
| Collection detection | ✅ | TPB, HC, Omnibus, etc. |
| Banned words filter | ✅ | Matches Mylar3 defaults |
| Size limits | ✅ | Singles and collections |
| Retry semantics | ✅ | 3 retries, exponential backoff |
| File verification | ✅ | Magic bytes, HTML detection |
| Underscore/period separators | ⚠️ | Documented as aspirational |
| Quality tag extraction | ⚠️ | Documented as aspirational |

---

## Iteration 010 (2026-02-02)
**EPIC 4.3: DDL Configuration & Mylar3 Import - COMPLETED**

### Commits
1. `feat: add Mylar3 config import and DDL provider settings (EPIC 4.3)`
2. `chore: update docs for iteration 010 completion`

### Deliverables
- ✅ DDL Provider Settings:
  - DdlProviderSettings: Comprehensive DDL-specific configuration
  - Site type, rate limits, timeouts, retries
  - Authentication methods (None, Basic, Cookie, ApiKey, OAuth2)
  - Auto-grab settings, format preferences, banned words
  - Size limits for singles and collections
  - JSON serialization for ProviderDefinition.Settings storage
- ✅ Mylar3 Config Importer:
  - IMylar3ConfigImporter: Interface for config parsing
  - Mylar3ConfigImporter: Full INI parser implementation
  - Section detection (DDL-1, GettyComics, etc.)
  - Site type inference from section names and URLs
  - Credential extraction (username, password, API key)
  - Unmapped section/setting tracking
  - Validation workflow with warnings
  - Import execution with options (overwrite, prefix, etc.)
- ✅ API Endpoints:
  - POST /api/v1/mylar3/parse (parse config content)
  - POST /api/v1/mylar3/parse/file (parse from file path)
  - POST /api/v1/mylar3/validate (validate before import)
  - POST /api/v1/mylar3/import (execute import)
  - GET /api/v1/mylar3/defaults (get all site defaults)
  - GET /api/v1/mylar3/defaults/{siteType} (get specific site defaults)
- ✅ Updated defaults.mylar3.json with DDL provider defaults
- ✅ 266 tests passing (19 new)

### Mylar3 Import Features
| Feature | Description |
|---------|-------------|
| INI Parsing | Standard config.ini format |
| Section Detection | Auto-detect DDL sections |
| Site Type Inference | From section name or URL |
| Credential Handling | Optional import with validation |
| Validation | Pre-import system state check |
| Import Options | Overwrite, prefix, skip disabled |
| Unmapped Tracking | Report unsupported settings |

### Site Type Defaults
| Site | Rate Limit | Timeout | Retries |
|------|------------|---------|---------|
| GettyComics | 10/min | 30s | 3 |
| ReadComicOnline | 5/min | 45s | 3 |
| GetComics | 10/min | 30s | 3 |
| Generic | 10/min | 30s | 3 |

---

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
