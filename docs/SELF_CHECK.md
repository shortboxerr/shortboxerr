# Self-Check: Iteration 237

## Lint

- [x] `cd ui && npm run lint` — **0** warnings (`--max-warnings 0`)

## Build

- [x] `cd ui && npm run build`
- [x] `dotnet build` / `dotnet test` — 2610 passed

## Files

Theme provider split, toast module split, pages/layout/client typing, CI ESLint, docs.

## Commits

1. `fix(ui): zero ESLint warnings and CI lint gate`
2. `docs: add ui lint to CONTRIBUTING quality gates`

## Summary

UI lint is clean; regressions prevented via `max-warnings 0` and CI.
