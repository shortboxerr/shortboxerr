SHELL := /bin/bash

# Dev server URLs (override if you use different hosts/ports)
API_URL ?= http://127.0.0.1:5052
UI_URL  ?= http://127.0.0.1:8585

.PHONY: help restore build test run fmt lint install-hooks dev-up dev-down check-servers

help:
	@echo "Targets:"
	@echo "  restore        dotnet restore"
	@echo "  build          dotnet build"
	@echo "  test           dotnet test"
	@echo "  run            dotnet run --project src/Shortboxerr.Api"
	@echo "  dev-up         start backend (5052) + frontend (8585) in background"
	@echo "  dev-down       stop backend + frontend dev servers"
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

# Start backend and frontend in background with logs under .logs/.
dev-up:
	@mkdir -p .logs
	@echo "Checking backend port 5052 is free ..."
	@lsof -i :5052 -sTCP:LISTEN >/dev/null 2>&1 && { echo "FAIL: port 5052 is already in use. Run 'make dev-down' or stop the process first." >&2; exit 1; } || true
	@echo "Checking frontend port 8585 is free ..."
	@lsof -i :8585 -sTCP:LISTEN >/dev/null 2>&1 && { echo "FAIL: port 8585 is already in use. Run 'make dev-down' or stop the process first." >&2; exit 1; } || true
	@echo "Starting backend on 5052 ..."
	@nohup bash -lc 'cd src/Shortboxerr.Api && dotnet run --urls "http://0.0.0.0:5052"' > .logs/backend.log 2>&1 &
	@echo "Waiting for backend readiness ($(API_URL)/ping) ..."
	@for i in {1..30}; do \
		if curl -sfS --max-time 2 "$(API_URL)/ping" >/dev/null 2>&1; then \
			echo "  Backend is ready."; \
			break; \
		fi; \
		if [ $$i -eq 30 ]; then \
			echo "FAIL: backend did not become ready in time. See .logs/backend.log" >&2; \
			exit 1; \
		fi; \
		sleep 1; \
	done
	@echo "Starting frontend on 8585 ..."
	@nohup bash -lc 'cd ui && npm run dev -- --host 0.0.0.0 --port 8585 --strictPort' > .logs/frontend.log 2>&1 &
	@echo "Waiting for frontend readiness ($(UI_URL)/) ..."
	@for i in {1..20}; do \
		if curl -sfS --max-time 2 -o /dev/null "$(UI_URL)/"; then \
			echo "  Frontend is ready."; \
			break; \
		fi; \
		if [ $$i -eq 20 ]; then \
			echo "FAIL: frontend did not become ready in time. See .logs/frontend.log" >&2; \
			exit 1; \
		fi; \
		sleep 1; \
	done
	@$(MAKE) check-servers || { echo "Startup check failed. See .logs/backend.log and .logs/frontend.log" >&2; exit 1; }
	@echo "Servers started. Logs: .logs/backend.log, .logs/frontend.log"

dev-down:
	@api_pid=$$(lsof -t -i :5052 -sTCP:LISTEN 2>/dev/null | head -n1); \
	if [ -n "$$api_pid" ]; then kill "$$api_pid" >/dev/null 2>&1 || true; fi
	@ui_pid=$$(lsof -t -i :8585 -sTCP:LISTEN 2>/dev/null | head -n1); \
	if [ -n "$$ui_pid" ]; then kill "$$ui_pid" >/dev/null 2>&1 || true; fi
	@sleep 1
	@echo "Stopped backend/frontend dev servers (if they were running)."

# Verify dev servers: API liveness + health (incl. DB) and Vite on UI_URL.
# Run inside the dev container after starting backend and frontend (README).
check-servers:
	@command -v curl >/dev/null || { echo "check-servers: curl is required" >&2; exit 1; }
	@echo "Checking API liveness $(API_URL)/ping ..."
	@code=$$(curl -sfS --max-time 5 -o /dev/null -w '%{http_code}' "$(API_URL)/ping") || { echo "FAIL: API not reachable at $(API_URL) (start: cd src/Shortboxerr.Api && dotnet run --urls \"http://0.0.0.0:5052\")" >&2; exit 1; }; \
	test "$$code" = "200" || { echo "FAIL: /ping HTTP $$code (expected 200)" >&2; exit 1; }
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
