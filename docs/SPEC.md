# PlaylistMiner — Technical Specification

## 1. Overview

PlaylistMiner is a self-hosted YouTube playlist management tool that syncs playlists, auto-categorizes videos using multi-tagging, and reorganizes them into topic-based playlists. Built for a single developer user managing tech-focused YouTube content.

## 2. Architecture

### 2.1 System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                        Podman Network                           │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐    │
│  │  Next.js      │  │  C# API      │  │  C# Worker         │    │
│  │  Frontend     │──│  (ASP.NET    │  │  (Quartz.NET)      │    │
│  │  :3000        │  │   Core)      │  │  Background Jobs   │    │
│  │              │  │  :5000        │  │                    │    │
│  └──────────────┘  └──────┬───────┘  └────────┬───────────┘    │
│                           │                    │                │
│                    ┌──────┴────────────────────┘                │
│                    │                                            │
│              ┌─────┴────────┐          ┌──────────────┐        │
│              │  PostgreSQL   │          │  Ollama       │        │
│              │  :5432        │          │  (Mistral)    │        │
│              │  Vol: ./data  │          │  :11434       │        │
│              └──────────────┘          │  Optional     │        │
│                                        └──────────────┘        │
└─────────────────────────────────────────────────────────────────┘
                           │
                    ┌──────┴──────┐
                    │  YouTube     │
                    │  Data API v3 │
                    └─────────────┘
```

### 2.2 Container Inventory

| Container | Image | Port | Volume | Purpose |
|-----------|-------|------|--------|---------|
| `pm-db` | postgres:16-alpine | 5432 | `./data/postgres` | Primary data store |
| `pm-api` | Custom .NET 10 | 5000 | — | REST API, OpenAPI/Swagger |
| `pm-worker` | Custom .NET 10 | — | — | Quartz.NET scheduler, YouTube sync, categorization |
| `pm-web` | Custom Node 20 | 3000 | — | Next.js frontend |
| `pm-ollama` | ollama/ollama | 11434 | `./data/ollama` | Optional AI categorization |

### 2.3 Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | C# / .NET 10 / ASP.NET Core Web API |
| Background Jobs | Quartz.NET in .NET Worker Service |
| Orchestration | .NET Aspire (AppHost + ServiceDefaults) |
| Database | PostgreSQL 16 with pg_trgm extension |
| ORM | Entity Framework Core 10 |
| Frontend | Next.js 14 + TypeScript + Tailwind CSS |
| API Client | Auto-generated TypeScript client from OpenAPI spec (NSwag or openapi-typescript) |
| Search | PostgreSQL full-text search + pg_trgm trigram matching |
| AI (optional) | Ollama with Mistral model |
| Containers | Podman + podman-compose (prod), .NET Aspire (dev) |
| Testing (C#) | xUnit + FluentAssertions + Testcontainers + Moq |
| Testing (Frontend) | Jest + React Testing Library + Playwright |

## 3. Database Schema

### 3.1 Entity Relationship Diagram

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────┐
│  playlists   │     │  playlist_videos  │     │   videos     │
├──────────────┤     ├──────────────────┤     ├──────────────┤
│ id (PK)      │────│ playlist_id (FK) │────│ id (PK)      │
│ youtube_id   │     │ video_id (FK)    │     │ youtube_id   │
│ name         │     │ position         │     │ title        │
│ description  │     │ added_at         │     │ description  │
│ is_inbox     │     └──────────────────┘     │ channel_name │
│ is_managed   │                               │ channel_id   │
│ topic        │     ┌──────────────────┐     │ thumbnail_url│
│ created_at   │     │   video_tags     │     │ duration     │
│ updated_at   │     ├──────────────────┤     │ published_at │
│ synced_at    │     │ video_id (FK)    │────│ status       │
└──────────────┘     │ tag_id (FK)      │     │ created_at   │
                     │ source           │     │ updated_at   │
                     │ confidence       │     │ synced_at    │
                     │ created_at       │     └──────────────┘
                     └────────┬─────────┘
                              │
                     ┌────────┴─────────┐
                     │     tags         │
                     ├──────────────────┤
                     │ id (PK)          │
                     │ name             │
                     │ slug             │
                     │ category         │
                     │ created_at       │
                     └──────────────────┘

┌──────────────────────┐     ┌──────────────────────┐
│  tag_rules           │     │  undo_log            │
├──────────────────────┤     ├──────────────────────┤
│ id (PK)              │     │ id (PK)              │
│ tag_id (FK)          │     │ video_id (FK)        │
│ keyword              │     │ action               │
│ field (title/desc)   │     │ source_playlist_id   │
│ weight               │     │ target_playlist_id   │
│ is_learned           │     │ performed_at         │
│ created_at           │     │ expires_at           │
│ updated_at           │     │ undone               │
└──────────────────────┘     └──────────────────────┘

┌──────────────────────┐     ┌──────────────────────┐
│  sync_log            │     │  import_batches      │
├──────────────────────┤     ├──────────────────────┤
│ id (PK)              │     │ id (PK)              │
│ sync_type            │     │ source (takeout/api) │
│ started_at           │     │ filename             │
│ completed_at         │     │ total_videos         │
│ videos_processed     │     │ imported_count       │
│ videos_categorized   │     │ failed_count         │
│ errors               │     │ imported_at          │
│ status               │     └──────────────────────┘
└──────────────────────┘
```

