# Prompt 08: Search & Google Takeout Import

## Context
PlaylistMiner uses PostgreSQL pg_trgm for fuzzy title search and full-text search for description relevance. Google Takeout CSV import is supported via both CLI and UI upload. TDD approach.

## Prompt 08a: Search Implementation (TDD)

```
TDD — integration tests first (using Testcontainers with real PostgreSQL):

SearchTests.cs:
- Test_FuzzySearch_FindsPartialMatch ("reac" → "React Tutorial")
- Test_FuzzySearch_HandlesMisspelling ("Rect" → "React Tutorial")
- Test_FuzzySearch_RanksExactMatchHigher
- Test_FullTextSearch_FindsDescriptionMatch
- Test_FullTextSearch_RanksRelevantly
- Test_CombinedSearch_FuzzyPlusFullText_MergesResults
- Test_Search_WithTagFilter_CombinesCorrectly
- Test_Search_EmptyQuery_ReturnsAll
- Test_Search_NoResults_ReturnsEmpty
- Test_Search_SpecialCharacters_HandledSafely ("C#", "ASP.NET")

Then implement in Infrastructure/Repositories/VideoRepository.cs:

Search query construction:
1. Enable pg_trgm extension in migration: CREATE EXTENSION IF NOT EXISTS pg_trgm
2. Create GIN index: CREATE INDEX ix_videos_title_trigram ON videos USING GIN (title gin_trgm_ops)
3. Create GiST index: CREATE INDEX ix_videos_title_fulltext ON videos USING GiST (to_tsvector('english', title))

Search algorithm:
- If search term provided:
  a. Trigram similarity: WHERE similarity(title, @search) > 0.3
  b. Full-text: WHERE to_tsvector('english', title || ' ' || description) @@ plainto_tsquery('english', @search)
  c. Combine with: ORDER BY similarity DESC, ts_rank DESC
- If tags provided: JOIN video_tags WHERE tag_id IN @tagIds GROUP BY video_id HAVING COUNT(DISTINCT tag_id) = @tagCount
- Combine search + tags with AND

Use raw SQL via EF Core's FromSqlRaw for the search query (EF Core doesn't natively support pg_trgm).
Parameterize all inputs to prevent SQL injection.
```

## Prompt 08b: Google Takeout Import (TDD)

```
TDD — tests first:

TakeoutParserTests.cs (Unit):
- Test_Parse_ValidCsv_ReturnsVideoIds
- Test_Parse_HandlesHeaderRow
- Test_Parse_SkipsEmptyLines
- Test_Parse_HandlesQuotedFields
- Test_Parse_InvalidFormat_ThrowsImportException
- Test_Parse_EmptyFile_ThrowsImportException

ImportServiceTests.cs (Unit):
- Test_Import_HydratesMetadataViaYouTubeApi
- Test_Import_BatchesHydrationBy50
- Test_Import_SkipsDuplicateVideos
- Test_Import_RecordsImportBatch
- Test_Import_HandlesPartialFailure_ContinuesRemaining
- Test_Import_UnavailableVideos_MarkedAsUnavailable

Then implement:

TakeoutParser in Core/Import/:
- Parse CSV: columns are "Video Id,Time Added" (Google Takeout format)
- Return List<TakeoutEntry> with VideoId and AddedAt
- Validate format, throw ImportException on invalid

IImportService in Core:
- Task<ImportResult> ImportTakeoutAsync(Stream csvStream, string filename, CancellationToken ct)
- Task<ImportResult> ImportTakeoutFromPathAsync(string folderPath, CancellationToken ct) // CLI

ImportService in Infrastructure:
1. Parse CSV via TakeoutParser
2. Filter out videos already in DB (by YouTubeId)
3. Batch remaining video IDs (groups of 50)
4. Hydrate metadata via IYouTubeApiClient.GetVideoMetadataAsync()
5. Insert into videos table
6. Create ImportBatch record
7. Queue new videos for categorization
8. Return ImportResult(Total, Imported, Skipped, Failed)

CLI command in PlaylistMiner.CLI:
- Command: dotnet run --project src/PlaylistMiner.CLI -- import-takeout --path /path/to/Takeout
- Finds "Watch later-videos.csv" in the Takeout folder structure
- Calls ImportService.ImportTakeoutFromPathAsync()
- Outputs progress and summary to console

API endpoint (already defined in prompt 05):
- POST /api/import/takeout accepts multipart/form-data with CSV file
- Calls ImportService.ImportTakeoutAsync()
```

## Verification
- Search tests pass with real PostgreSQL (Testcontainers)
- Fuzzy search finds "React" when searching "reac" or "Rect"
- Special characters like "C#" don't break search
- Takeout CSV import via CLI processes a real Takeout export
- Takeout CSV upload via API processes uploaded file
- Duplicate videos are skipped, not re-imported
- Import history shows in UI
