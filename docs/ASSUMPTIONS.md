# Assumptions (Auto-logged)

- Defaults should match Mylar3 behavior. If exact values unknown, use reasonable Arr-like defaults and mark TODO.
- Preferred archive: CBZ
- Default DB: SQLite for MVP
- Collections modeled as EditionTitle + EditionContents + EditionFileAsset
- Use git and commit at logical breakpoints.
- All dev happens inside the Dev Container.

## Iteration 110: Mylar3 Config Import

- **INI Format**: Assumed standard INI format with `[Section]` headers and `Key = Value` pairs
- **Mylar3 extra_newznabs**: Uses Python tuple format `[('name', 'host', verify, 'apikey', 'uid', enabled, 'categories'), ...]`
- **Default Ports**: SABnzbd = 8080, NZBGet = 6789 (matching Mylar3 defaults)
- **Boolean Values**: Accepted values: true/1/yes/on = true, everything else = false
- **Indexer Sections**: Supports `[Newznab]`, `[Newznab1]`-`[Newznab20]`, and `extra_newznabs` in `[General]`
- **SABnzbd Section**: Can be `[SABnzbd]` or `sab_*` keys in `[General]`
- **NZBGet Section**: Can be `[NZBGet]` or `nzbget_*` keys in `[General]`
- **Case Insensitivity**: Section names and keys are case-insensitive (matching INI standard)