### 3.2 Field Details

**videos.status** enum: `active`, `unavailable`, `private`, `deleted`, `archived`

**video_tags.source** enum: `manual`, `rule_based`, `tfidf`, `ollama`, `suggested`

**video_tags.confidence**: float 0.0-1.0, only set for non-manual sources

**tag_rules.field** enum: `title`, `description`, `both`

**tag_rules.is_learned**: true if derived from user's manual tagging patterns, false if seeded

**undo_log.expires_at**: 7 days from `performed_at`

## 4. Seed Categories

Initial tag categories for tech content:

| Category | Tags |
|----------|------|
| Languages | C#, Python, JavaScript, TypeScript, Go, Rust, Java, SQL |
| Frontend | React, Next.js, Angular, Vue, Tailwind CSS, HTML/CSS |
| Backend | ASP.NET Core, Node.js, Django, FastAPI, Spring Boot |
| Cloud | AWS, Azure, GCP, Firebase, Terraform, Kubernetes |
| DevOps | Docker, Podman, CI/CD, GitHub Actions, Jenkins |
| Data | SQL Server, PostgreSQL, MongoDB, Redis, EF Core |
| Security | OAuth, JWT, OIDC, API Security, Penetration Testing |
| Tools | Git, GitHub, GitHub Copilot, VS Code, JetBrains |
| Architecture | Microservices, Clean Architecture, DDD, CQRS, Event Sourcing |
| AI/ML | Machine Learning, LLMs, Prompt Engineering, OpenAI, Ollama |
| General | Tutorial, Conference Talk, Live Stream, Code Review, Career |

Each tag gets initial keyword rules seeded (e.g., tag "React" → keywords: "react", "reactjs", "react.js", "hooks", "jsx", "component").

## 5. YouTube API Integration

### 5.1 Authentication

| Credential | Scope | Usage |
|-----------|-------|-------|
| API Key | Public data | `videos.list`, `search.list` — metadata hydration, public lookups |
| OAuth 2.0 | Private data | `playlists.list`, `playlistItems.list/insert/delete` — user's playlists |

OAuth uses desktop application flow. Refresh token stored encrypted in PostgreSQL `settings` table.

### 5.2 Quota Management

YouTube API daily quota: 10,000 units.

| Operation | Cost | Estimated daily usage |
|-----------|------|----------------------|
| playlists.list | 1 | ~5 |
| playlistItems.list | 1 | ~50 (paged, 50 items each) |
| videos.list | 1 | ~100 (batch 50 IDs per call) |
| playlistItems.insert | 50 | ~20 moves = 1000 |
| playlistItems.delete | 50 | ~20 moves = 1000 |

Total estimated: ~2,156 units/day — well within quota.

If quota is exhausted, the worker defers remaining operations to the next sync cycle and logs the deferral.

### 5.3 Sync Flow

```
1. Fetch all user playlists (playlists.list, mine=true)
2. For each playlist:
   a. Fetch all video IDs (playlistItems.list, paginate)
   b. Diff against local DB
   c. For new videos: batch fetch metadata (videos.list, 50 per call)
   d. Upsert into DB
3. Detect deleted/private videos (API returns status)
4. Update sync_log
```

### 5.4 Google Takeout Import

**CLI command:** `dotnet run --project src/PlaylistMiner.CLI -- import-takeout --path /path/to/Takeout/YouTube`

**UI upload:** Drag-and-drop CSV on import page

**Flow:**
1. Parse CSV (columns: Video ID, Timestamp)
2. Batch hydrate metadata via `videos.list` (50 per request)
3. Insert into `videos` table, create `import_batches` record
4. Queue for categorization

## 6. Categorization Engine

### 6.1 Pipeline

```
New Video → Keyword Matching → TF-IDF Scoring → (Optional) Ollama → Suggestions
                  │                    │                   │
           High confidence      Medium confidence    Low confidence
           (exact keyword)      (description sim.)   (LLM inference)
```

### 6.2 Layer 1: Keyword Matching (Simple)

- Match video title and description against `tag_rules` table
- Exact and substring match, case-insensitive
- Each match returns the rule's weight
- If total weight for a tag exceeds threshold (configurable, default 0.7): suggest tag

