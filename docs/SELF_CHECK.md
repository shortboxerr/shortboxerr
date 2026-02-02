# Self-Check Rubric

## Iteration 006: EPIC 4.2.2 - DDL Candidate Normalization ✅ COMPLETED

### Core Requirements
| Check | Status |
|-------|--------|
| DDL release parser implemented | ✅ |
| DDL candidate model defined | ✅ |
| DDL filtering rules implemented | ✅ |
| Golden parsing fixtures created | ✅ |
| Golden filtering fixtures created | ✅ |
| Services registered in DI | ✅ |
| All tests passing | ✅ (183 tests) |
| Build succeeds | ✅ |
| Documentation updated | ✅ |

### DDL Parsing Features
| Feature | Status |
|---------|--------|
| Series title extraction | ✅ |
| Issue number (#001, 001, Issue 1) | ✅ |
| Decimal issues (#1.5) | ✅ |
| Volume number (Vol. 1, v1) | ✅ |
| Year (parentheses, trailing) | ✅ |
| Collection detection (TPB, HC, Omnibus, Deluxe) | ✅ |
| Publisher detection (Marvel, DC, Image, etc.) | ✅ |
| Quality tags (Digital, Webrip, Scan) | ✅ |
| Release group extraction | ✅ |
| Confidence scoring | ✅ |
| Title normalization | ✅ |

### DDL Filtering Features (Mylar3 Defaults)
| Feature | Default Value | Status |
|---------|---------------|--------|
| Banned words | sample, preview | ✅ |
| Required words | (configurable) | ✅ |
| Min size singles | 1MB | ✅ |
| Max size singles | 200MB | ✅ |
| Min size collections | 5MB | ✅ |
| Max size collections | 2GB | ✅ |
| Blocked formats | pdf | ✅ |
| Preferred formats | cbz, cbr | ✅ |
| Parse confidence threshold | 20 | ✅ |
| Blocked release groups | (configurable) | ✅ |

### Golden Test Coverage
| Fixture | Test Cases | Status |
|---------|------------|--------|
| ddl_parsing_golden.json | 14 | ✅ |
| ddl_filtering_golden.json | 10 | ✅ |

### Test Counts
| Category | Count |
|----------|-------|
| DDL Parser tests | 48 |
| DDL Filter tests | 38 |
| Golden parsing tests | 14 |
| Golden filtering tests | 10 |
| **Total new tests** | 86 (approx) |
| **Total tests** | 183 |

### Files Created/Modified
- `src/Shortboxerr.Core/Ddl/DdlCandidate.cs` ✅
- `src/Shortboxerr.Core/Ddl/DdlReleaseParser.cs` ✅
- `src/Shortboxerr.Core/Ddl/IDdlReleaseParser.cs` ✅
- `src/Shortboxerr.Core/Ddl/DdlFilter.cs` ✅
- `src/Shortboxerr.Core/Ddl/IDdlFilter.cs` ✅
- `src/Shortboxerr.Infrastructure/DependencyInjection.cs` ✅
- `tests/Shortboxerr.Tests/DdlReleaseParserTests.cs` ✅
- `tests/Shortboxerr.Tests/DdlFilterTests.cs` ✅
- `tests/Shortboxerr.Tests/DdlParsingGoldenTests.cs` ✅
- `tests/Shortboxerr.Tests/DdlFilteringGoldenTests.cs` ✅
- `tests/Shortboxerr.Tests/Fixtures/ddl_parsing_golden.json` ✅
- `tests/Shortboxerr.Tests/Fixtures/ddl_filtering_golden.json` ✅
- `docs/API.md` ✅
- `docs/WORKLOG.md` ✅
- `docs/BACKLOG.md` ✅

### Commits
1. `feat: add DDL candidate normalization (EPIC 4.2.2)` ✅
2. `test: add golden test fixtures for DDL parsing (EPIC 4.2.2)` ✅
3. `chore: update docs for iteration 006 completion` (pending)

---

## Progress Summary

| Epic | Status |
|------|--------|
| EPIC 0: Repo Skeleton | ✅ COMPLETED |
| EPIC 1: Domain + Persistence | ✅ COMPLETED |
| EPIC 2: Import Pipeline | ✅ COMPLETED |
| EPIC 3: DecisionEngine | ✅ COMPLETED |
| EPIC 4.1: Provider Abstractions | ✅ COMPLETED |
| EPIC 4.2.2: DDL Candidate Normalization | ✅ COMPLETED |
| EPIC 4.2.1: DDL Discovery & Search | 🔜 Next |
