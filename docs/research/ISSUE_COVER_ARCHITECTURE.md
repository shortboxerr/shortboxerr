# Issue Data & Cover Acquisition Architecture

## EPIC 14.7.1 Code Architecture Review

This document describes the current architecture of issue metadata and cover image acquisition in Shortboxerr.

---

## 1. Overview

- **Issue data**: Series/issue metadata (titles, dates, ComicVine IDs) comes from ComicVine, Metron, Mylar3 import, or pull-list discovery.
- **Cover acquisition**: Covers are resolved in priority order (ComicVine → Metron → volume fallback), cached on disk by `CoverService`, and served via API.

---

## 2. Data Sources Hierarchy

| Priority | Source | Use |
|----------|--------|-----|
| 1 | **ComicVine** | Authoritative for series/issue metadata and cover URLs; finalizes pull-list enrichment. |
| 2 | **Metron** | Fallback when ComicVine issue ID is missing; backup covers for discovery; CV ID and volume+number lookups. |
| 3 | **WalkSoftly** | Release schedule for pull list; initial discovery data (no cover URLs). |
| 4 | **Volume cover** | Fallback when no issue-specific cover exists (series/volume image). |

---

## 3. Core Components

### 3.1 ICoverService / CoverService

- **Location**: `Core/Services/ICoverService.cs`, `Infrastructure/Services/CoverService.cs`
- **Role**: Single entry point for cover images. Handles disk cache, download, revalidation (ETag/Last-Modified), and fallback (issue → series cover).
- **Cache layout** (under `CoverSettings.CacheDirectory`, default `covers/`):
  - `series/{seriesId}/{thumb|small|medium|large}.jpg`
  - `issues/{issueId}/{size}.jpg`
  - `editions/{editionId}/{size}.jpg`
  - `discovery/{cvIssueId}/{size}.jpg` (pull-list items keyed by ComicVine issue ID or Metron ID)
- **Behavior**:
  - Series: load from DB → check cache → optional revalidation → download from `Series.CoverImageUrl` if miss.
  - Issue: load issue + series → check cache → download from `Issue.CoverImageUrl` if present; else fall back to series cover (cached or downloaded).
  - Discovery: `GetDiscoveryCoverAsync(coverId, size)` serves from `discovery/{coverId}/{size}.jpg` (no DB entity).
- **Sizes**: Thumb, Small, Medium, Large; ComicVine URL size segments (`scale_avatar`, `scale_small`, etc.) are rewritten via `GetSizedUrl`.

### 3.2 ICoverFallbackService / CoverFallbackService

- **Location**: `Core/Services/ICoverFallbackService.cs`, `Infrastructure/Services/CoverFallbackService.cs`
- **Role**: Resolve a cover URL when ComicVine does not have one. Used for enrichment and single-issue lookups.
- **Lookup order**:
  1. Metron by ComicVine issue ID (`cv_id`).
  2. Metron by ComicVine volume ID + issue number.
  3. Metron by series name + issue number search.
  4. ComicVine volume cover URL (last resort).
- **Caching**: In-memory (`IMemoryCache`), key `cover_fallback:{cvIssueId}`, TTL 24 hours.

### 3.3 Pull List & Discovery Covers

- **PullListService** (`Infrastructure/PullList/PullListService.cs`):
  - Builds discovery list; enriches with ComicVine (direct when CV ID present), then Metron, then volume covers.
  - `EnrichDiscoveryWithMetronCoversAsync`: for library issues with DB id, caches via `ICoverService.DownloadExternalCoverAsync` (Issue type); for discovery-only items, caches as Discovery type keyed by Metron issue ID or CV issue ID.
  - Sets `DiscoverableIssue.CoverImageUrl` to either local API path (e.g. `/api/v1/covers/issues/{id}` or `/api/v1/covers/discovery/{id}`) or external URL.
- **DiscoveryCoverEnrichmentService** (background): Downloads Metron covers for discovery items to disk.
- **DiscoveryUpgradeBackgroundService**: When ComicVine data becomes available, downloads CV covers and stores under discovery cache (by CV issue ID).

### 3.4 Variant Covers

- **IVariantCoverService / VariantCoverService** (`Infrastructure/ComicVine/VariantCoverService.cs`): Fetches and stores variant cover images from ComicVine for a given issue; supports preferred cover selection. Separate from the main series/issue/discovery cover flow.

---

## 4. API Surface

- **CoverEndpoints** (`Api/Endpoints/CoverEndpoints.cs`):
  - `GET /api/v1/covers/series/{seriesId}` → series cover (1-day response cache).
  - `GET /api/v1/covers/issues/{issueId}` → issue cover (fallback to series).
  - `GET /api/v1/covers/discovery/{coverId}` and `.../discovery/{coverId}/{size}` → discovery cover (cached by Metron/CV id).
  - `DELETE .../series/{id}`, `.../issues/{id}` → clear cache.
  - `GET .../cache/stats`, `.../cache/stats/detailed`, `POST .../cache/stats/reset` → stats and diagnostics.

---

## 5. Issue Metadata Flow (Where Cover URLs Come From)

- **Library series/issues**: `Series.CoverImageUrl`, `Issue.CoverImageUrl` populated by ComicVine refresh, Mylar3 import, or manual metadata refresh.
- **Pull list discovery**: WalkSoftly provides schedule; ComicVine enriches with CV IDs and cover URLs when available; Metron fills in covers for issues without CV cover; volume cover used when no issue cover found. Final `DiscoverableIssue` has `CoverImageUrl` (API path or URL) and enrichment status (`EnrichmentStatus`, `CoverSource`, `IsVolumeFallbackCover`).

---

## 6. Cache Lifecycle & Limits

- **CoverSettings** (Core): `MaxCacheSizeBytes` (default 500MB), `CleanupTargetPercent`, `CleanupIntervalHours`, `AutoCleanupEnabled`, `RetentionDays`, `EnableRevalidation`, `RevalidationIntervalHours`.
- **CoverCacheCleanupBackgroundService**: Runs periodically; enforces size limit via LRU eviction; can remove by retention.
- **Source priority for overwrite**: `CoverCacheSource` (ComicVine > Metron > Placeholder). Higher-priority source can overwrite lower when storing via `DownloadExternalCoverAsync`.

---

## 7. Refactoring Candidates (for 14.7.2+)

- **Cover source integration testing**: Explicit tests that each source (ComicVine, Metron, volume fallback) is invoked in the correct order and that discovery cache keys align with API.
- **Unit test coverage**: CoverService path logic, revalidation, and discovery key mapping; CoverFallbackService lookup order and cache key format.
- **Edge cases**: Missing CV ID, rate limiting (Metron), and behavior when both CV and Metron fail for an issue.
