<p align="center">
  <img src="docs/assets/logo-placeholder.png" alt="PlaylistMiner" width="120" />
</p>

<h1 align="center">PlaylistMiner</h1>

<p align="center">
  <strong>Intelligent YouTube playlist organizer that syncs, categorizes, and reorganizes your videos by topic</strong>
</p>

<p align="center">
  <a href="#-the-problem">Problem</a> &bull;
  <a href="#-how-it-works">Solution</a> &bull;
  <a href="#%EF%B8%8F-architecture">Architecture</a> &bull;
  <a href="#-quick-start">Quick Start</a> &bull;
  <a href="#-development">Development</a> &bull;
  <a href="#-testing">Testing</a> &bull;
  <a href="#-roadmap">Roadmap</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Next.js-14-black?logo=next.js" alt="Next.js 14" />
  <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/Aspire-orchestrated-6C3FA0" alt=".NET Aspire" />
  <img src="https://img.shields.io/badge/TDD-xUnit%20%2B%20Playwright-green" alt="TDD" />
  <img src="https://img.shields.io/badge/AI-Ollama%20%2B%20Mistral-orange" alt="Ollama" />
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="License" />
</p>

---

## The Problem

If you learn tech through YouTube, you know this pain:

```
Your YouTube Playlists (right now)
├── "Watch Later"                    → 847 videos, mixed topics
├── "Good Stuff"                     → 213 videos, no theme
├── "React tutorials"                → has C#, AWS, and Docker videos too
├── "Dev Videos"                     → 500+ videos, completely unsearchable
├── "Cloud"                          → mix of AWS, Azure, GCP, Terraform
├── "Save for later"                 → you saved them. you never went back.
└── "Untitled Playlist"              → why does this exist
```

**The core problems:**

1. **No multi-tagging.** YouTube allows one playlist per video. A video about "Deploying React to AWS with Docker" belongs in 4 categories — but YouTube forces you to pick one.

2. **No search across playlists.** Want to find that OAuth tutorial you saved 6 months ago? Good luck scrolling through 800 videos in "Watch Later."

3. **No auto-organization.** Every video you save goes into a pile. Manual sorting is tedious, so you stop doing it, and the pile grows.

4. **Dead videos vanish silently.** Creators delete or private their videos. You never know what you lost.

## How It Works

PlaylistMiner solves this with a **sync-categorize-organize** pipeline:

```mermaid
graph LR
    A[📺 YouTube Playlists] -->|Sync via API| B[🗄️ Local Database]
    B -->|Analyze| C[🤖 Categorization Engine]
    C -->|Suggest Tags| D[👤 Review Queue]
    D -->|Accept/Reject| E[📚 Topic Playlists]
    E -->|Write Back| A
    D -->|Learn| C

    style A fill:#ff0000,color:#fff
    style B fill:#4169E1,color:#fff
    style C fill:#ff6b35,color:#fff
    style D fill:#2ecc71,color:#fff
    style E fill:#9b59b6,color:#fff
```

**After PlaylistMiner:**

```
Your YouTube Playlists (organized)
├── 📥 Inbox                         → 12 new videos, pending review
├── 🏷️ React                        → 47 videos (also tagged: Frontend, JavaScript)
├── 🏷️ AWS                          → 38 videos (also tagged: Cloud, DevOps)
├── 🏷️ C#                           → 62 videos (also tagged: .NET, ASP.NET Core)
├── 🏷️ Docker & Kubernetes          → 29 videos (also tagged: DevOps, Cloud)
├── 🏷️ OAuth & Security             → 15 videos (also tagged: API, Backend)
├── 🏷️ System Design                → 22 videos (also tagged: Architecture)
└── 📦 Archived                      → 8 unavailable videos (metadata preserved)
```

### The Self-Learning Loop

PlaylistMiner gets smarter with every decision you make:

