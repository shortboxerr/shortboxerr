# Design Decisions & Technical Debt

This document captures intentional design decisions and known technical debt discovered during code audits.

---

## Audit: Iteration 190 - Git History Test Analysis

**Date**: 2026-02-27  
**Scope**: Review of test modifications that may have masked bugs

### Findings Summary

| ID | Type | Severity | Status | Description |
|----|------|----------|--------|-------------|
| AUDIT-001 | Regression Bug | CRITICAL | ✅ Fixed (Iter 191) | GetComicsAdapter lost 5 RSS/category methods |
| AUDIT-002 | Code Bug | MEDIUM | ✅ Fixed (Iter 192) | DdlReleaseParser regex truncates hyphenated groups |
| AUDIT-003 | Missing Feature | LOW | 📋 Backlog | "Absolute" edition detection not implemented |
| AUDIT-004 | Missing Feature | LOW | 📋 Backlog | "Marvel NOW" reboot indicator (no parens) not detected |

---

### AUDIT-001: GetComicsAdapter Feature Regression

**Classification**: Regression Bug (code deleted, tests deleted to hide failure)

**Timeline**:
1. `dad408b` (Feb 10) - Added `GetPublisherRssFeedAsync`, `GetPublisherAsync` to GetComicsAdapter
2. `b78ab5f` (earlier) - Added RSS feed and category browsing features
3. `a6192fe` (Feb 25) - Renamed GetComicsAdapterV2 → GetComicsAdapter, **replacing** the old adapter
4. `4d4afa9` (Feb 27) - Deleted 669 lines of tests calling "non-existent" methods

**Lost Methods**:
- `GetRssFeedAsync(int limit, CancellationToken)`
- `GetCategoryAsync(string category, int limit, CancellationToken)`
- `GetCategoryRssFeedAsync(string category, int limit, CancellationToken)`
- `GetPublisherRssFeedAsync(string publisher, int limit, CancellationToken)`
- `GetPublisherAsync(string publisher, int limit, CancellationToken)`

**Impact**: GetComics adapter lost feature parity with ReadComicOnlineAdapter

**Resolution**: Restore methods from git history (`git show a6192fe^:src/.../GetComicsAdapter.cs`) or port from ReadComicOnlineAdapter

---

### AUDIT-002: DdlReleaseParser Release Group Extraction Bug

**Classification**: Code Bug (test expectations changed to match buggy behavior)

**Root Cause**: The release group regex `\s-\s*([^-]+?)\s*$` uses `[^-]+?` which stops at the first hyphen.

**Example**:
```
Input:  "Batman 001 (2023) - DC-Empire.cbz"
Actual: ReleaseGroup = "Empire", Publisher = "DC" (found separately)
Expected: ReleaseGroup = "DC-Empire", Publisher = "DC Comics" (from lookup)
```

**Impact**: The `ReleaseGroupPublishers` dictionary (which maps "DC-Empire" → "DC Comics") is never used because the full group name is never extracted.

**Masked By**: Commit `16c1651` changed test expectations:
- "DC Comics" → "DC"
- "Image Comics" → "Image"
- Removed `PublisherHint` assertion

**Resolution**: Fix the regex to capture the full release group including internal hyphens. Suggested pattern:
```csharp
// Match " - GroupName" at end, where GroupName can contain hyphens
@"\s-\s*([A-Za-z][\w-]+?)\s*$"
```

---

### AUDIT-003: Absolute Edition Detection (Missing Feature)

**Classification**: Missing Feature (documented, not masked)

**Description**: Parser doesn't recognize "Absolute" as an edition type or collection indicator.

**Example**:
```
Input: "Absolute Sandman Vol 1 (Vertigo) (2006).cbz"
Actual: editionType = null, isCollection = false
Desired: editionType = "Absolute", isCollection = true
```

**Decision**: Documented as future enhancement in golden test fixture. Not a regression.

---

### AUDIT-004: Reboot Indicator Without Parentheses (Missing Feature)

**Classification**: Missing Feature (documented, not masked)

**Description**: Parser only detects reboot indicators in parentheses like "(New 52)". Indicators without parentheses like "Marvel NOW" are not detected.

**Example**:
```
Input: "Avengers Marvel NOW 001.cbz"
Actual: rebootIndicator = null
Desired: rebootIndicator = "Marvel NOW"
```

**Decision**: Removed test case with note. Would require more sophisticated NLP to distinguish "Marvel NOW" from regular title words.

---

## Legitimate Test Fixes (No Bug Masking)

These test changes were categorized as legitimate:

| Commit | Change | Classification |
|--------|--------|----------------|
| `d0ebc29` | ActivityService test isolation via IAsyncLifetime | Test bug (isolation) |
| `b1c6d72` | MetronClientTests mock setup for IServiceProvider | Test bug (mocks) |
| `d96746e` | GetComicsAdapterTests HTML fixture format | Test bug (fixtures) |
| `5a3ad89` | Remove duplicate DTOs causing Swagger conflict | Code bug (fixed) |
| `68c9fb0` | RCO not enabled by default expectation | Stale test (intentional change) |
| `e13deaa` | CoverService fresh HttpClient per call | Test bug (mocks) |
| `e13deaa` | Mega.nz now supported | Stale test (feature added) |
| `008c5b1` | Enum string serialization expectations | Stale test (serialization change) |

---

## Action Required

1. **AUDIT-001**: Create backlog item to restore GetComicsAdapter RSS/category methods
2. **AUDIT-002**: Create backlog item to fix release group regex
3. Document AUDIT-003 and AUDIT-004 as future enhancements in BACKLOG.md
