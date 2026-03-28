# Self-Check: Iteration 236

## Build Status

- [x] `dotnet build` succeeds
- [x] `cd ui && npm run build` succeeds

## Lint Status

- [x] `npm run lint` — **15** warnings (down from 22); 0 errors

## Test Status

- [x] `dotnet test` — 2610 passed, 0 failed

## Files Changed

| File | Type |
|------|------|
| `ui/src/pages/ManualImportPage.tsx` | EditMatchModal |
| `ui/src/pages/SeriesDetailPage.tsx` | memo + eslint |
| `ui/src/pages/LogsPage.tsx` | eslint |
| `ui/eslint.config.js` | doc comment |
| `docs/BACKLOG.md` | 14.23 |
| `docs/WORKLOG.md` | Iteration 236 |
| `docs/SELF_CHECK.md` | this file |
| `src/Shortboxerr.Api/wwwroot/**` | Vite output |

## Commits

1. `fix(ui): reduce ESLint warnings for 14.23`

## Summary

Partial progress on BACKLOG 14.23: fixed exhaustive-deps and incompatible-library noise; remaining warns are mostly set-state-in-effect, only-export-components, and client `any`.
