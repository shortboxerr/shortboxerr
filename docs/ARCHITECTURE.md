# Architecture (Living Doc)

## Stack
- Backend: .NET (LTS) ASP.NET Core Web API
- DB: SQLite (default), PostgreSQL (future)
- UI: Arr-like SPA (React or Vue; decide during EPIC 5)

## Key Modules
- Core: domain models, decision engine, parsing, interfaces
- Infrastructure: EF Core, filesystem ops, indexers/acquirers adapters
- API: controllers, DTOs, validation, auth (optional MVP+)

## Mylar3 Behavioral Parity
- Release selection: DecisionEngine must mimic Mylar3 defaults.
- Media management: staging, renaming, failed handling, tagging, history.

## Collected Editions
- TPBs/omnibuses are first-class: EditionTitle + EditionContents + EditionFileAsset.

## Containerized Dev
- Use .devcontainer for all development work to avoid host pollution.
