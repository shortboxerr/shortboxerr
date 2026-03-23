SHELL := /bin/bash

# Dev server URLs (override if you use different hosts/ports)
API_URL ?= http://127.0.0.1:5052
UI_URL  ?= http://127.0.0.1:8585

.PHONY: help restore build test run fmt lint install-hooks check-servers

help:
	@echo "Targets:"
	@echo "  restore        dotnet restore"
	@echo "  build          dotnet build"
	@echo "  test           dotnet test"
	@echo "  run            dotnet run --project src/Shortboxerr.Api"
	@echo "  check-servers  verify API + Vite dev servers respond (see API_URL, UI_URL)"
	@echo "  fmt            dotnet format (if available) or noop"
	@echo "  install-hooks  install git hooks from scripts/hooks"

restore:
	dotnet restore

build:
	dotnet build -c Debug

test:
	dotnet test -c Debug

run:
	dotnet run --project src/Shortboxerr.Api

fmt:
	@echo "If dotnet-format is installed in the container, run: dotnet format"
	@echo "TODO: enable dotnet format in EPIC 0 once solution exists."

lint:
	@echo "TODO: add analyzers later."

install-hooks:
	@mkdir -p .git/hooks
	@cp -f scripts/hooks/commit-msg .git/hooks/commit-msg
	@chmod +x .git/hooks/commit-msg
	@echo "Installed commit-msg hook."

# Verify dev servers: API liveness + health (incl. DB) and Vite on UI_URL.
# Run inside the dev container after starting backend and frontend (README).
check-servers:
	@command -v curl >/dev/null || { echo "check-servers: curl is required" >&2; exit 1; }
	@echo "Checking API liveness $(API_URL)/ping ..."
	@body=$$(curl -sfS --max-time 5 "$(API_URL)/ping") || { echo "FAIL: API not reachable at $(API_URL) (start: cd src/Shortboxerr.Api && dotnet run --urls \"http://0.0.0.0:5052\")" >&2; exit 1; }
	@echo "$$body" | grep -qE '^pong$$|^"pong"$$' || { echo "FAIL: unexpected /ping response: $$body" >&2; exit 1; }
	@echo "  OK"
	@echo "Checking API health $(API_URL)/health ..."
	@health=$$(mktemp) && trap 'rm -f "$$health"' EXIT; \
	code=$$(curl -sfS --max-time 10 -o "$$health" -w '%{http_code}' "$(API_URL)/health") || { echo "FAIL: /health request failed" >&2; exit 1; }; \
	test "$$code" = "200" || { echo "FAIL: /health HTTP $$code (expected 200). Body:" >&2; cat "$$health" >&2; exit 1; }; \
	grep -q '"status"[[:space:]]*:[[:space:]]*"Healthy"' "$$health" || { echo "FAIL: API health is not Healthy. Response:" >&2; cat "$$health" >&2; exit 1; }
	@echo "  OK (Healthy)"
	@echo "Checking frontend $(UI_URL)/ ..."
	@curl -sfS --max-time 5 -o /dev/null "$(UI_URL)/" || { echo "FAIL: frontend not reachable at $(UI_URL) (start: cd ui && npm run dev). If Vite moved to 8586+, set UI_URL=http://127.0.0.1:8586" >&2; exit 1; }
	@echo "  OK"
	@echo "All dev servers look good."