```mermaid
graph TD
    A[New Video Arrives] --> B{Keyword Match?}
    B -->|Yes, high confidence| C[Suggest Tags]
    B -->|Partial match| D[TF-IDF Analysis]
    D -->|Score > threshold| C
    D -->|No match| E{Ollama Available?}
    E -->|Yes| F[LLM Classification]
    E -->|No| G[Queue for Manual Review]
    F --> C
    C --> H[User Reviews]
    H -->|Accept| I[✅ Strengthen Rules +0.1 weight]
    H -->|Reject| J[❌ Weaken Rules -0.1 weight]
    I --> K[Better Future Suggestions]
    J --> K

    style A fill:#3498db,color:#fff
    style C fill:#f39c12,color:#fff
    style H fill:#2ecc71,color:#fff
    style K fill:#9b59b6,color:#fff
```

### Feature Highlights

| Feature | Description |
|---------|-------------|
| **Multi-tagging** | Each video gets multiple tags (React + Frontend + TypeScript). No more single-playlist limitation. |
| **Fuzzy search** | Find "that OAuth video" by typing "oarth" — PostgreSQL trigram matching handles typos. |
| **Smart categorization** | 3-layer pipeline: keyword rules → TF-IDF text analysis → optional Ollama/Mistral LLM. |
| **Self-learning** | Every accept/reject decision trains the engine. Keyword rules grow stronger or weaker. |
| **7-day undo** | Miscategorized? Undo any video move within 7 days. |
| **Dead video detection** | Deleted/privated videos auto-archived with metadata preserved. |
| **Watch Later import** | Google Takeout CSV import via CLI or drag-and-drop UI. |
| **Playlist consolidation** | Merge overlapping playlists into clean topic-based ones. |

---

## Architecture

### System Overview

```mermaid
C4Context
    title PlaylistMiner — System Context

    Person(user, "Developer", "Manages tech YouTube playlists")

    System_Boundary(pm, "PlaylistMiner") {
        Container(web, "Frontend", "Next.js 14, TypeScript, Tailwind", "Search, tag, review suggestions")
        Container(api, "API", "ASP.NET Core, .NET 10", "REST API, OpenAPI/Swagger")
        Container(worker, "Worker", "Quartz.NET, .NET 10", "Sync, categorize, organize")
        ContainerDb(db, "Database", "PostgreSQL 16", "Videos, tags, rules, playlists")
        Container(ollama, "Ollama", "Mistral LLM", "Optional AI categorization")
    }

    System_Ext(youtube, "YouTube Data API v3", "Playlist sync, video metadata")
    System_Ext(takeout, "Google Takeout", "Watch Later CSV export")

    Rel(user, web, "Uses")
    Rel(web, api, "REST/JSON")
    Rel(api, db, "EF Core")
    Rel(worker, db, "EF Core")
    Rel(worker, youtube, "Sync & organize")
    Rel(worker, ollama, "Classify (optional)")
    Rel(user, takeout, "Export CSV")
    Rel(api, takeout, "Import CSV")
```

### Container Architecture

```mermaid
graph TB
    subgraph Development ["🛠️ Development (Aspire)"]
        aspire[".NET Aspire AppHost<br/>Orchestrates all services"]
        aspire --> api_dev["API :5000"]
        aspire --> worker_dev["Worker"]
        aspire --> web_dev["Frontend :3000"]
        aspire --> db_dev["PostgreSQL :5432"]
        aspire --> ollama_dev["Ollama :11434<br/>(optional)"]
    end

    subgraph Production ["🚀 Production (Podman)"]
        direction TB
        pm_web["pm-web<br/>Next.js :3000"] --> pm_api
        pm_api["pm-api<br/>ASP.NET Core :5000"] --> pm_db
        pm_worker["pm-worker<br/>Quartz.NET"] --> pm_db
        pm_db["pm-db<br/>PostgreSQL :5432<br/>📁 ./data/postgres"]
        pm_worker -.-> pm_ollama["pm-ollama<br/>Ollama :11434<br/>📁 ./data/ollama"]
    end

    style aspire fill:#6C3FA0,color:#fff
    style pm_db fill:#4169E1,color:#fff
    style pm_ollama fill:#ff6b35,color:#fff
```

