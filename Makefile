# ConfIT — local build and test
# Requires .NET 9 SDK or later (uses ~/.dotnet if available).

DOTNET    := $(shell [ -x "$(HOME)/.dotnet/dotnet" ] && echo "$(HOME)/.dotnet/dotnet" || echo "dotnet")
TEST_OPTS := --logger "console;verbosity=minimal" -nologo
API_PORT  := 5170
SVC_PORT  := 9999
DB        := example/User.Api/User.db

.PHONY: default help build unit component component.applauncher test integration services-start services-stop ci clean

default: build test

# ── Help ────────────────────────────────────────────────────────────────────────
help: ## Show available targets
	@grep -E '^[a-zA-Z_.-]+:.*## ' $(MAKEFILE_LIST) \
	  | sort \
	  | awk 'BEGIN {FS=":.*## "}; {printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2}'

# ── Build ───────────────────────────────────────────────────────────────────────
build: ## Build library and all example projects
	@echo "Building library..."
	@$(DOTNET) build src/ConfIT.slnx --configuration Release -v minimal -nologo
	@echo "Building examples..."
	@$(DOTNET) build example/User.sln  --configuration Release -v minimal -nologo
	@echo "  ✓ Built"

# ── Tests ───────────────────────────────────────────────────────────────────────
unit: ## Run unit tests
	@$(DOTNET) test test/ConfIT.UnitTest/ConfIT.UnitTest.csproj \
	  --configuration Release $(TEST_OPTS)

component: ## Run component tests (in-process, no live services needed)
	@$(DOTNET) test example/User.ComponentTests $(TEST_OPTS)

component.applauncher: ## Run AppLauncher component tests (starts User.Api as a real process on port 5170)
	@lsof -ti :$(API_PORT) 2>/dev/null | xargs kill -9 2>/dev/null || true
	@dotnet build example/User.Api --configuration Debug -v minimal -nologo
	@$(DOTNET) test example/User.ComponentTests.AppLauncher $(TEST_OPTS)

test: unit component ## Run unit + component tests (default test suite, no live services)

integration: services-start ## Run integration tests (handles full service lifecycle)
	@$(DOTNET) test example/User.IntegrationTests $(TEST_OPTS); \
	 EXIT=$$?; $(MAKE) -s services-stop; exit $$EXIT

# ── Service lifecycle ───────────────────────────────────────────────────────────
services-start: services-stop ## Wipe DB, start services, block until both ports are ready
	@rm -f $(DB)
	@(cd example/User.Api && $(DOTNET) run > /dev/null 2>&1) & \
	 (cd example/JustAnotherService && $(DOTNET) run > /dev/null 2>&1) & \
	 printf "  Starting services"; \
	 for i in $$(seq 1 30); do \
	   nc -z localhost $(API_PORT) 2>/dev/null && \
	   nc -z localhost $(SVC_PORT) 2>/dev/null && \
	   echo " ready" && exit 0; \
	   printf '.'; sleep 1; \
	 done; echo " timed out"; exit 1

services-stop: ## Kill any running User.Api / JustAnotherService processes
	@lsof -ti :$(API_PORT) 2>/dev/null | xargs kill -9 2>/dev/null || true
	@lsof -ti :$(SVC_PORT) 2>/dev/null | xargs kill -9 2>/dev/null || true

# ── Pipelines ───────────────────────────────────────────────────────────────────
ci: build test component.applauncher integration ## Full pipeline: build + unit + component + integration

clean: services-stop ## Stop services, remove build artefacts and SQLite DB
	@rm -f $(DB)
	@find . -type d \( -name bin -o -name obj \) -not -path '*/node_modules/*' \
	  -exec rm -rf {} + 2>/dev/null || true
	@echo "  ✓ Cleaned"
