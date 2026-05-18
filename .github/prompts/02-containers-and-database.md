# Prompt 02: Database Schema & EF Core Migrations

## Context
PlaylistMiner uses PostgreSQL 16 with pg_trgm extension. In development, .NET Aspire provisions the database automatically. In production, Podman runs it. This prompt creates the EF Core schema, seed data, and migrations. TDD with xUnit.

## Prompt 02a: Domain Entities

```
In PlaylistMiner.Core, create the following domain entities in Models/ folder.
Use .NET 10 / C# 13 features (primary constructors, required members where appropriate).

Video entity:
- Id (int, auto-increment), YouTubeId (string, unique, max 11), Title (string, max 500), Description (string, max 5000), ChannelName (string, max 200), ChannelId (string, max 50), ThumbnailUrl (string, max 500), Duration (TimeSpan), PublishedAt (DateTime), Status (enum: Active/Unavailable/Private/Deleted/Archived), CreatedAt (DateTime), UpdatedAt (DateTime), SyncedAt (DateTime)

Tag entity:
- Id (int), Name (string, max 100, unique), Slug (string, max 100, unique), Category (string, max 100, nullable), CreatedAt (DateTime)

VideoTag entity (join table):
- VideoId (FK), TagId (FK), Source (enum: Manual/RuleBased/TfIdf/Ollama/Suggested), Confidence (float, nullable), CreatedAt (DateTime)
- Composite PK: (VideoId, TagId, Source)

Playlist entity:
- Id (int), YouTubeId (string, max 50, unique), Name (string, max 200), Description (string, max 1000, nullable), IsInbox (bool, default false), IsManaged (bool, default false), Topic (string, max 200, nullable), CreatedAt, UpdatedAt, SyncedAt

PlaylistVideo entity (join with position):
- PlaylistId (FK), VideoId (FK), Position (int), AddedAt (DateTime)

TagRule entity:
- Id (int), TagId (FK), Keyword (string, max 200), Field (enum: Title/Description/Both), Weight (float, default 0.5), IsLearned (bool, default false), CreatedAt, UpdatedAt

UndoLog entity:
- Id (int), VideoId (FK), Action (string, max 50), SourcePlaylistId (FK, nullable), TargetPlaylistId (FK, nullable), PerformedAt (DateTime), ExpiresAt (DateTime), Undone (bool, default false)

SyncLog entity:
- Id (int), SyncType (string, max 50), StartedAt, CompletedAt (nullable), VideosProcessed (int), VideosCategorized (int), Errors (string, nullable), Status (string, max 50)

ImportBatch entity:
- Id (int), Source (string, max 50), Filename (string, max 500), TotalVideos (int), ImportedCount (int), FailedCount (int), ImportedAt (DateTime)
```

## Prompt 02b: EF Core DbContext & Configuration

```
In PlaylistMiner.Infrastructure/Data/:

Create PlaylistMinerDbContext with DbSets for all entities.

Use Fluent API in OnModelCreating (no data annotations on entities):
- Configure relationships, composite keys, indexes
- GIN index on Video.Title for pg_trgm: CREATE INDEX ix_videos_title_trigram ON videos USING GIN (title gin_trgm_ops)
- GiST index for full-text: CREATE INDEX ix_videos_title_fulltext ON videos USING GiST (to_tsvector('english', title))
- Unique indexes on Video.YouTubeId, Tag.Name, Tag.Slug, Playlist.YouTubeId

DbContext is registered via Aspire: builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");

Seed data: insert initial tech tags with categories and basic keyword rules.
Tags: C#, Python, JavaScript, TypeScript, Go, Rust, Java, SQL, React, Next.js, Angular, Vue, Tailwind CSS, HTML/CSS, ASP.NET Core, Node.js, Django, FastAPI, AWS, Azure, GCP, Firebase, Terraform, Kubernetes, Docker, Podman, CI/CD, GitHub Actions, SQL Server, PostgreSQL, MongoDB, Redis, EF Core, OAuth, JWT, Git, GitHub, GitHub Copilot, VS Code, Microservices, Clean Architecture, DDD, Machine Learning, LLMs, Prompt Engineering

Each tag gets 2-5 keyword rules seeded (e.g., tag "React" → keywords: "react", "reactjs", "react.js", "hooks", "jsx").

Create the initial EF Core migration.

Follow TDD: write unit tests first in PlaylistMiner.UnitTests verifying:
- Entity validation rules
- Enum value coverage
- Tag slug generation (Name "C#" → Slug "csharp", "ASP.NET Core" → "aspnet-core")
```

## Verification
- `dotnet run --project src/PlaylistMiner.AppHost` starts PostgreSQL via Aspire
- `dotnet ef database update` applies migration
- `dotnet test` passes all entity tests
- `SELECT * FROM tags` returns seeded categories
- Trigram index exists on videos table
