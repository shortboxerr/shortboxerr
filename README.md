# Shortboxerr

Arr-like comic automation platform with Mylar3 behavioral parity and first-class collected editions.

## Development (Dev Container)
Open this repo in Cursor and use the Dev Container:
- Command Palette → “Dev Containers: Reopen in Container”
All development should happen in-container (dotnet, node, etc.).

## Dev Commands (inside container)
- dotnet build
- dotnet test
- dotnet run --project src/Shortboxerr.Api

## Docker (runtime)
- docker compose up --build

## Docs
- docs/PLAN.md is source of truth
- docs/BACKLOG.md drives implementation
- docs/ITERATION_PROTOCOL.md defines autonomous iteration rules
- .cursor/rules.md enforces Cursor behavior and git commit discipline
