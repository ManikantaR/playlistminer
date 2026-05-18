# Prompt 09: GitHub Actions CI/CD Setup

## Context
PlaylistMiner needs CI/CD via GitHub Actions. The workflow is already defined in `.github/workflows/ci.yml`. This prompt sets up the supporting infrastructure to make it work.

## Prompt 09a: CI Pipeline Verification

```
Verify and fix the GitHub Actions CI pipeline in .github/workflows/ci.yml for PlaylistMiner.

The pipeline has 3 jobs:
1. dotnet-build-and-test: Build .NET 10 solution, run xUnit unit tests and integration tests against a PostgreSQL 16 service container with pg_trgm extension
2. frontend-build-and-test: Lint, type-check, run Jest tests, build Next.js
3. e2e-tests: Start the API against a test database, run Playwright E2E tests against it

Ensure:
- .NET 10 SDK is used (dotnet-version: '10.0.x')
- Node.js 20 with npm caching on src/web/package-lock.json
- PostgreSQL 16 service container with health checks
- pg_trgm extension enabled via psql before tests run
- Unit tests output TRX results and code coverage (XPlat Code Coverage)
- Integration tests use Testcontainers (they spin up their own PostgreSQL, but also need the service container connection string as fallback)
- E2E job depends on both build jobs passing first
- API started in background with & and health-checked before Playwright runs
- Playwright installs only chromium (fastest)
- All test results and coverage uploaded as artifacts
- Playwright HTML report uploaded as artifact

Also create a .github/workflows/codeql.yml for security scanning:
- Runs on push to main and weekly schedule
- Analyzes C# and JavaScript/TypeScript
```

## Prompt 09b: Test Infrastructure

```
Ensure the test projects are properly configured for CI:

In PlaylistMiner.IntegrationTests:
- Add a base class IntegrationTestBase that uses Testcontainers to spin up PostgreSQL
- Configure it to run EF Core migrations on startup
- Enable pg_trgm extension in the test container
- Add [Category("Integration")] attribute support for filtering

In PlaylistMiner.UnitTests:
- Add [Category("Unit")] attribute support for filtering
- Ensure all tests can run in parallel

In src/web:
- Ensure jest.config.ts outputs coverage to coverage/ folder
- Ensure playwright.config.ts:
  - Uses chromium only
  - Sets baseURL from NEXT_PUBLIC_API_URL env var
  - Outputs HTML report to playwright-report/
  - Has reasonable timeouts for CI (30s action, 60s navigation)
  - Retries once on CI (process.env.CI check)
```

## Verification
- Push to a branch and verify all 3 CI jobs pass
- Check artifacts are uploaded (test results, coverage, Playwright report)
- Verify E2E tests actually hit the running API
