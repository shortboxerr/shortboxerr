# Worklog

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
