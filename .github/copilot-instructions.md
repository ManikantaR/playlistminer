# GitHub Copilot Instructions for PlaylistMiner

## Project Overview
PlaylistMiner is a YouTube playlist organizer that syncs playlists, auto-categorizes videos with multi-tagging, and reorganizes them by topic. Self-hosted via Podman containers.

## Tech Stack
- **Backend API:** C# / .NET 10 / ASP.NET Core Web API
- **Background Jobs:** C# / .NET 10 / Quartz.NET Worker Service
- **Orchestration:** .NET Aspire (AppHost + ServiceDefaults)
- **Database:** PostgreSQL 16 with pg_trgm extension
- **ORM:** Entity Framework Core 8
- **Frontend:** Next.js 14 + TypeScript + Tailwind CSS
- **API Client:** Auto-generated from OpenAPI spec
- **Containers:** Podman + podman-compose

## Code Style & Conventions

### C# Backend
- Use .NET 10 with C# 13 features (primary constructors, collection expressions, extensions)
- Nullable reference types enabled globally
- Follow Clean Architecture: Core (domain) → Infrastructure (data, external) → Api/Worker (presentation)
- Use record types for DTOs and value objects
- Use interfaces for all services (dependency injection)
- Async/await everywhere with CancellationToken on all async methods
- Use FluentValidation for request validation
- Use IOptions<T> pattern for configuration
- Use ILogger<T> for structured logging (Serilog)
- Entity Framework Core with Fluent API configuration (no data annotations on entities)

### Testing (CRITICAL — TDD)
- **Red-Green-Refactor:** Always write the failing test FIRST, then implement
- xUnit for all C# tests
- FluentAssertions for readable assertions (`.Should().Be()`, `.Should().Contain()`)
- Moq for mocking dependencies in unit tests
- Testcontainers for integration tests (real PostgreSQL in container)
- WebApplicationFactory for API controller tests
- Test naming: `Test_MethodName_Scenario_ExpectedResult`
- One assertion concept per test (multiple `.Should()` is fine if testing one concept)
- Arrange-Act-Assert pattern

### Frontend
- TypeScript strict mode, no `any` types
- React functional components with hooks
- TanStack Query (react-query) for server state
- Tailwind CSS utility classes, no custom CSS unless necessary
- Jest + React Testing Library for component tests
- Playwright for E2E tests
- File naming: PascalCase for components, camelCase for hooks/utils

### .NET Aspire
- AppHost orchestrates all services for local development (replaces docker-compose for dev)
- ServiceDefaults provides shared config: health checks, OpenTelemetry, resilience, service discovery
- PostgreSQL provisioned via Aspire with volume persistence
- Api and Worker receive DB connection via Aspire resource injection
- Next.js frontend added as npm app with API URL injected
- Podman compose is for production deployment only

## Architecture Rules
- Core project has ZERO external dependencies (no NuGet packages except abstractions)
- Infrastructure references Core, never the reverse
- Api and Worker reference Core, Infrastructure, and ServiceDefaults
- DTOs live in Core (shared between layers)
- Domain entities are internal to Infrastructure (exposed via DTOs)
- Never return EF entities from API endpoints — always map to DTOs

## Database
- PostgreSQL 16 with pg_trgm extension for fuzzy search
- EF Core migrations (code-first)
- Connection string from environment variable
- Parameterized queries only — never concatenate SQL

## YouTube API
- API key for public data (videos.list, search.list)
- OAuth 2.0 for private playlist operations
- Rate limit: max 10 requests/second
- Retry with exponential backoff on 429/503
- Batch video IDs in groups of 50
- Monitor quota usage (10,000 units/day)

## Key Domain Concepts
- **Video:** YouTube video with metadata and multi-tags
- **Tag:** Category label (e.g., "React", "AWS") with keyword rules
- **TagRule:** Keyword → Tag mapping with weight, used for auto-categorization
- **Inbox:** Designated YouTube playlist where new videos land
- **Suggestion:** Auto-generated tag proposal, always requires user acceptance
- **Undo Log:** 7-day window to reverse video moves between playlists

## Implementation Order
Follow the numbered prompts in docs/prompts/ sequentially:
1. Project scaffolding
2. Containers & database schema
3. YouTube API integration
4. Categorization engine
5. API layer
6. Frontend
7. Scheduler & jobs
8. Search & import
