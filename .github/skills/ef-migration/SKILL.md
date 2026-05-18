---
name: ef-migration
description: 'Create or modify an Entity Framework Core migration for PlaylistMiner database schema changes.'
---

# EF Core Migration

Create a new migration after modifying entities or DbContext.

## Steps

1. **Verify changes** — check what entities or relationships changed:
   ```bash
   git diff src/PlaylistMiner.Core/Models/
   git diff src/PlaylistMiner.Infrastructure/Data/
   ```

2. **Create migration** with a descriptive name:
   ```bash
   dotnet ef migrations add <DescriptiveName> \
     --project src/PlaylistMiner.Infrastructure \
     --startup-project src/PlaylistMiner.Api
   ```

3. **Review the generated migration** in `src/PlaylistMiner.Infrastructure/Migrations/`
   - Check for data loss (column drops, type changes)
   - Verify index creation (especially pg_trgm GIN indexes)
   - Ensure seed data is correct

4. **Apply migration locally**:
   ```bash
   dotnet ef database update \
     --project src/PlaylistMiner.Infrastructure \
     --startup-project src/PlaylistMiner.Api
   ```

5. **Test** — run integration tests to verify schema works:
   ```bash
   dotnet test tests/PlaylistMiner.IntegrationTests
   ```

## Naming Conventions

- `AddVideoTable`, `AddTagRules`, `CreateSearchIndexes`
- `AlterVideoAddStatus`, `DropLegacyColumns`
- Never: `Migration1`, `Update`, `Fix`

## Rollback

```bash
dotnet ef migrations remove   # Remove last unapplied migration
dotnet ef database update <PreviousMigrationName>  # Rollback applied
```
