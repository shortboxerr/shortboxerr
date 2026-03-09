# Test Baseline

**Established**: Iteration 193 (Feb 27, 2026)
**Last Verified**: Iteration 193

## Current Baseline

| Metric | Value |
|--------|-------|
| **Total Tests** | 2589 |
| **Passed** | 2589 |
| **Failed** | 0 |
| **Skipped** | 0 |
| **Flaky** | 0 |

## Test Counts by Class (Top 30)

| Test Class | Count |
|------------|-------|
| DownloadHostResolverTests | 100 |
| PremiumHostResolverTests | 79 |
| DdlReleaseParserTests | 67 |
| MegaResolverTests | 58 |
| NzbgetClientTests | 55 |
| NzbReleaseParserTests | 55 |
| QBittorrentClientTests | 54 |
| SiteHealthServiceTests | 53 |
| PullListServiceTests | 51 |
| SearchResultScorerTests | 49 |
| ComicVineIdParserTests | 49 |
| VariantCoverServiceTests | 42 |
| TorrentImportServiceTests | 39 |
| LogFormattingTests | 38 |
| ArchiveExtractorTests | 37 |
| SensitiveDataMaskingTests | 35 |
| HostReliabilityServiceTests | 35 |
| DdlImportServiceTests | 35 |
| Mylar3ConfigImporterTests | 34 |
| ReadComicOnlineAdapterTests | 33 |
| HostBlacklistServiceTests | 32 |
| CloudflareBypassServiceTests | 32 |
| NzbFilterServiceTests | 29 |
| DelugeClientTests | 29 |
| DecisionEngineTests | 29 |
| SetupStatusServiceTests | 28 |
| SabnzbdDownloadProviderTests | 28 |
| ComicVineRequestBatcherTests | 28 |
| SettingsEndpointTests | 27 |
| GetComicsAdapterTests | 27 |

## Regression Prevention

A pre-commit hook (`scripts/hooks/pre-commit`) enforces:
- Tests must pass before commit (when test files are staged)
- Test count must not decrease from baseline

### Installing Hooks

```bash
cp scripts/hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

### Bypass (emergency only)

```bash
git commit --no-verify -m "chore: emergency fix"
```

## Updating Baseline

When legitimately removing tests (e.g., feature removal):
1. Update this file with new count
2. Update `TEST_MINIMUM` in `scripts/hooks/pre-commit`
3. Copy updated hook to `.git/hooks/`
4. Document reason in commit message

## Flaky Tests

Tests marked with `[Trait("Category", "Flaky")]` are quarantined and excluded from CI blocking.

**Currently quarantined**: None
