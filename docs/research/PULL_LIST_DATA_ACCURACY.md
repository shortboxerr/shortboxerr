# Pull List Data Accuracy Research

## EPIC 15.9 Investigation Results

### Problem Statement
Pull list data in Shortboxerr doesn't match Mylar3's data for the same week.

---

## 1. Mylar3 Pull List Data Source

### Primary Source: WalkSoftly Aggregator

Mylar3 uses an external service called **WalkSoftly** for weekly pull list data:
- **URL**: `https://walksoftly.itsaninja.party/newcomics.php`
- **Parameters**: `week` (week number), `year`
- **Response**: JSON array with pre-mapped ComicVine IDs

#### WalkSoftly Response Fields:
```json
{
  "series": "Series Name",
  "alias": "Alternative name",
  "issue": "#1",
  "publisher": "Publisher Name",
  "shipdate": "2024-01-10",
  "coverdate": "2024-02-01",
  "comicid": 12345,        // ComicVine volume ID
  "issueid": 67890,        // ComicVine issue ID
  "weeknumber": 2,
  "link": "annual_link",
  "year": 2024,
  "volume": "1",
  "seriesyear": "2023",
  "type": "format"
}
```

### Shortboxerr Source: ComicVine Direct

Shortboxerr queries ComicVine directly:
- **Endpoint**: `issues/?filter=store_date:{date_range}`
- **Response**: Raw ComicVine issue data

### Key Difference

| Aspect | Mylar3 | Shortboxerr |
|--------|--------|-------------|
| Data Source | WalkSoftly aggregator | ComicVine direct |
| Data Freshness | Aggregated, potentially more complete | Dependent on CV update timing |
| Pre-mapped IDs | Yes (volume + issue IDs included) | Must fetch separately |
| Publisher Info | Included in response | Requires additional lookup |

---

## 2. ComicVine Date Fields

### store_date vs cover_date

| Field | Description | Usage |
|-------|-------------|-------|
| `store_date` | Actual in-store release date | **Correct for pull lists** |
| `cover_date` | Date printed on cover (often 1st of month) | Marketing/archive purposes |

#### Our Implementation: ✅ CORRECT

Shortboxerr uses `store_date` for pull list queries:
```csharp
// ComicVineClient.cs
var url = $"issues/?filter=store_date:{storeDateFilter}&sort=store_date:asc";
```

### ComicVine Update Delays

ComicVine frequently has delays updating issue information:
- Sometimes not updated until Thursday, Friday, or later
- Occasionally updates on Sunday after Wednesday release
- This creates a gap where issues are "known" but not searchable

**WalkSoftly mitigates this** by potentially aggregating from multiple sources.

---

## 3. Week Boundary Calculation

### Comic Release Schedule
- **Release Day**: Wednesday (US standard)
- **Week Definition**: Sunday-to-Saturday (US convention)

### Our Implementation
```csharp
// PullListService.cs
private static DateTime GetWeekStart(DateTime date)
{
    // Week starts on Sunday (US standard for comics)
    var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
    return date.Date.AddDays(-diff);
}

private static DateTime GetReleaseDay(DateTime weekStart)
{
    // Release day is Wednesday
    return weekStart.AddDays((int)DayOfWeek.Wednesday);
}
```

**Status**: ✅ CORRECT - Matches standard US comic release conventions

---

## 4. Publisher Filtering

### Mylar3 Approach
Mylar3 has configurable **Ignored Publishers** list:
```python
def ignored_publisher_check(publisher):
    if mylar.CONFIG.IGNORED_PUBLISHERS is not None and any(
        [x for x in mylar.CONFIG.IGNORED_PUBLISHERS 
         if x.lower() == publisher.lower() or 
            ('*' in x and re.sub(r'\*', '', x.lower()).strip() in publisher.lower())]
    ):
        return True
    return False
```

Features:
- Exact match or wildcard (`*`) support
- Case-insensitive matching

### Our Implementation
Currently, publisher filtering is available in the discovery view but not enforced globally.

**Recommendation**: Add configurable ignored publishers list.

---

## 5. Variant Cover Handling

### Common Variant Types
- Cover A/B/C (retailer variants)
- Incentive variants (1:10, 1:25, etc.)
- Convention exclusives
- Digital exclusives

### ComicVine Behavior
- Variants may have same `store_date` as main issue
- Some tracked as separate issues, some not
- Inconsistent data quality

### Our Implementation
No specific variant filtering - all issues with matching store_date are included.

---

## 6. Data Augmentation Options

### Alternative Data Sources

| Source | Type | Availability | Notes |
|--------|------|--------------|-------|
| League of Comic Geeks | Web/API | Limited | Reliable weekly lists |
| PreviewsWorld | Web | Public | Diamond distributor data |
| Publisher RSS | Feeds | Varies | Direct from publishers |
| WalkSoftly | API | Unknown | Used by Mylar3 |

### Recommendation

1. **Short-term**: Document the ComicVine delay limitation
2. **Medium-term**: Add comparison endpoint for debugging
3. **Long-term**: Consider WalkSoftly integration or alternative aggregator

---

## 7. Conclusions

### Root Causes of Discrepancy

1. **Data Source Difference**: WalkSoftly vs ComicVine direct
2. **ComicVine Update Delays**: Up to 4+ days for new releases
3. **Publisher Filtering**: Mylar3 has configurable exclusions
4. **Aggregation Timing**: WalkSoftly may have fresher data

### Our Implementation Status

| Component | Status | Notes |
|-----------|--------|-------|
| Date Field (store_date) | ✅ Correct | Using proper field |
| Week Boundaries | ✅ Correct | Sunday-Saturday, Wed release |
| Publisher Filter | ⚠️ Partial | Available but not configurable globally |
| Variant Handling | ⚠️ None | All variants included |
| Alternative Sources | ❌ None | ComicVine only |

### Recommendations

1. **Add Debug Endpoint**: Export pull list for comparison
2. **Add Ignored Publishers**: Configurable list in settings
3. **Document Limitations**: ComicVine delays are expected
4. **Consider WalkSoftly**: Evaluate as alternative data source
