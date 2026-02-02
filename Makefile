SHELL := /bin/bash

.PHONY: help restore build test run fmt lint install-hooks

help:
	@echo "Targets:"
	@echo "  restore        dotnet restore"
	@echo "  build          dotnet build"
	@echo "  test           dotnet test"
	@echo "  run            dotnet run --project src/Shortboxerr.Api"
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
