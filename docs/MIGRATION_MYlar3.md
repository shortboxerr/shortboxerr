# Mylar3 Migration Plan

## Goals
- Import monitored series, wanted queue, and (where possible) rule defaults.
- Re-verify and reconcile files post-import.
- Auto-classify TPB/omnibus when obvious; route ambiguous to Manual Import.

## Phases
1) Read Mylar3 SQLite (read-only)
2) Export intermediate JSON snapshot
3) Import into Shortboxerr schema
4) Post-scan reconciliation job
5) Generate migration report

(Expand with concrete mappings once Mylar DB schema is inspected.)
