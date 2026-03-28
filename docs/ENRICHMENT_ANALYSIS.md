# Comic Ingestion & Enrichment Pipeline Analysis

## Current Process Flow

### 1. Comic Discovery
Two primary sources:
- **DDL Sites** - Parsed via site adapters → `DdlImportService`
- **Manual Upload** - User uploads file → `ManualImportEndpoints`

### 2. File Parsing
Extracts from filename:
- Series name
- Issue number
- Edition type (TPB, HC, Omnibus, etc.)
- Publication year
- Publisher (optional)
- Determines if collection or single issue

### 3. Auto-Matching (`AutoMatchService`)
Searches ComicVine for matches:
- **Series match**: Title + year + publisher context
- **Issue/Edition match**: If applicable
- Returns confidence score (0-100)
- Auto-imports if confidence > threshold (default 80%)
- Holds low-confidence matches for manual review

### 4. Staging
File held pending review if:
- Match confidence below threshold
- Multiple possible matches
- No match found at all

### 5. Metadata Enrichment (on import)
When matched, syncs from ComicVine:
- Series: genres, publishers, status, creators
- Issues: numbers, dates, cover art, creators
- Collections: volume info, contents, variants
- Cover images: cached with Metron fallback

### 6. Ongoing Refresh (`MetadataRefreshService`)
Periodic updates:
- Refreshes matched series metadata
- Discovers new issues (creates stubs)
- Updates stale data
- Configurable interval (default: 30 days)

---

## Data Source Strategy

### Primary Sources
1. **ComicVine** - Main metadata provider
   - Comprehensive US/mainstream coverage
   - API rate-limited (100 req/hour free tier)
   - Good issue data, cover art
   
2. **WalkSoftly** - Upcoming releases
   - Used for pull lists only (not general enrichment)
   - Fresher data than ComicVine polling
   
3. **Metron** - Fallback for covers
   - Better indie/international coverage
   - Currently only used for missing cover art

### Gap: Secondary Enrichment
**Current:** If ComicVine match fails, no fallback enrichment
**Opportunity:** Could use Metron or other sources for:
- Missing issue data (creators, story arcs, ratings)
- UK/indie comics with poor ComicVine coverage
- Creator/team information

---

## Confidence Scoring Analysis

### Current Algorithm
Simple keyword matching:
- Exact series name match → high confidence
- Fuzzy/partial match → lower confidence
- Based on Levenshtein distance

### Limitations
- No context weighting:
  - Publisher not used in scoring
  - Year proximity not weighted
  - Known variants/reboots not considered
  
- Can't distinguish between:
  - "Batman" (DC mainline)
  - "Batman Adventures" (animated series)
  - "Batman Beyond" (alternate continuity)
  - "Batman: The Dark Knight" (specific arc)

### Mylar3 Comparison
Mylar3 uses multi-signal scoring:
- Title similarity (fuzzy)
- Publisher confirmation
- Year range validation
- Known variant detection (reboots, relaunches)
- User override history

---

## Current Strengths

✅ **ComicVine Integration** - Well-implemented, respects API limits
✅ **Confidence Filtering** - Prevents auto-matching bad results
✅ **Staged Import** - Manual review option for uncertain matches
✅ **Incremental Discovery** - Refresh finds new issues automatically
✅ **Fallback Covers** - Metron prevents missing artwork
✅ **Batch Operations** - Can refresh all series in background

---

## Improvement Opportunities

### 1. Enhanced Confidence Scoring (Medium Priority)
**Goal:** Reduce false positives and manual review workload

Add to matching algorithm:
- Publisher confirmation (exact match → +20 points)
- Year proximity bonus (±2 years → +15 points)
- Variant detection:
  - Known reboot patterns (e.g., "Batman 2011" → different from "Batman 1939")
  - "All-New", "Ultimate", "Marvel NOW" prefixes
  - "#1" issues (more weight if vol=1 or year matches)

**Impact:** Should increase confident auto-matches, reduce staging backlog

### 2. Secondary Source Enrichment (Medium Priority)
**Goal:** Better coverage for indie/international/obscure comics

Implement fallback chain:
```
ComicVine (primary)
  → Metron (currently: covers only)
  → Manual creation (user-initiated)
```

For unmatched series after ComicVine search:
- Try Metron search API
- If found, create series + fetch issue data from Metron
- Mark source for transparency

**Impact:** Handle more comics without user intervention

### 3. Enrichment Timing (Low Priority)
**Current:** Import does basic sync, refresh does full sync (slow)

**Options:**
- A) Full sync at import time (may be slow for large requests)
- B) Background job for full sync after import (better UX)
- C) Hybrid: essential fields at import, defer non-critical

**Recommendation:** Option B - background job queues refreshes after import

### 4. Failed Match Handling (Low Priority)
**Goal:** Smarter recovery for unmatched comics

Current: Manual intervention required

Could add:
- Batch re-matching with improved algorithm
- Suggest top 5 possible matches (let user pick)
- Allow manual series creation + linking
- Remember user overrides for future matching

### 5. Post-Processing & Naming (Deferred)
**Current:** Import stores, renaming is separate step

**Consider later:**
- Customizable naming schemes (Mylar3-compatible)
- Edition detection improvements
- Variant handling in filenames

---

## Decision Engine Verification Needed

**Current Status:** We have a DecisionEngine for release selection

**To Verify:**
- Prefers higher quality (bitrate, resolution)
- Respects language preferences (English-first?)
- Handles variants correctly (alternate covers shouldn't override)
- Matches Mylar3 scoring for well-known test cases

**Recommendation:** Audit against Mylar3's defaults with test dataset

---

## Recommended Roadmap

**Phase 1 (High ROI):**
1. Verify Decision Engine matches Mylar3 behavior
2. Add publisher + year weighting to confidence scoring
3. Add known variant detection (reboot patterns)

**Phase 2 (Medium ROI):**
4. Implement Metron fallback for unmatched series
5. Add batch re-matching for staging backlog
6. User feedback on match quality

**Phase 3 (Nice-to-Have):**
7. Background enrichment jobs
8. User override learning
9. Custom naming schemes

---

## Questions for Rob

1. **Data Sources:** Should we integrate Metron as fallback, or is ComicVine sufficient for your use case?

2. **Confidence Threshold:** Current default is 80%. Does that feel right, or should we be more/less aggressive?

3. **Enrichment Timing:** Prefer fast import + background enrichment, or slower import with full data upfront?

4. **Known Issues:** Any specific comics that are failing to match that we should test against?

5. **Post-Processing:** Important to support Mylar3-compatible naming, or free to design our own?
