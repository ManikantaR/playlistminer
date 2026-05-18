# PlaylistMiner — Claude Code Context

## What is this project?
PlaylistMiner is a self-hosted YouTube playlist organizer that syncs playlists, auto-categorizes videos using keyword matching + TF-IDF + optional Ollama, and reorganizes them into topic-based playlists.

## Role of Claude Code vs Copilot CLI
- **Claude Code:** Brainstorming, spec refinement, architecture decisions, code review
- **GitHub Copilot CLI:** Implementation, coding, scaffolding

Do not write implementation code unless explicitly asked. Focus on specs, prompts, and decisions.

## Key Files
- `docs/SPEC.md` — Full technical specification (schema, API, categorization, backup)
- `docs/ARCHITECTURE.md` — Architecture decision records
- `docs/EXECUTION-GUIDE.md` — Step-by-step build + commit guide
- `.github/prompts/` — 9 sequential implementation prompts for Copilot CLI
- `.github/skills/` — Copilot agent skills (tdd-dotnet, tdd-frontend, ef-migration, categorization-debug)
- `.github/agents/` — Copilot expert agents (.NET, frontend)
- `AGENTS.md` — Root agent instructions (cross-tool: Copilot + Claude + Codex)

## Tech Stack
- C# .NET 10 (ASP.NET Core Web API + Quartz.NET Worker)
- .NET Aspire (AppHost + ServiceDefaults for dev orchestration)
- PostgreSQL 16 (pg_trgm for fuzzy search)
- Next.js 14 + TypeScript + Tailwind CSS
- Podman containers (5: db, api, worker, web, ollama)
- Testing: xUnit + FluentAssertions + Testcontainers (C#), Jest + Playwright (frontend)
- CI/CD: GitHub Actions (3-job pipeline: .NET, frontend, E2E)

## Testing Approach
Red-Green-Refactor TDD. Write failing tests first, implement to pass, then refactor.

## Important Decisions
- Tags are always SUGGESTED, never auto-applied
- Videos MOVE between playlists (not copy) with 7-day undo window
- Watch Later imported via Google Takeout CSV (YouTube API blocks access)
- C# handles everything (no Python) — ML.NET for TF-IDF
- All categorization training data stored in PostgreSQL
- Daily automated PostgreSQL backup with 7-day retention
- AGENTS.md (uppercase) is the cross-tool standard — placed at root and per-project only where needed

## Skills Available
- `/tdd-dotnet` — Red-Green-Refactor for .NET backend
- `/tdd-frontend` — TDD for Next.js components
- `/ef-migration` — EF Core migration workflow
- `/categorization-debug` — Trace categorization pipeline issues

## Hooks Active
- **PreToolUse (Bash):** Blocks destructive commands (rm -rf, git push --force, DROP TABLE, secrets in commands)
- **PostToolUse (Edit/Write):** Auto-formats .cs files with dotnet format, .ts/.tsx with Prettier
