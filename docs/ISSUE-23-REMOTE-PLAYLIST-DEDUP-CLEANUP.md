# Issue #23: Remote YouTube Playlist Dedup Cleanup

## Summary

Enforce the product invariant **end-to-end**, not only in the local database:

- A video should belong to **exactly one playlist**.
- If a video is found in multiple playlists on YouTube, PlaylistMiner must:
  1. detect the overlap,
  2. choose a single winning playlist deterministically,
  3. present a dry-run plan,
  4. optionally execute the required YouTube removals safely and incrementally.

This issue is the **remote** counterpart to the local single-membership enforcement already
implemented in sync/database code on **2026-06-29**.

## Why this exists

Current state after the local fix:

- Local DB now converges to one `playlist_videos` row per `video_id`.
- Sync no longer preserves multi-playlist membership locally.
- The Operations duplicate review surface now reflects the global invariant.

What is still missing:

- The remote source of truth, **YouTube**, may still contain the same video in multiple playlists.
- If we do nothing, every future sync must repeatedly reconcile the same remote overlaps.
- The user wants the invariant to hold remotely too, not just in the DB.

## Product rule

Treat any cross-playlist duplicate on YouTube as invalid.

- A video may appear in **one and only one** playlist.
- This applies across **all playlists**, not just managed playlists.
- The app is allowed to remove duplicate placements from non-winning playlists.

This is intentionally stricter than normal YouTube semantics.

## Required implementation shape

Do **not** jump straight to destructive remote deletes.

Build this as two layers:

1. **Dry-run planner**
   - Detect remote duplicates.
   - Decide the winner playlist for each duplicated video.
   - Return the planned removals with no mutations.

2. **Executor**
   - Apply the removals on YouTube.
   - Use quota-aware batching, idempotency, logging, and undo/audit surfaces where applicable.

## Winning-playlist policy

Unless the user changes policy later, use this deterministic ranking:

1. Non-inbox playlist beats inbox playlist.
2. If both are same inbox-ness, use stable deterministic fallback:
   - lower local `Playlist.Id` wins.

This policy matches the local reconciliation behavior already added in `SyncService`.

## Scope

### Backend

Add a remote cleanup plan flow, likely under organize/operations rather than sync:

- `POST /api/operations/duplicates/plan-remote-cleanup`
  - Computes a dry-run plan from current local state derived from sync.
  - No YouTube writes.

- `POST /api/operations/duplicates/execute-remote-cleanup`
  - Executes approved removals on YouTube.
  - Quota-aware, resumable, idempotent.

- Optional:
  - `GET /api/operations/duplicates/cleanup-status`
  - If you want a dedicated status/read model rather than relying only on pipeline runs.

### Frontend

Extend `/operations`:

- Show duplicate items grouped by video.
- Show the chosen winner playlist.
- Show which playlist memberships will be removed.
- Add “Plan Remote Cleanup” action.
- Add “Execute Remote Cleanup” action after plan review.
- Show live progress and quota consumption.

### Worker / execution model

Recommended:

- Reuse the existing pipeline run tracking / events infrastructure.
- Execute as a small incremental job, not a single giant mutation pass.
- Commit checkpoint progress after each remote removal.

## Safety requirements

This is destructive behavior against a real user account. The implementation must include:

- **Dry-run first**. No hidden automatic deletes in the first version.
- **Idempotency**:
  - Re-running execution should converge safely.
  - Already-removed playlist memberships should not break the run.
- **Quota-aware throttling**:
  - `playlistItems.delete` costs quota.
  - Stop early when quota is near the configured ceiling.
- **Checkpointing**:
  - Persist progress after each removal or small batch.
  - A crash must not force restarting from zero.
- **Clear audit trail**:
  - Every planned and executed removal should be visible in pipeline events or a dedicated log.
- **Graceful stale-data handling**:
  - If local state drifted since planning, executor must re-check before delete or tolerate no-op removals.

## Data / modeling guidance

No new tables are strictly required for v1, but one of these should exist:

Option A:
- Compute plan on demand and execute immediately from the supplied payload.

Option B:
- Persist a `RemoteCleanupPlan` / `RemoteCleanupItem` model.
- Better for auditability, resume, and explicit approval.

Recommendation:
- Start with **Option A** if speed matters.
- Use the existing `pipeline_runs` + `pipeline_events` for execution observability.

## YouTube API details

Important distinction:

- To remove a video from a playlist, you need the **playlist item id**, not just the video id.
- The system already stores `PlaylistVideo.PlaylistItemId`, which should be used.