### 6.3 Layer 2: TF-IDF Scoring (Medium)

- Build TF-IDF vectors from descriptions of all manually-tagged videos per tag
- For new video, compute cosine similarity against each tag's centroid vector
- Tags above similarity threshold (configurable, default 0.5): suggest tag
- Retrain vectors whenever manual tags change (debounced, not real-time)

Implementation: Use `ML.NET` or a lightweight C# TF-IDF library. Keep vectors in memory, persist to DB for cold start.

### 6.4 Layer 3: Ollama Fallback (Optional)

- Only invoked if Layers 1+2 produce zero suggestions or all below low-confidence threshold
- Prompt template sends video title + description + list of available tags
- Parse LLM response for tag names + reasoning
- Store with source=`ollama` and the returned confidence

### 6.5 Self-Learning

When a user manually tags a video:
1. Extract keywords from that video's title and description
2. For each keyword not already in `tag_rules` for that tag:
   - Insert with `is_learned=true`, initial weight 0.3
3. For existing learned rules where this keyword matches:
   - Increment weight by 0.1 (cap at 1.0)
4. If user rejects a suggested tag:
   - Decrement associated rule weights by 0.1 (floor at 0.0)
   - Remove rules that hit 0.0

## 7. API Endpoints

### 7.1 Videos

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/videos` | List videos with pagination, filtering, search |
| GET | `/api/videos/{id}` | Get video details with tags |
| PATCH | `/api/videos/{id}/tags` | Add/remove tags on a video |
| POST | `/api/videos/{id}/accept-suggestions` | Accept suggested tags |
| POST | `/api/videos/{id}/reject-suggestions` | Reject suggested tags |
| GET | `/api/videos/suggestions` | List videos with pending tag suggestions |

**GET /api/videos query params:**
- `search` (string): fuzzy title search via pg_trgm
- `tags` (string[]): filter by tags (AND logic)
- `status` (string): filter by video status
- `playlist` (string): filter by playlist
- `page`, `pageSize`: pagination

### 7.2 Tags

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/tags` | List all tags with video counts |
| POST | `/api/tags` | Create a new tag |
| PUT | `/api/tags/{id}` | Update tag name/category |
| DELETE | `/api/tags/{id}` | Delete tag (removes associations) |
| GET | `/api/tags/{id}/rules` | List keyword rules for a tag |
| POST | `/api/tags/{id}/rules` | Add keyword rule |
| DELETE | `/api/tags/{id}/rules/{ruleId}` | Remove keyword rule |

### 7.3 Playlists

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/playlists` | List all playlists (local + YouTube) |
| POST | `/api/playlists` | Create a new topic playlist |
| PUT | `/api/playlists/{id}` | Update playlist metadata |
| POST | `/api/playlists/{id}/set-inbox` | Designate as inbox playlist |
| POST | `/api/playlists/consolidate` | Merge overlapping-topic playlists |
| GET | `/api/playlists/{id}/videos` | List videos in playlist |

### 7.4 Sync & Import

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/sync/trigger` | Trigger manual sync now |
| GET | `/api/sync/status` | Get current/last sync status |
| GET | `/api/sync/history` | List sync history |
| POST | `/api/import/takeout` | Upload Google Takeout CSV |
| GET | `/api/import/history` | List import batches |

### 7.5 Undo

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/undo/pending` | List undoable actions (within 7-day window) |
| POST | `/api/undo/{id}` | Undo a specific action |

### 7.6 Settings

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/settings` | Get app settings |
| PUT | `/api/settings` | Update settings (thresholds, schedule) |
| GET | `/api/settings/oauth-status` | Check YouTube OAuth connection status |
| POST | `/api/settings/oauth-connect` | Initiate OAuth flow |

## 8. Frontend Pages

| Page | Route | Description |
|------|-------|-------------|
| Dashboard | `/` | Overview stats, recent syncs, pending suggestions count |
| Videos | `/videos` | Searchable, filterable video list with tag chips |
| Video Detail | `/videos/[id]` | Full metadata, tag management, suggestions |
| Suggestions | `/suggestions` | Queue of videos needing tag review |
| Playlists | `/playlists` | Playlist list, consolidation UI |
| Tags | `/tags` | Tag management, keyword rules editor |
| Import | `/import` | Takeout CSV upload, import history |
| Undo | `/undo` | Recent actions with undo buttons |
| Settings | `/settings` | OAuth, sync schedule, thresholds |

## 9. Testing Strategy

### 9.1 C# Backend — Red-Green-Refactor TDD

**Framework:** xUnit + FluentAssertions + Moq + Testcontainers

