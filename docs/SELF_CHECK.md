# Self-Check: Iteration 230

## Build Status
- [x] `dotnet restore src/Shortboxerr.Api/Shortboxerr.Api.csproj` + `dotnet publish ... --no-restore` succeeds
- [x] `dotnet test` — `CacheEventPublisherTests` passed

## Summary
Unblocks PR CI: Docker image build no longer restores missing test project; cache event tests wait for async publishes instead of a fixed 50ms sleep.

## Commits
1. `fix(ci): Docker restore and flaky cache event tests` — (this iteration)
