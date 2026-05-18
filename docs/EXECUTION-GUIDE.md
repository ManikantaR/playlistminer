# PlaylistMiner — Execution Guide

Step-by-step commands to build PlaylistMiner from scratch using GitHub Copilot CLI, then commit to GitHub as a public repository.

---

## Phase 0: Initial Commit (specs only)

Run these commands in terminal from the project root:

```bash
# 1. Verify repo state
cd ~/repo/playlistminer
git status

# 2. Stage all spec and config files
git add -A

# 3. Commit the foundation
git commit -m "feat: add project specs, prompts, and AI tooling configuration

- Technical spec (docs/SPEC.md) with full schema, API, categorization pipeline
- Architecture decision records (docs/ARCHITECTURE.md)
- 9 sequential implementation prompts for Copilot CLI
- GitHub Actions CI/CD workflow (build, test, e2e)
- Copilot skills (tdd-dotnet, tdd-frontend, ef-migration, categorization-debug)
- Copilot agents (expert-dotnet-engineer, frontend-engineer)
- Claude Code skills (mirrored) + hooks (pre-bash safety, post-edit format)
- AGENTS.md at root and per-project for cross-tool agent instructions
- Public README with Mermaid architecture diagrams
- MIT License"

# 4. Create public repo on GitHub and push
gh repo create ManikantaR/playlistminer --public --source=. --remote=origin --description "Intelligent YouTube playlist organizer — sync, auto-categorize, and reorganize videos by topic"

# 5. Push
git push -u origin main
```

---

## Phase 1: Build with Copilot CLI

Run each prompt sequentially in GitHub Copilot CLI. After each step, verify and commit.

### Step 1: Project Scaffolding + Aspire

```bash
# Open Copilot CLI and paste the prompt from:
cat .github/prompts/01-project-setup.md

# Or use Copilot CLI directly:
gh copilot suggest "read .github/prompts/01-project-setup.md and implement it"
```

**In VS Code with Copilot Chat (alternative):**
- Open `.github/prompts/01-project-setup.md`
- Select all content in the code block
- Paste into Copilot Chat: "Implement this"

**Verify:**
```bash
dotnet build
dotnet test
dotnet run --project src/PlaylistMiner.AppHost  # Should start Aspire dashboard
# Ctrl+C to stop
```

**Commit:**
```bash
git add -A
git commit -m "feat: scaffold .NET 10 solution with Aspire orchestration

- PlaylistMiner.sln with Api, Worker, Core, Infrastructure, CLI projects
- AppHost + ServiceDefaults for Aspire dev orchestration
- xUnit test projects (Unit + Integration)
- Podman compose for production
- Directory.Build.props, global.json, .editorconfig"
git push
```

### Step 2: Database Schema

```bash
cat .github/prompts/02-containers-and-database.md
# Paste into Copilot CLI/Chat and implement
```

**Verify:**
```bash
dotnet build
dotnet test  # Entity tests should pass
dotnet run --project src/PlaylistMiner.AppHost
# In another terminal:
dotnet ef database update --project src/PlaylistMiner.Infrastructure --startup-project src/PlaylistMiner.Api
# Check seeded data:
# psql -h localhost -U pmuser -d playlistminer -c "SELECT name, category FROM tags ORDER BY category;"
```

**Commit:**
```bash
git add -A
git commit -m "feat: add database schema, EF Core entities, seed data

- Video, Tag, Playlist, TagRule, UndoLog, SyncLog entities
- PlaylistMinerDbContext with Fluent API configuration
- GIN index for pg_trgm fuzzy search
- Seeded 40+ tech tags with keyword rules
- Initial EF Core migration
- Entity validation unit tests"
git push
```

### Step 3: YouTube API Integration

```bash
cat .github/prompts/03-youtube-integration.md
# Implement via Copilot
```

**Verify:**
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "YouTube"
```

**Commit:**
```bash
git add -A
git commit -m "feat: add YouTube API client, OAuth, and sync service

- YouTubeApiClient with rate limiting and retry (Polly)
- OAuth desktop flow with encrypted refresh token storage
- SyncService for full and inbox-only sync
- Quota tracking and deferral on exhaustion
- Unit tests with mocked HTTP responses"
git push
```

### Step 4: Categorization Engine

```bash
cat .github/prompts/04-categorization-engine.md
# This is the biggest prompt — implement in sub-sections (04a through 04e)
```

**Verify:**
```bash
dotnet test --filter "Categorization"
dotnet test --filter "SelfLearning"
```

**Commit:**
```bash
git add -A
git commit -m "feat: add 3-layer categorization pipeline with self-learning

- KeywordMatcher: rule-based title/description matching
- TfIdfScorer: statistical text similarity via ML.NET
- OllamaCategorizer: optional LLM fallback (graceful degradation)
- CategorizationPipeline: orchestrates all 3 layers
- SelfLearningService: adjusts weights on accept/reject
- Full TDD test coverage for all components"
git push
```

### Step 5: API Layer

```bash
cat .github/prompts/05-api-layer.md
# Implement repositories, then services, then controllers
```

**Verify:**
```bash
dotnet test
dotnet run --project src/PlaylistMiner.Api
# Open http://localhost:5000/swagger — verify all endpoints documented
```

**Commit:**
```bash
git add -A
git commit -m "feat: add REST API with OpenAPI/Swagger