**Unit Tests (Red-Green-Refactor cycle):**
- Write failing test first (Red)
- Write minimal code to pass (Green)
- Refactor while keeping tests green
- Target: all business logic, categorization engine, keyword matching, TF-IDF scoring

**Integration Tests (Testcontainers):**
- Spin up real PostgreSQL in container for each test class
- Test EF Core repositories, migrations, full-text search queries
- Test YouTube API client with recorded HTTP responses (WireMock or similar)
- Test Quartz job execution end-to-end

**Test Organization:**
```
tests/
├── PlaylistMiner.UnitTests/
│   ├── Categorization/
│   │   ├── KeywordMatcherTests.cs
│   │   ├── TfIdfScorerTests.cs
│   │   └── CategorizationPipelineTests.cs
│   ├── Services/
│   │   ├── VideoServiceTests.cs
│   │   ├── TagServiceTests.cs
│   │   ├── PlaylistServiceTests.cs
│   │   └── UndoServiceTests.cs
│   └── Import/
│       └── TakeoutParserTests.cs
├── PlaylistMiner.IntegrationTests/
│   ├── Repositories/
│   │   ├── VideoRepositoryTests.cs
│   │   ├── SearchTests.cs
│   │   └── TagRuleRepositoryTests.cs
│   ├── YouTube/
│   │   └── YouTubeClientTests.cs
│   └── Jobs/
│       └── SyncJobTests.cs
```

### 9.2 Frontend — Jest + Playwright

**Jest (Unit/Component Tests):**
- React Testing Library for component tests
- Mock API responses
- Test search, filtering, tag selection logic
- Test form validation

**Playwright (E2E Tests):**
- Full user flows: search → view → tag → accept
- Import flow: upload CSV → verify import
- Sync trigger → verify status updates
- Responsive layout checks

**Test Organization:**
```
src/
├── __tests__/           # Jest component tests alongside components
├── e2e/
│   ├── videos.spec.ts
│   ├── tags.spec.ts
│   ├── import.spec.ts
│   ├── sync.spec.ts
│   └── search.spec.ts
```

### 9.3 TDD Workflow

For every feature:
1. Write the failing test(s) defining expected behavior
2. Run tests — confirm they fail (Red)
3. Write the minimal implementation to pass
4. Run tests — confirm they pass (Green)
5. Refactor implementation while keeping tests green
6. Commit with message referencing the feature

## 10. Database Backup & Restore

### 10.1 Backup Strategy

PostgreSQL data is persisted to `./data/postgres/` via volume mount. Backups are critical — this is the single source of truth for all tags, rules, training data, and undo logs.

**Automated Daily Backup (Quartz Job):**

```
BackupJob : IJob
- Cron: "0 0 4 * * ?" (daily at 4 AM, after all other jobs complete)
- Runs: pg_dump via Npgsql to ./data/backups/
- Filename: playlistminer_YYYYMMDD_HHmmss.sql
- Retention: keep last 7 daily backups, delete older
- Logs: creates entry in sync_log with sync_type='backup'
```

**Manual Backup:**

```bash
# From host (Podman)
podman exec pm-db pg_dump -U pmuser playlistminer > backup_$(date +%Y%m%d).sql

# From host (Aspire dev — find container name first)
podman exec $(podman ps --filter name=postgres --format '{{.Names}}') \
  pg_dump -U pmuser playlistminer > backup_$(date +%Y%m%d).sql

# Compressed backup
podman exec pm-db pg_dump -U pmuser -Fc playlistminer > backup_$(date +%Y%m%d).dump
```

### 10.2 Restore

```bash
# From SQL file
podman exec -i pm-db psql -U pmuser playlistminer < backup_20260517.sql

# From compressed dump
podman exec -i pm-db pg_restore -U pmuser -d playlistminer backup_20260517.dump
```

### 10.3 API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/backup/trigger` | Trigger manual backup |
| GET | `/api/backup/history` | List available backups |
| GET | `/api/backup/download/{filename}` | Download a backup file |

### 10.4 Schema

```sql
-- Add to sync_log or create separate table
CREATE TABLE backup_log (
    id SERIAL PRIMARY KEY,
    filename VARCHAR(500) NOT NULL,
    size_bytes BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    status VARCHAR(50) NOT NULL,  -- completed, failed
    error TEXT
);
```

## 11. Phase 2 — Cloud Deployment (Future)

Documented for planning, not built in Phase 1.

- **Hosting:** Firebase (Cloud Run for containers or App Engine)
- **Database:** One-way push from PostgreSQL to Firestore for read-heavy web queries
- **Auth:** Firebase Auth for multi-user support
- **Scheduler:** Cloud Scheduler replacing Quartz.NET
- **CDN:** Firebase Hosting for Next.js static assets
- **Migration path:** Add Firestore sync service to C# worker, expose same REST API shape from Cloud Run
