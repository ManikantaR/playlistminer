---
name: ef-migration
description: Create or modify an EF Core migration for PlaylistMiner database schema changes, with review and verification steps.
---

# EF Core Migration

Create a new migration after modifying entities or DbContext.

## Steps

1. **Verify changes:**
   ```bash
   git diff src/PlaylistMiner.Core/Models/
   git diff src/PlaylistMiner.Infrastructure/Data/
   ```

2. **Create migration** (use descriptive name like `AddVideoStatusColumn`, `CreateSearchIndexes`):
   ```bash
   dotnet ef migrations add <Name> --project src/PlaylistMiner.Infrastructure --startup-project src/PlaylistMiner.Api
   ```

3. **Review generated migration** — check for data loss, verify indexes (especially pg_trgm GIN indexes)

4. **Apply locally:**
   ```bash
   dotnet ef database update --project src/PlaylistMiner.Infrastructure --startup-project src/PlaylistMiner.Api
   ```

5. **Run integration tests** to verify schema:
   ```bash
   dotnet test tests/PlaylistMiner.IntegrationTests
   ```

## Rollback
```bash
dotnet ef migrations remove   # Remove last unapplied
dotnet ef database update <PreviousMigration>  # Rollback applied
```