### Tech Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend** | Next.js 14 + TypeScript + Tailwind CSS | Search, tag management, suggestion review |
| **API** | C# / .NET 10 / ASP.NET Core Web API | REST endpoints, OpenAPI spec generation |
| **Worker** | C# / .NET 10 / Quartz.NET | Scheduled sync, categorization, cleanup |
| **Orchestration** | .NET Aspire (dev) / Podman (prod) | Service discovery, health checks, telemetry |
| **Database** | PostgreSQL 16 + pg_trgm | Full-text search, fuzzy matching, data store |
| **ORM** | Entity Framework Core 10 | Code-first migrations, LINQ queries |
| **AI** | Ollama + Mistral (optional) | LLM fallback for unclassified videos |
| **API Client** | Auto-generated from OpenAPI | Type-safe frontend-backend communication |

### Database Schema

```mermaid
erDiagram
    VIDEO ||--o{ VIDEO_TAG : has
    VIDEO ||--o{ PLAYLIST_VIDEO : "belongs to"
    VIDEO ||--o{ UNDO_LOG : "tracked by"
    TAG ||--o{ VIDEO_TAG : applied
    TAG ||--o{ TAG_RULE : "matched by"
    PLAYLIST ||--o{ PLAYLIST_VIDEO : contains

    VIDEO {
        int id PK
        string youtube_id UK
        string title
        string description
        string channel_name
        enum status "active|archived|private|deleted"
        datetime synced_at
    }

    TAG {
        int id PK
        string name UK
        string slug UK
        string category
    }

    VIDEO_TAG {
        int video_id FK
        int tag_id FK
        enum source "manual|rule|tfidf|ollama|suggested"
        float confidence
    }

    TAG_RULE {
        int id PK
        int tag_id FK
        string keyword
        enum field "title|description|both"
        float weight
        bool is_learned
    }

    PLAYLIST {
        int id PK
        string youtube_id UK
        string name
        bool is_inbox
        bool is_managed
    }

    PLAYLIST_VIDEO {
        int playlist_id FK
        int video_id FK
        int position
    }

    UNDO_LOG {
        int id PK
        int video_id FK
        string action
        datetime expires_at
        bool undone
    }
```

### Categorization Pipeline

