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

## ADR-009: Deploy on the UGREEN NAS with Docker (amends ADR-007)

**Decision:** Production deployment target is the UGREEN DXP4800+ NAS running **Docker** (UGOS), not podman. Podman remains the local dev runtime on the Mac. The agent's LLM inference (Ollama) runs on the **M1 Mac**, not the NAS. See `docs/NAS-DEPLOYMENT-SPEC.md`.

**Rationale:**
- An autonomous learning agent (VISION-v2) must be always-on; a laptop sleeps, the NAS doesn't. The NAS is the correct host for the db/api/worker/web tier.
- UGOS supports Docker + `docker compose` v2 natively; podman is not a first-class NAS runtime. The existing `podman-compose.yml` is ~portable to `docker compose`. This amends ADR-007 (podman over Docker) — podman for dev, Docker for the NAS.
- The N100 CPU runs a 7B model at ~0.5–1 tok/s, so Ollama stays on the M1 Mac; the NAS worker calls it over the LAN and degrades gracefully (queues) when the Mac is asleep, with an on-demand "Process now" trigger.
- PlaylistMiner inherits MoneyPulse's proven homelab patterns (Traefik, `*.home.lab`, `deploy-to-nas.sh`, gitleaks, the `NEXT_PUBLIC_*` build-arg fix).

**Consequence:** The homelab runs a real domain with wildcard Let's Encrypt TLS, so OAuth completes **directly on the NAS** at `https://playlistminer.home.manikantar.com/api/oauth/callback` (the redirect is browser-side; the LAN browser resolves via AdGuard and trusts the cert). The redirect URI must be registered in Google Console and the consent screen Published (Testing-mode tokens expire in 7 days). An API key is insufficient (public reads only). Mac-localhost bootstrap + token copy remains a fallback (requires identical `YouTube__EncryptionKey` both hosts). If N100 build times become painful, fall back to building on the Mac or GitHub Actions → GHCR with the NAS pulling images.