- Video, Tag, Playlist, Sync, Import, Undo, Settings controllers
- Repository layer with fuzzy search (pg_trgm + full-text)
- Service layer for tag management, playlist organization
- WebApplicationFactory integration tests
- Swagger/OpenAPI spec generation"
git push
```

### Step 6: Frontend

```bash
cat .github/prompts/06-frontend.md
# Start with setup (06a), then build pages one at a time
```

**Verify:**
```bash
cd src/web
npm install
npm run generate-api  # Generate TypeScript client from Swagger
npm test              # Jest component tests
npm run dev           # Open http://localhost:3000
# Test each page manually
npm run test:e2e      # Playwright E2E
cd ../..
```

**Commit:**
```bash
git add -A
git commit -m "feat: add Next.js frontend with all pages

- Dashboard, Videos, Suggestions, Playlists, Tags, Import, Undo, Settings
- Auto-generated TypeScript API client from OpenAPI
- TanStack Query hooks for all API calls
- Dark mode support with toggle
- Keyboard shortcuts on Suggestions page (j/k/y/n)
- Jest component tests + Playwright E2E tests"
git push
```

### Step 7: Scheduler & Background Jobs

```bash
cat .github/prompts/07-scheduler-and-jobs.md
# Implement jobs including the backup job (07c)
```

**Verify:**
```bash
dotnet test --filter "Job"
dotnet run --project src/PlaylistMiner.Worker
# Worker should log registered Quartz jobs
```

**Commit:**
```bash
git add -A
git commit -m "feat: add Quartz.NET scheduler with sync, categorization, backup jobs

- SyncJob: daily full playlist sync
- InboxProcessingJob: inbox sync + categorize every 6 hours
- CategorizationJob: batch process uncategorized videos
- UndoCleanupJob: remove expired undo entries
- BackupJob: daily pg_dump with 7-day retention
- Manual sync trigger via sync_requests table
- Backup API endpoints (trigger, history, download)"
git push
```

### Step 8: Search & Import

```bash
cat .github/prompts/08-search-and-import.md
```

**Verify:**
```bash
dotnet test --filter "Search"
dotnet test --filter "Import"
# Test CLI import:
dotnet run --project src/PlaylistMiner.CLI -- import-takeout --path /path/to/Takeout
```

**Commit:**
```bash
git add -A
git commit -m "feat: add fuzzy search (pg_trgm) and Google Takeout import

- PostgreSQL trigram + full-text search with ranking
- Handles special chars (C#, ASP.NET) safely
- TakeoutParser: CSV parsing with validation
- ImportService: batch hydration via YouTube API
- CLI command: import-takeout
- UI upload: drag-and-drop CSV on Import page
- Integration tests with real PostgreSQL"
git push
```

### Step 9: CI/CD Verification

```bash
cat .github/prompts/09-github-actions-setup.md
# Verify the GitHub Actions workflow works
```

**Verify:**
```bash
# Push to a branch and check GitHub Actions
git checkout -b test/ci-verification
git push -u origin test/ci-verification
# Go to GitHub → Actions tab → verify all 3 jobs pass
# Then merge and delete branch
gh pr create --title "Verify CI pipeline" --body "Test that all CI jobs pass"
```

---

## Full Build Verification Checklist

After all steps are complete, verify the full system:

```bash
# 1. Start everything via Aspire
dotnet run --project src/PlaylistMiner.AppHost

# 2. Run all backend tests
dotnet test

# 3. Run all frontend tests
cd src/web && npm test && npm run test:e2e && cd ../..

# 4. Manual smoke test
# Open http://localhost:3000
# - Connect YouTube account (Settings)
# - Designate inbox playlist (Playlists)
# - Trigger sync (Dashboard)
# - Review suggestions (Suggestions)
# - Search for a video (Videos)
# - Import a Takeout CSV (Import)
# - Trigger backup (Settings or API)
# - Check backup exists in ./data/backups/

# 5. Production test
podman-compose up -d
# Verify http://localhost:3000 works
podman-compose down
```

---

## Useful Copilot CLI Commands

```bash
# Ask Copilot to explain code
gh copilot explain "what does the CategorizationPipeline do?"

# Ask Copilot to suggest a command
gh copilot suggest "run only the integration tests for the search feature"

# Use a Copilot skill
# In VS Code Copilot Chat: /tdd-dotnet implement a new tag merge feature
# In VS Code Copilot Chat: /ef-migration add a backup_log table
# In VS Code Copilot Chat: @expert-dotnet-engineer review this PR
```

---

## Useful Claude Code Commands

```bash
# Use a Claude skill
# In Claude Code: /tdd-dotnet implement the backup download endpoint
# In Claude Code: /categorization-debug why wasn't this video tagged as React?
# In Claude Code: /ef-migration add the backup_log table

# Claude will auto-format C# and TypeScript files via hooks after edits
# Claude will block destructive bash commands via pre-bash hook
```