Execution should:

1. For each duplicate video:
   - Identify all current playlist memberships.
   - Keep the winning playlist item.
   - Remove all other playlist items via `IYouTubeApiClient.RemoveVideoFromPlaylistAsync(...)`.

2. After successful remote removal:
   - Remove the local `PlaylistVideo` link for that losing playlist membership.

If a stored `PlaylistItemId` is missing or stale:

- Treat that item as unresolved.
- Emit a warning event.
- Skip it or re-sync before retry, depending on implementation choice.

## Recommended service shape

Add a dedicated service rather than overloading `SyncService`:

- `IRemoteDuplicateCleanupService`
  - `Task<List<RemoteDuplicateCleanupItemDto>> BuildPlanAsync(CancellationToken ct)`
  - `Task<RemoteDuplicateCleanupResultDto> ExecuteAsync(IEnumerable<RemoteDuplicateCleanupItemDto> plan, CancellationToken ct)`

Recommended DTO shape:

- `RemoteDuplicateCleanupItemDto`
  - `VideoId`
  - `YouTubeId`
  - `Title`
  - `WinnerPlaylistId`
  - `WinnerPlaylistName`
  - `LoserPlaylists[]`
    - `PlaylistId`
    - `PlaylistName`
    - `PlaylistItemId`

- `RemoteDuplicateCleanupResultDto`
  - `VideosExamined`
  - `RemovalsPlanned`
  - `RemovalsExecuted`
  - `RemovalsSkipped`
  - `DeferredCount`
  - `Errors[]`

## TDD requirements

Follow repo rules: failing tests first.

### Unit tests

Create tests for the planner:

- `Test_BuildPlan_WhenVideoExistsInTwoPlaylists_KeepsWinningPlaylistAndPlansOneRemoval`
- `Test_BuildPlan_WhenWinnerIsInboxAndNonInboxAlsoExists_PrefersNonInbox`
- `Test_BuildPlan_WhenNoDuplicates_ReturnsEmpty`
- `Test_BuildPlan_WhenPlaylistItemIdMissing_FlagsItemAsUnresolved`

Create tests for the executor:

- `Test_Execute_RemovesLoserPlaylistMembershipsOnYouTube`
- `Test_Execute_RemovesLocalLinksAfterSuccessfulRemoteDelete`
- `Test_Execute_WhenRemovalFails_LeavesLocalLinkAndLogsError`
- `Test_Execute_IsIdempotent_WhenLinkAlreadyGone`
- `Test_Execute_StopsWhenQuotaNearLimit_AndDefersRemaining`
- `Test_Execute_WritesPipelineEventsForEachRemoval`

### Integration tests

Controller/API:

- `Test_PlanRemoteCleanup_Returns200_WithPlannedRemovals`
- `Test_ExecuteRemoteCleanup_Returns202_AndStartsExecution`

If execution is synchronous for v1:

- `Test_ExecuteRemoteCleanup_Returns200_WithSummary`

### Frontend tests

Operations page:

- Renders remote cleanup plan preview.
- Shows winner playlist and removal targets.
- Allows triggering plan build.
- Allows triggering execution.
- Shows loading/error/success states.

## Rollout strategy

Implement in this order:

1. Planner only.
2. UI plan preview.
3. Executor behind manual action only.
4. Quota/progress/audit hardening.
5. Optional later: scheduled auto-cleanup.

## Non-goals for v1

Do not combine this issue with:

- topic reorganization,
- managed-playlist creation,
- auto-filing into topic playlists,
- broad organize executor work,
- Watch Later migration logic.

Keep this issue narrow: **remove duplicate remote playlist memberships so one video remains in one playlist**.

## Open questions

These are not blockers; default behavior may be assumed if user does not answer:

1. Should remote cleanup touch system/user-curated playlists that are not app-managed?
   - Current recommendation: **yes**, because the invariant is global.

2. Should execution require an explicit checkbox/confirmation in the UI?
   - Current recommendation: **yes**.

3. Should we add a dedicated undo path for remote duplicate removals?
   - Current recommendation: not required for v1 if full audit is present, but preferable later.

## Acceptance criteria

- Dry-run plan exists and shows every duplicate video with one winner + N removals.
- Executing the plan removes losing memberships on YouTube.
- Local DB reflects the same post-cleanup state.
- Execution is idempotent and quota-aware.
- Progress is visible in pipeline runs/events or equivalent operations UI.
- Tests cover planner, executor, API, and UI.

