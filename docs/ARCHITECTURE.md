# PlaylistMiner — Architecture Decision Records

## ADR-001: C# + Python Backend Split → C# Only

**Decision:** Use C# for the entire backend (API + Worker) instead of splitting C#/Python.

**Rationale:** The categorization engine (keyword matching + TF-IDF) is implementable in C# using ML.NET. Adding Python would require a second runtime, inter-process communication, and complicates the container setup. Ollama is accessed via HTTP API, language-agnostic.

**Consequence:** Use ML.NET for TF-IDF. If ML.NET proves insufficient, revisit adding a Python sidecar.

## ADR-002: Separate API and Worker Containers

**Decision:** Run C# API and C# Worker as separate containers from the same solution.

**Rationale:** The API handles HTTP requests and must stay responsive. The Worker runs Quartz jobs (sync, categorization) that can be CPU-intensive. Separating them prevents job execution from blocking API responses. Both share the same EF Core data access layer via a shared project.

## ADR-003: PostgreSQL Full-Text Search over Dedicated Search Engine

**Decision:** Use pg_trgm + full-text search instead of Meilisearch/Elasticsearch.

**Rationale:** For personal use with < 10K videos, PostgreSQL's built-in search is sufficient. Avoids an extra container and data sync complexity. Can always add a dedicated search engine in Phase 2 if needed.

## ADR-004: Always Suggest, Never Auto-Apply Tags

**Decision:** Auto-categorization always produces suggestions that require user acceptance.

**Rationale:** User wants control over tag accuracy. Every suggestion is a learning opportunity — accepted suggestions reinforce rules, rejected ones weaken them.

## ADR-005: Move with 7-Day Undo Window

**Decision:** When organizing videos into topic playlists, remove from source and add to target, with a 7-day undo log.

**Rationale:** Keeps inbox clean automatically while providing a safety net for miscategorization. Undo log entries auto-expire after 7 days.

## ADR-006: Google Takeout for Watch Later Import

**Decision:** Use Google Takeout CSV export for Watch Later videos instead of API workarounds.

**Rationale:** YouTube API blocks Watch Later read access since 2016. Takeout is official and reliable. Video IDs from CSV are hydrated via videos.list API. Supported via CLI (bulk) and UI upload (ad-hoc).

## ADR-007: Podman over Docker

**Decision:** Use Podman with podman-compose for containerization.

**Rationale:** User preference. Podman is daemonless, rootless by default, and CLI-compatible with Docker.

## ADR-008: Red-Green TDD with xUnit

**Decision:** Follow strict Red-Green-Refactor TDD cycle using xUnit for all C# code.

**Rationale:** Ensures test coverage from the start, catches regressions early, and produces better-designed code through test-first thinking. Integration tests use Testcontainers for real PostgreSQL instances.
