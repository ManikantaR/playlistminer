# PlaylistMiner — Agent Instructions

## Project
PlaylistMiner is a self-hosted YouTube playlist organizer that syncs playlists, auto-categorizes videos with multi-tagging, and reorganizes them by topic.

## Architecture
- **C# .NET 10 backend:** ASP.NET Core Web API (REST) + Quartz.NET Worker Service
- **.NET Aspire:** Orchestrates all services for local dev (AppHost + ServiceDefaults)
- **PostgreSQL 16:** Primary data store with pg_trgm for fuzzy search
- **Next.js 14 frontend:** TypeScript + Tailwind CSS, consumes OpenAPI-generated client
- **Ollama (optional):** Mistral model for AI-powered categorization fallback
- **Podman:** Production containerization (5 containers)

## Rules for All Code
1. **TDD (Red-Green-Refactor):** Write failing test FIRST. No implementation without a test.
2. **Clean Architecture:** Core (zero deps) → Infrastructure → Api/Worker
3. **No data annotations on entities.** Use Fluent API in EF Core.
4. **CancellationToken on all async methods.**
5. **DTOs for API responses.** Never expose EF entities.
6. **Parameterized SQL only.** No string concatenation in queries.

## Subprojects
Projects with specific AGENTS.md overrides:
- `src/PlaylistMiner.Api/` — REST API (has own AGENTS.md: controller patterns, DTOs)
- `src/PlaylistMiner.Worker/` — Quartz.NET jobs (has own AGENTS.md: job patterns, quota)
- `src/web/` — Next.js frontend (has own AGENTS.md: component patterns, hooks)

Projects without overrides (inherit this root file):
- `src/PlaylistMiner.Core/` — Domain models, interfaces, DTOs (zero dependencies)
- `src/PlaylistMiner.Infrastructure/` — EF Core, YouTube client, repositories, categorization
- `src/PlaylistMiner.AppHost/` — .NET Aspire orchestration
- `src/PlaylistMiner.ServiceDefaults/` — Shared Aspire config

## Implementation Order
Follow prompts in `.github/prompts/` numbered 01-09 sequentially.

## Database Backup
- Automated daily via BackupJob (Quartz, 4 AM)
- Manual via API: POST /api/backup/trigger
- Files stored in ./data/backups/, 7-day retention
- See docs/SPEC.md Section 10 for full backup spec

## Key Domain Rules
- Tags are SUGGESTED, never auto-applied
- Videos MOVE between playlists with 7-day undo window
- Categorization pipeline: keyword match → TF-IDF → optional Ollama
- Self-learning: accepted tags strengthen rules, rejected tags weaken them
- YouTube API quota: 10,000 units/day — defer if exhausted
