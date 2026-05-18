# Prompt 05: REST API Layer

## Context
PlaylistMiner exposes a REST API via ASP.NET Core Web API with OpenAPI/Swagger. The frontend will consume auto-generated TypeScript clients. TDD with xUnit — controllers tested via WebApplicationFactory.

## Prompt 05a: Repository Layer (TDD)

```
TDD — write integration tests first using Testcontainers:

VideoRepositoryTests.cs (IntegrationTests project):
- Test_GetAll_WithPagination_ReturnsPage
- Test_GetAll_WithTagFilter_FiltersCorrectly
- Test_GetAll_WithMultipleTags_UsesAndLogic
- Test_GetAll_WithStatusFilter_FiltersCorrectly
- Test_Search_FuzzyMatch_FindsSimilarTitles (pg_trgm)
- Test_Search_FullText_RanksRelevantly
- Test_GetById_IncludesTags_ReturnsFull
- Test_Upsert_NewVideo_Inserts
- Test_Upsert_ExistingVideo_Updates

TagRepositoryTests.cs:
- Test_GetAll_IncludesVideoCounts
- Test_Create_GeneratesSlug
- Test_Create_DuplicateName_Throws
- Test_Delete_RemovesAssociations

PlaylistRepositoryTests.cs:
- Test_GetAll_IncludesVideoCounts
- Test_SetInbox_ClearsPreviousInbox
- Test_GetInboxPlaylist_ReturnsDesignated

Then implement repository interfaces in Core and implementations in Infrastructure:

IVideoRepository:
- Task<PagedResult<VideoDto>> GetAllAsync(VideoFilter filter, CancellationToken ct)
- Task<VideoDetailDto?> GetByIdAsync(int id, CancellationToken ct)
- Task<Video> UpsertAsync(Video video, CancellationToken ct)
- Task UpdateStatusAsync(int id, VideoStatus status, CancellationToken ct)

ITagRepository:
- Task<List<TagWithCountDto>> GetAllAsync(CancellationToken ct)
- Task<Tag> CreateAsync(Tag tag, CancellationToken ct)
- Task<Tag> UpdateAsync(Tag tag, CancellationToken ct)
- Task DeleteAsync(int id, CancellationToken ct)
- Task<List<TagRule>> GetRulesAsync(int tagId, CancellationToken ct)
- Task<TagRule> AddRuleAsync(TagRule rule, CancellationToken ct)
- Task DeleteRuleAsync(int ruleId, CancellationToken ct)

IPlaylistRepository:
- Task<List<PlaylistDto>> GetAllAsync(CancellationToken ct)
- Task<Playlist> CreateAsync(Playlist playlist, CancellationToken ct)
- Task SetInboxAsync(int playlistId, CancellationToken ct)
- Task<Playlist?> GetInboxAsync(CancellationToken ct)

IUndoRepository:
- Task<List<UndoLogDto>> GetPendingAsync(CancellationToken ct)
- Task<UndoLog> CreateAsync(UndoLog entry, CancellationToken ct)
- Task MarkUndoneAsync(int id, CancellationToken ct)
- Task CleanupExpiredAsync(CancellationToken ct)

VideoFilter record: Search, Tags[], Status, PlaylistId, Page, PageSize
PagedResult<T> record: Items, TotalCount, Page, PageSize, TotalPages

For fuzzy search, use pg_trgm similarity() function with a minimum threshold of 0.3.
For full-text search, use to_tsvector('english', title) @@ plainto_tsquery('english', search).
Combine both: full-text for relevance ranking, trigram for typo tolerance.
```

## Prompt 05b: API Controllers (TDD)

```
TDD — write tests first using WebApplicationFactory:

VideosControllerTests.cs:
- Test_GetVideos_Returns200_WithPaginatedList
- Test_GetVideos_WithSearch_ReturnsFuzzyMatches
- Test_GetVideos_WithTags_ReturnsFiltered
- Test_GetVideo_Returns200_WithDetails
- Test_GetVideo_NotFound_Returns404
- Test_PatchTags_Returns200_UpdatesTags
- Test_AcceptSuggestions_Returns200_CallsSelfLearning
- Test_RejectSuggestions_Returns200_CallsSelfLearning
- Test_GetSuggestions_Returns200_WithPendingVideos

TagsControllerTests.cs:
- Test_GetTags_Returns200_WithCounts
- Test_CreateTag_Returns201_WithCreatedTag
- Test_CreateTag_DuplicateName_Returns409
- Test_DeleteTag_Returns204
- Test_GetRules_Returns200
- Test_AddRule_Returns201
- Test_DeleteRule_Returns204

PlaylistsControllerTests.cs:
- Test_GetPlaylists_Returns200
- Test_CreatePlaylist_Returns201
- Test_SetInbox_Returns200_ClearsPrevious
- Test_Consolidate_Returns200_WithResult

SyncControllerTests.cs:
- Test_TriggerSync_Returns202_Accepted
- Test_GetStatus_Returns200_WithCurrentStatus
- Test_GetHistory_Returns200_WithSyncLogs

ImportControllerTests.cs:
- Test_UploadTakeout_Returns200_WithImportResult
- Test_UploadTakeout_InvalidCsv_Returns400
- Test_GetHistory_Returns200_WithBatches

UndoControllerTests.cs:
- Test_GetPending_Returns200
- Test_Undo_Returns200_ReversesAction
- Test_Undo_Expired_Returns410

Then implement controllers in PlaylistMiner.Api/Controllers/:

Each controller:
- Uses constructor injection for services
- Returns proper HTTP status codes (200, 201, 204, 400, 404, 409, 410)
- Uses [ApiController] attribute for automatic model validation
- Uses [ProducesResponseType] attributes for OpenAPI documentation
- Accepts CancellationToken on all async methods
- Sync trigger returns 202 Accepted (fire-and-forget via background channel)

Configure in Program.cs:
- AddSwaggerGen with XML comments
- AddCors (allow localhost:3000)
- AddHealthChecks (PostgreSQL)
- AddProblemDetails for error responses
- Map /health endpoint
```

## Prompt 05c: Service Layer (TDD)

```
TDD — tests first for service orchestration:

VideoServiceTests.cs:
- Test_AcceptTag_CallsSelfLearning_PromotesToManual
- Test_RejectTag_CallsSelfLearning_RemovesSuggestion
- Test_AddTag_CreatesManualVideoTag
- Test_RemoveTag_DeletesVideoTag

PlaylistOrganizer tests:
- Test_MoveVideo_AddsToTarget_RemovesFromSource
- Test_MoveVideo_CreatesUndoLog
- Test_UndoMove_ReversesAction_WithinWindow
- Test_UndoMove_Expired_ThrowsGoneException
- Test_ConsolidatePlaylists_MergesOverlappingTopics

Then implement service interfaces in Core and implementations in Infrastructure:
- IVideoService: tag management, suggestion handling
- IPlaylistOrganizer: move videos, undo, consolidate
- IImportService: parse Takeout CSV, hydrate, import
```

## Verification
- All tests pass (Red-Green-Refactor)
- Swagger UI accessible at localhost:5000/swagger
- All endpoints documented with request/response schemas
- Health check returns healthy when DB is up