```mermaid
graph TD
    A["🎬 New Video<br/>Title + Description"] --> B["Layer 1: Keyword Matching<br/>⚡ Fast, rule-based"]
    B --> C{Tags found?}
    C -->|"✅ High confidence (>0.7)"| D["Suggest Tags"]
    C -->|"⚠️ Low/no match"| E["Layer 2: TF-IDF Scoring<br/>📊 Statistical similarity"]
    E --> F{Tags found?}
    F -->|"✅ Score > 0.5"| D
    F -->|"❌ No match"| G{Ollama running?}
    G -->|Yes| H["Layer 3: Ollama/Mistral<br/>🧠 LLM inference"]
    G -->|No| I["Queue: Manual Review"]
    H --> D
    D --> J["👤 User Review Queue<br/>Accept ✓ or Reject ✗"]
    J -->|Accept| K["Move to topic playlist<br/>+ Strengthen keyword rules"]
    J -->|Reject| L["Weaken keyword rules<br/>Remove if weight = 0"]

    style B fill:#3498db,color:#fff
    style E fill:#f39c12,color:#fff
    style H fill:#e74c3c,color:#fff
    style J fill:#2ecc71,color:#fff
```

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)
- [Podman](https://podman.io/) (production) or .NET Aspire (development)
- [YouTube Data API v3](https://console.cloud.google.com/apis/library/youtube.googleapis.com) credentials

### 1. Clone and Configure

```bash
git clone https://github.com/ManikantaR/playlistminer.git
cd playlistminer
cp .env.example .env
```

Edit `.env`:

```env
POSTGRES_PASSWORD=your_secure_password
YOUTUBE_API_KEY=your_api_key
YOUTUBE_CLIENT_ID=your_oauth_client_id
YOUTUBE_CLIENT_SECRET=your_oauth_client_secret
```

### 2. Get YouTube API Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a project, enable **YouTube Data API v3**
3. Create an **API Key** (public metadata) and **OAuth 2.0 Client ID** (Desktop app)
4. Copy credentials to `.env`

### 3. Launch with Aspire (Development)

```bash
# Start everything: PostgreSQL, API, Worker, Frontend
dotnet run --project src/PlaylistMiner.AppHost

# With Ollama AI categorization
dotnet run --project src/PlaylistMiner.AppHost -- --EnableOllama=true
```

Aspire provisions PostgreSQL, injects connection strings, and starts all services. Dashboard URL appears in terminal output.

### 4. Launch with Podman (Production)

```bash
podman-compose up -d                      # Core services
podman-compose --profile ai up -d         # + Ollama
```

### 5. Connect and Organize

1. Open http://localhost:3000
2. **Settings** > **Connect YouTube** (OAuth flow)
3. **Playlists** > designate your **Inbox** playlist
4. **Dashboard** > **Sync Now**
5. **Suggestions** > review and accept/reject tags

### 6. Import Watch Later (Optional)

YouTube API blocks Watch Later access. Use [Google Takeout](https://takeout.google.com/) to export, then:

```bash
# CLI bulk import
dotnet run --project src/PlaylistMiner.CLI -- import-takeout --path /path/to/Takeout

# Or drag-and-drop CSV in the Import page
```

---

## Development

### Project Structure

```
playlistminer/
├── .github/
│   ├── copilot-instructions.md          # Copilot coding conventions
│   ├── prompts/                         # Implementation prompts (01-08)
│   ├── skills/                          # Copilot agent skills
│   ├── agents/                          # Copilot custom agents
│   ├── workflows/                       # GitHub Actions CI/CD
│   └── ISSUE_TEMPLATE/
├── .claude/
│   ├── CLAUDE.md                        # Claude Code context
│   ├── settings.json                    # Hooks configuration
│   ├── hooks/                           # Hook scripts
│   └── skills/                          # Claude Code skills
├── AGENTS.md                             # Shared agent instructions
├── docs/
│   ├── SPEC.md                          # Technical specification
│   └── ARCHITECTURE.md                  # Architecture decisions (ADRs)
├── src/
│   ├── PlaylistMiner.AppHost/           # .NET Aspire orchestrator
│   ├── PlaylistMiner.ServiceDefaults/   # Aspire shared config
│   ├── PlaylistMiner.Api/               # REST API (CLAUDE.md + AGENTS.md)
│   ├── PlaylistMiner.Worker/            # Background jobs (CLAUDE.md + AGENTS.md)
│   ├── PlaylistMiner.Core/              # Domain models, interfaces, DTOs
│   ├── PlaylistMiner.Infrastructure/    # EF Core, YouTube, categorization
│   ├── PlaylistMiner.CLI/               # Import CLI
│   └── web/                             # Next.js frontend (CLAUDE.md + AGENTS.md)
├── tests/
│   ├── PlaylistMiner.UnitTests/         # xUnit + FluentAssertions
│   └── PlaylistMiner.IntegrationTests/  # Testcontainers + PostgreSQL
└── podman-compose.yml
```

### Running Services Individually

```bash
# Backend API (needs PostgreSQL)
dotnet run --project src/PlaylistMiner.Api

# Worker (needs PostgreSQL)
dotnet run --project src/PlaylistMiner.Worker

# Frontend
cd src/web && npm install && npm run dev

# Generate TypeScript API client from Swagger
cd src/web && npm run generate-api
```

### AI-Assisted Development

This project is configured for both **GitHub Copilot CLI** and **Claude Code**:

| Tool | Config Location | Purpose |
|------|----------------|---------|
| Copilot CLI | `.github/copilot-instructions.md` | Coding conventions, architecture rules |
| Copilot Skills | `.github/skills/` | Specialized tasks (migrations, testing, components) |
| Copilot Prompts | `.github/prompts/` | Step-by-step implementation guides (01-08) |
| Copilot Agents | `.github/agents/` | Expert personas (.NET engineer, frontend) |
| Claude Code | `.claude/CLAUDE.md` | Project context, brainstorming |
| Claude Skills | `.claude/skills/` | Custom workflows (TDD, categorization, search) |
| Claude Hooks | `.claude/settings.json` | Auto-format, build validation, safety guards |
| Shared | `AGENTS.md` (root + per-project) | Agent instructions both tools read |

---

## Testing

This project follows **Red-Green-Refactor TDD**. Every feature starts with a failing test.

```mermaid
graph LR
    A["🔴 Red<br/>Write failing test"] --> B["🟢 Green<br/>Write minimal code"]
    B --> C["🔵 Refactor<br/>Clean up"]
    C --> A

    style A fill:#e74c3c,color:#fff
    style B fill:#2ecc71,color:#fff
    style C fill:#3498db,color:#fff
```

### C# Backend

| Type | Framework | Command |
|------|-----------|---------|
| Unit | xUnit + FluentAssertions + Moq | `dotnet test --filter "Category=Unit"` |
| Integration | xUnit + Testcontainers (real PostgreSQL) | `dotnet test --filter "Category=Integration"` |

### Next.js Frontend

| Type | Framework | Command |
|------|-----------|---------|
| Component | Jest + React Testing Library | `cd src/web && npm test` |
| E2E | Playwright | `cd src/web && npm run test:e2e` |

### CI Pipeline

GitHub Actions runs on every push and PR:

```mermaid
graph LR
    A[Push/PR] --> B[Build .NET]
    A --> C[Build Next.js]
    B --> D[Unit Tests]
    B --> E[Integration Tests<br/>PostgreSQL container]
    C --> F[Jest Tests]
    C --> G[Playwright E2E<br/>Against API + DB]
    D --> H[✅ Merge]
    E --> H
    F --> H
    G --> H
```

---

## Backup & Restore

```bash
# Backup
podman exec pm-db pg_dump -U pmuser playlistminer > backup_$(date +%Y%m%d).sql

# Restore
podman exec -i pm-db psql -U pmuser playlistminer < backup.sql
```

---

## Roadmap

### Phase 1 — Local Self-Hosted (Current)

- [x] Project specification and architecture
- [ ] .NET 10 solution with Aspire orchestration
- [ ] PostgreSQL schema with EF Core migrations
- [ ] YouTube API integration (sync + organize)
- [ ] 3-layer categorization engine
- [ ] REST API with OpenAPI/Swagger
- [ ] Next.js frontend with search and tag management
- [ ] Quartz.NET scheduler
- [ ] Google Takeout import (CLI + UI)
- [ ] Fuzzy search with pg_trgm

### Phase 2 — Cloud Deployment (Future)

- [ ] Firebase hosting (Cloud Run for containers)
- [ ] Firestore one-way push for read-heavy queries
- [ ] Firebase Auth for multi-user support
- [ ] Cloud Scheduler replacing Quartz.NET
- [ ] Firebase Hosting for Next.js static assets

---

## API Documentation

Swagger UI available at http://localhost:5000/swagger when the API is running.

---

## Contributing

This is currently a personal project. If you find it useful and want to contribute:

1. Fork the repo
2. Create a feature branch
3. Follow TDD — write failing tests first
4. Submit a PR with test evidence

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

<p align="center">
  Built with <a href="https://dotnet.microsoft.com/">.NET 10</a> + <a href="https://nextjs.org/">Next.js</a> + <a href="https://www.postgresql.org/">PostgreSQL</a>
  <br/>
  AI-assisted development with <a href="https://github.com/features/copilot">GitHub Copilot</a> + <a href="https://claude.ai/code">Claude Code</a>
</p>
