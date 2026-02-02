# Self-Check Rubric

## Must Pass
- dotnet build succeeds
- dotnet test succeeds
- API starts and /api/v1/system/health returns 200
- DB migrations apply cleanly (SQLite)

## Should Pass
- At least one vertical slice exists per epic in progress
- Logging is structured and includes correlation id
- DecisionEngine outputs include rejection reasons (once implemented)

## Documentation
- New endpoints listed in docs/API.md
- New configs listed in config/env.example and docs/ARCHITECTURE.md
