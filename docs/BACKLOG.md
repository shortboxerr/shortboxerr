# Backlog

## EPIC 0: Repo Skeleton (FOUNDATION)
- [ ] Create .NET solution structure:
  - src/Shortboxerr.Api
  - src/Shortboxerr.Core
  - src/Shortboxerr.Infrastructure
  - tests/Shortboxerr.Tests
- [ ] Health endpoint + Swagger
- [ ] SQLite migrations scaffold
- [ ] Dockerfile + docker-compose
- [ ] CI workflow (build + test)
- [ ] Dev Container config (verify dotnet build/test run inside container)

## EPIC 1: Domain + Persistence (MINIMUM DATA MODEL)
- [ ] Entities: Series, Issue, EditionTitle (Collections), FileAsset, HistoryEvent
- [ ] Repositories + EF Core mappings (SQLite)
- [ ] CRUD endpoints for Series + Collections
- [ ] Filtering/paging conventions aligned with Arr APIs

## EPIC 2: Import Pipeline (MYLAR3-LIKE)
- [ ] Staging folder model + endpoints
- [ ] Filename parser (singles + collections)
- [ ] Manual Import endpoints and basic UI contract
- [ ] Atomic move/rename preview
- [ ] History events for pipeline steps

## EPIC 3: DecisionEngine (MYLAR3-LIKE SELECTION)
- [ ] Candidate model + rejection reasons
- [ ] Ranking/scoring + deterministic tie-break
- [ ] Explanation report surfaced to API
- [ ] Golden test harness skeleton

## EPIC 4: Indexers + Download Clients (ARR-LIKE SHAPE)
- [ ] IndexerManager abstractions + health/test endpoints
- [ ] RSS/Atom indexer adapter
- [ ] First-party HTTP provider (feed poll + download-to-staging)
- [ ] DownloadClient abstraction (generic)

## EPIC 5: UI (ARR-LIKE UI)
- [ ] UI shell + nav map (Dashboard/Series/Collections/Wanted/Activity/History/Manual Import/Settings)
- [ ] Series list page (table + bulk actions)
- [ ] Collections list page
- [ ] Activity + Manual Import pages (thin but functional)

## EPIC 6: Mylar3 Migration (BEHAVIORAL PARITY SETUP)
- [ ] Read Mylar3 SQLite DB (read-only)
- [ ] Transform to intermediate JSON snapshot
- [ ] Import into Shortboxerr DB
- [ ] Post-migration scan job
- [ ] Migration report
