# Completed EPICs Archive

This document contains the full details of completed EPICs, archived from `BACKLOG.md` to keep the active backlog focused.

**Last Updated:** 2026-02-25 (Iteration 163)

---

## Table of Contents

| EPIC | Description | Completed |
|------|-------------|-----------|
| [EPIC 0](#epic-0-repo-skeleton-foundation--completed) | Repo Skeleton (Foundation) | Iteration 001 |
| [EPIC 1](#epic-1-domain--persistence-minimum-data-model--completed) | Domain + Persistence | Iteration 002 |
| [EPIC 2](#epic-2-import-pipeline-mylar3-like--completed) | Import Pipeline | Iteration 005 |
| [EPIC 3](#epic-3-decisionengine-mylar3-like-selection--completed) | DecisionEngine | Iteration 006 |
| [EPIC 4](#epic-4-indexers--download-clients-arr-like-shape--completed) | Indexers + Download Clients | Iteration 040 |
| [EPIC 5](#epic-5-ui-arr-like-ui--completed) | UI (Arr-like) | Iteration 015 |
| [EPIC 6](#epic-6-settings-persistence--ui-enhancements--completed) | Settings & UI Enhancements | Iteration 025 |
| [EPIC 7](#epic-7-mylar3-migration-behavioral-parity-setup--completed) | Mylar3 Migration | Iteration 030 |
| [EPIC 8](#epic-8-ddl-site-adapters--download-hosts-mylar3-parity--completed) | DDL Site Adapters | Iteration 080 |
| [EPIC 10](#epic-10-nzbusenet-support-mylar3sonarradarr-parity--completed) | NZB/Usenet Support | Iteration 100 |
| [EPIC 15](#epic-15-ui-bug-fixes--improvements--completed) | UI Bug Fixes | Iteration 163 |
| [EPIC 16](#epic-16-end-to-end-testing-infrastructure--completed) | E2E Testing Infrastructure | Iteration 120 |
| [EPIC 17](#epic-17-ddl-download-link-robustness--completed) | DDL Download Robustness | Iteration 160 |

---

## EPIC 0: Repo Skeleton (FOUNDATION) ✅ COMPLETED
- [x] Create .NET solution structure:
  - src/Shortboxerr.Api
  - src/Shortboxerr.Core
  - src/Shortboxerr.Infrastructure
  - tests/Shortboxerr.Tests
- [x] Health endpoint + Swagger
- [x] SQLite migrations scaffold
- [x] Dockerfile + docker-compose
- [x] CI workflow (build + test)
- [x] Dev Container config (verify dotnet build/test run inside container)
- [x] **Developer tooling** ✅
  - AC: Makefile with common targets (restore, build, test, run, install-hooks)
  - AC: Git commit-msg hook enforcing conventional commits (feat/fix/chore/test prefix)
  - AC: .gitignore for build artifacts and IDE files
- [x] **Port standardization** ✅
  - AC: Default application port: 8585 (changed from 7878)
  - AC: Updated in docker-compose, Dockerfile, CI workflow, launchSettings

## EPIC 1: Domain + Persistence (MINIMUM DATA MODEL) ✅ COMPLETED
- [x] Entities: Series, Issue, EditionTitle (Collections), FileAsset, HistoryEvent
- [x] Repositories + EF Core mappings (SQLite)
- [x] CRUD endpoints for Series + Collections
- [x] Filtering/paging conventions aligned with Arr APIs

## EPIC 2: Import Pipeline (MYLAR3-LIKE) ✅ COMPLETED
- [x] Staging folder model + endpoints
- [x] Filename parser (singles + collections)
- [x] Manual Import endpoints and basic UI contract
- [x] Atomic move/rename preview
- [x] History events for pipeline steps

## EPIC 3: DecisionEngine (MYLAR3-LIKE SELECTION) ✅ COMPLETED
- [x] Candidate model + rejection reasons
- [x] Ranking/scoring + deterministic tie-break
- [x] Explanation report surfaced to API
- [x] Golden test harness skeleton

## EPIC 4: Indexers + Download Clients (ARR-LIKE SHAPE) ✅ COMPLETED

### 4.1 Provider Abstractions ✅ COMPLETED
- [x] **IProvider interface**: Base abstraction for all provider types
- [x] **IIndexerProvider**: Extends IProvider for search/discovery
- [x] **IDownloadProvider**: Extends IProvider for acquisition
- [x] **ProviderManager**: Registry for all configured providers

### 4.2 DDL Provider (Mylar3-Compatible) - BUILT-IN SERVICE
The DDL (Direct Download) provider is a **built-in internal service** with Mylar3 parity.
**225+ unit tests** cover DDL functionality.

#### 4.2.1 DDL Discovery & Search ✅ COMPLETED
- [x] **DDL site adapter interface (IDdlSiteAdapter)**
- [x] **DDL search endpoint polling**
- [x] **DDL link discovery**

#### 4.2.2 DDL Candidate Normalization ✅ COMPLETED
- [x] **DDL release parser**
- [x] **DDL candidate model**
- [x] **DDL filtering rules**

#### 4.2.3 DDL Download Execution ✅ COMPLETED
- [x] **DDL downloader service**
- [x] **DDL retry semantics**
- [x] **DDL failure handling**

#### 4.2.4 DDL → Import Handoff ✅ COMPLETED
- [x] **DDL post-download processing**
- [x] **DDL import integration**

### 4.3 DDL Configuration & Mylar3 Import ✅ COMPLETED
- [x] **DDL provider entity + settings**
- [x] **Mylar3 DDL settings import**
- [x] **DDL provider defaults**

### 4.4 DDL Conformance Tests (Mylar3 Parity) ✅ COMPLETED
- [x] **DDL parsing fixture tests**
- [x] **DDL filtering fixture tests**
- [x] **DDL retry/failure fixture tests**
- [x] **DDL integration tests**

### 4.5 DDL UI (Arr-Style) ✅ COMPLETED
- [x] **DDL provider list page**
- [x] **DDL provider add/edit modal**
- [x] **DDL provider test endpoint**
- [x] **DDL activity feed**

### 4.6 Generic Indexer/Download Client Support ✅ COMPLETED
- [x] **RSS/Atom indexer adapter**
- [x] **Built-in HTTP download client**
- [x] **Torrent client abstraction** (placeholder)

### 4.7 DDL Parser Enhancements (Mylar3 Parity) ✅ COMPLETED
- [x] **Publisher extraction improvement**
- [x] **Quality tag extraction**
- [x] **Separator normalization**
- [x] **Hyphen-separated subtitles**
- [x] **Aspirational tests promoted to main tests**

---

## EPIC 5: UI (ARR-LIKE UI) ✅ COMPLETED
- [x] **UI technology stack** ✅ (React 18, TypeScript, Vite, TanStack Query)
- [x] **UI shell + navigation** ✅
- [x] **Core pages** ✅ (Series, Collections, Activity, Manual Import)
- [x] **Build integration** ✅
- [x] **API response mapping** ✅

## EPIC 6: Settings Persistence & UI Enhancements ✅ COMPLETED
- [x] **Theme persistence** ✅
- [x] **General settings persistence** ✅
- [x] **API key management** ✅
- [x] **Naming format token helper** ✅
- [x] **Separate Download and Staging folders** ✅
- [x] **UI/API development infrastructure** ✅
- [x] **Settings page structure** ✅

## EPIC 7: Mylar3 Migration (BEHAVIORAL PARITY SETUP) ✅ COMPLETED
- [x] Read Mylar3 SQLite DB (read-only) ✅
- [x] Transform to intermediate JSON snapshot ✅
- [x] Import into Shortboxerr DB ✅
- [x] Post-migration scan job ✅
- [x] Migration report ✅

### API Endpoints
- `POST /api/v1/mylar3/migration/analyze`
- `POST /api/v1/mylar3/migration/export`
- `POST /api/v1/mylar3/migration/import`
- `POST /api/v1/mylar3/migration/migrate`

---

## EPIC 8: DDL Site Adapters & Download Hosts (Mylar3 Parity) ✅ COMPLETED

### 8.1 DDL Site Indexers (Comic Discovery)

#### 8.1.1 GetComics.org Adapter (Primary) ✅ COMPLETED
- [x] **HTML scraping for GetComics** ✅
- [x] **GetComics search integration** ✅
- [x] **GetComics link resolution** ✅

#### 8.1.2 ReadComicOnline Adapter (Secondary) ✅ COMPLETED
- [x] **Determine homepage address** ✅
- [x] **HTML scraping for ReadComicOnline** ✅
- [x] **ReadComicOnline search integration** ✅
- [x] **ReadComicOnline link resolution** ✅

### 8.2 Download Host Resolvers (File Acquisition)

#### Completed Resolvers:
- [x] **Direct/Main Server Downloads** ✅
- [x] **MediaFire Resolver** ✅
- [x] **Mega.nz Resolver** ✅
- [x] **Pixeldrain Resolver** ✅
- [x] **Dropbox Resolver** ✅
- [x] **Google Drive Resolver** ✅
- [x] **Zippyshare resolver** ✅ (defunct, graceful handling)
- [x] **Rapidgator/Uploaded resolver** ✅
- [x] **1fichier resolver** ✅

### 8.3 Download Host Priority & Fallback ✅ COMPLETED
- [x] **Host priority configuration** ✅
- [x] **Automatic fallback** ✅

### 8.4 DDL Site Health Monitoring ✅ COMPLETED
- [x] **Site availability checks** ✅
- [x] **Rate limiting per site** ✅

### 8.5 DDL Adapter Tests ✅ COMPLETED
- [x] **GetComics fixture tests** ✅
- [x] **Download host resolver tests** ✅ (35 unit tests)
- [x] **Integration tests** ✅ (27 tests)

### 8.6 GetComics Mylar3 Full Parity ✅ COMPLETED
- [x] **Session & Cookie Persistence** ✅
- [x] **GetComicsAdapter (Complete Rewrite)** ✅
- [x] **GetComicsSettings Model** ✅
- [x] **Post-Download Processing** ✅
- [x] **Enhanced Pack Detection** ✅

---

## EPIC 10: NZB/Usenet Support (Mylar3/Sonarr/Radarr Parity) ✅ COMPLETED

### 10.1 NZB Indexer Integration ✅ COMPLETED
- [x] **Newznab API client** ✅
- [x] **NZBHydra2 support** ✅
- [x] **Built-in indexer presets** ✅
- [x] **Indexer health monitoring** ✅

### 10.2 NZB Download Client Integration ✅ COMPLETED
- [x] **SABnzbd integration** ✅
- [x] **NZBGet integration** ✅
- [x] **Download client health checks** ✅
- [x] **Download client failover** ✅

### 10.3 NZB Candidate Processing ✅ COMPLETED
- [x] **NZB release parsing** ✅
- [x] **NZB candidate model** ✅
- [x] **NZB filtering rules** ✅

### 10.4 NZB → Import Handoff ✅ COMPLETED
- [x] **Post-download detection** ✅
- [x] **Import integration** ✅

### 10.5 NZB Configuration & Settings ✅ COMPLETED
- [x] **Indexer configuration** ✅
- [x] **Download client configuration** ✅
- [x] **Mylar3 NZB settings import** ✅

### 10.6 NZB UI ✅ COMPLETED
- [x] **Indexers settings page** ✅
- [x] **Download clients settings page** ✅
- [x] **Unified download client modal** ✅
- [x] **Activity integration** ✅

### 10.7 NZB Conformance Tests ✅ COMPLETED
**Total NZB tests: 63**

---

## EPIC 15: UI Bug Fixes & Improvements ✅ COMPLETED

### Completed Items:
- [x] **15.1 Dashboard Statistics Accuracy** ✅ (Iteration 096)
- [x] **15.2 "This Week" Section Accuracy** ✅ (Iteration 096)
- [x] **15.3 Forthcoming Releases View** ✅
- [x] **15.4 Issue Overlay Button Visibility** ✅ (Iteration 097)
- [x] **15.5 Click Issue to Open ComicVine** ✅ (Iteration 097)
- [x] **15.6 Wanted View Empty State** ✅ (Iteration 096)
- [x] **15.7 Issue Status Toggle from Series View** ✅ (Iteration 097)
- [x] **15.8 Annual Handling Settings** ✅
- [x] **15.9 Pull List Data Accuracy Investigation** ✅ (Iteration 137)
- [x] **15.10 Series-Annual Integration** ✅
- [x] **15.11 Default User-Agent Header** ✅ (Iteration 128)
- [x] **15.12 SabnzbdClient Constructor Ambiguity** ✅ (Iteration 129)
- [x] **15.13 NewznabClient User-Agent Rejection** ✅ (Iteration 129)
- [x] **15.14 EF Core Query Splitting** ✅ (Iteration 130)
- [x] **15.15 Download Client Error Log Noise** ✅ (Iteration 132)
- [x] **15.16 Background Service Graceful Degradation** ✅ (Iteration 132)
- [x] **15.17 Compiler Warning Cleanup** ✅ (Iteration 135)
- [x] **15.19 Manual Import & Parser Improvements** ✅ (Iteration 163)

---

## EPIC 16: End-to-End Testing Infrastructure ✅ COMPLETED

### 16.1 Test Framework Setup ✅ COMPLETED
- Playwright E2E tests in `tests/e2e`
- 10 smoke tests covering core pages

### 16.2 User Workflow Tests ✅ COMPLETED
- 13 series management tests
- 12 issue management tests
- 13 pull list tests

### 16.3 Background Automation Tests ✅ COMPLETED
- 19 background service tests

### 16.4 API Integration Tests ✅ COMPLETED
- 26 API integration tests

### 16.5 UI Smoke Tests ✅ COMPLETED
- 9 settings tests
- 13 error state tests

---

## EPIC 17: DDL Download Link Robustness ✅ COMPLETED

### 17.1 Activity Tracking ✅ COMPLETED
- [x] Centralized download history
- [x] Fixed DI lifetime mismatch

### 17.2 GetComics Adapter Consolidation ✅ COMPLETED
- [x] V2 adapter is sole implementation
- [x] Legacy adapter removed

---

*For active backlog items, see [BACKLOG.md](./BACKLOG.md)*
