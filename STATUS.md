# PlaylistMiner — Live Status

> Single source of truth for "where are we right now". Update this whenever system state
> changes (deploy, sync run, new bug). Pairs with [TASK.md](TASK.md) (the backlog).
> Goal: any session — human or agent — can resume cold from this file.

_Last updated: 2026-07-05_

## Deployment
| Component | Where | State |
|-----------|-------|-------|
| pm-db (Postgres 16) | NAS `10.140.1.95`, Docker | ✅ running |
| pm-api (.NET 10) | NAS, Docker, behind Traefik | ✅ healthy (`/api/health`) |
| pm-worker (Quartz) | NAS, Docker | ✅ running, heartbeat live |
| pm-web (Next.js 15) | NAS, Docker | ✅ running |
| Ollama | Mac (M1, not NAS) | reachability-gated |

- URL (LAN only, AdGuard DNS): https://playlistminer.home.manikantar.com
- Deploy: `./deploy-to-nas.sh` (tar → scp → build on NAS → `up -d --force-recreate`)
- OAuth: ✅ connected (green). Refresh token persisted. Consent screen **In production** (tokens don't expire).

## Data (as of 2026-06-29, post-fix verified)
- Playlists: **406** synced
- Videos: **11,620** · playlist→video links: **12,975**
- Last full sync: ✅ **completed** end-to-end via the new incremental path (406/406)
- Categorization runs: completing (~6,900 videos/run)

## What works
- Full + inbox sync pull real data from YouTube.
- Local playlist membership now converges to **one playlist per video**; DB enforces unique
  `playlist_videos.video_id`.
- Categorization pipeline (keyword + TF-IDF; Ollama when Mac reachable) produces tag **suggestions**.
- Operations UI (`/operations`) + dashboard card show live pipeline progress (issues #16/#17).
- Worker heartbeat + dependency health (issue #18, partial).
- Remote duplicate cleanup issue **#24** is merged on `main` (PR #25, merged July 1, 2026):
  - dry-run planner exists,
  - manual execute endpoint exists,
  - Operations UI can plan + confirm + execute in controlled batches,
  - executor revalidates local state before each remote delete to tolerate stale plans,
  - planner hydrates missing loser-side `playlist_item_id` values from YouTube and persists
    them locally when it can reconcile a match,
  - first live YouTube batch already executed successfully.
- Organize planner issue **#4** is now on `main` (July 4, 2026):
  - `POST /api/organize/plan` returns a dry-run plan from the configured inbox playlist,
  - `/organize` renders the preview UI with action/quota summary cards,
  - the planner currently uses the best existing tag suggestion/manual tag as the topic signal,
  - it previews `create_playlist`, `move`, and `review` actions without mutating YouTube.
- Organize observability issue **#21** is now on `main` (July 4, 2026):
  - `GET /api/operations/activity` returns newest-first organize-side pipeline activity with pagination,
  - `GET /api/operations/quota` reports the daily move budget snapshot (`movesUsedToday`,
    `moveBudget`, `resetsAt`, `unitsRemaining`),
  - `/operations` now shows a neutral-loading move-budget meter and activity feed,
  - the dashboard operations card now surfaces the same move-budget snapshot.
- Organize classifier issue **#2** is now on `main` (July 5, 2026):
  - `CategorizationPipeline` now exposes `ClassifyAsync` and uses **Ollama first** when reachable,
  - if Ollama is unavailable or returns unusable output, categorization falls back to
    keyword/TF-IDF without failing the run,
  - Ollama output is constrained to the known tag vocabulary and malformed output is tolerated,
  - `Categorization:AutoFileConfidence` is now the shared confidence threshold used by the
    planner for move vs review decisions.
- Managed-playlist materialization issue **#3** is now on `main` (July 5, 2026):
  - `IPlaylistOrganizer` / `PlaylistOrganizer` now expose `EnsureManagedPlaylistAsync(topic)`,
  - topic matching is normalized (trimmed, case-insensitive) and ignores unmanaged playlists,
  - managed playlist creation is quota-aware and single-flight idempotent,
  - the service persists newly created playlists with `IsManaged=true` and `Topic=<topic>`.
- Organize executor issue **#5** is partially landed on `main` (July 5, 2026):
  - `POST /api/organize/execute` now executes the next organize batch from the current planner output,
  - execution is capped by `Organize:ExecutionBatchSize` (default `20`) and the shared daily move budget,
  - successful moves still write 7-day undo logs through `PlaylistOrganizer.MoveVideoAsync`,
  - YouTube playlist inserts now request `position: 0` for newest-first filing,
  - the worker now has a 15-minute `OrganizeExecutionJob` that only runs when Ollama is reachable.
- Organize dedup detection issue **#6** is superseded by shipped work already on `main`:
  - local state now enforces **one playlist per video** via the unique
    `playlist_videos.video_id` index and the cleanup migration,
  - remote YouTube-side duplicate detection + controlled cleanup shipped under issue `#24`,
  - the old `#6` assumption that cross-playlist membership is intentional no longer matches the
    current product direction.

## Recently fixed (2026-06-29)
- **Full sync stalled forever.** Root cause: monolithic all-or-nothing sync with per-item DB
  N+1 in the linking phase + concurrent full/inbox syncs colliding on the same tables.
  - Sync is now **incremental + checkpointed**: playlist-by-playlist, committed per playlist,
    bulk DB ops. Partial progress is always persisted/reviewable and restartable.
  - Added **single-flight gate** (`SyncConcurrencyGate`) — two syncs never write concurrently.
  - Added **stale-run reaper** (15-min threshold) in the worker loop + on startup, so an
    interrupted run can never show "in progress" forever (completes issue #18 stale-detection).

## In progress (2026-07-04)
- **GitHub/tracker reconciliation completed.**
  - `main` contains merged work for `#24`; `#9/#16/#17/#18/#19` are now closed after audit.
  - `docs/OPS-RUNBOOK.md` captures the NAS deploy + sync babysitting flow and the
    OAuth -> first full sync verification path used to close out `#19` and the remaining
    documentation acceptance for `#9`.
- **Remote duplicate cleanup rollout (post-merge hardening).**
  - Planner: `POST /api/operations/duplicates/plan-remote-cleanup`
  - Executor: `POST /api/operations/duplicates/execute-remote-cleanup`
  - UI: `/operations` shows duplicate review, remote cleanup plan, confirmation modal, batch-size
    control, and execution summary.
  - Reconciliation rollout result: planner resolves loser-side `playlist_item_id` values; the
    live plan became **1,104 resolved / 0 unresolved**.
  - Controlled live batch result: executed **5/5** planned removals successfully on YouTube
    (`runId: a8dfb170-3030-4502-96ea-a734068bb078`).
  - Post-run verification: planner dropped from **1,104 duplicate videos / 1,355 removals** to
    **1,099 duplicate videos / 1,350 removals** immediately after the batch.
  - Remaining: continue staged live cleanup batches, broaden drift/live verification, and finish
    execution hardening polish.
- **Organize planner rollout.**
  - Live deploy verification on NAS succeeded.
  - `POST /api/organize/plan` currently returns `0` videos / `0` actions on the live system
    because there are no inbox videos pending at the moment.
  - Screenshot artifacts captured:
    `docs/assets/organize-page-initial.png`
    `docs/assets/organize-page-empty-plan.png`
- **Organize observability rollout.**
  - Backend endpoints landed:
    `GET /api/operations/activity`
    `GET /api/operations/quota`
  - `/operations` now includes the move-budget quota meter and activity feed used for issue `#21`.
  - NAS deploy verification succeeded on July 4, 2026.
  - Live endpoint check:
    - `/api/operations/quota` returned `movesUsedToday: 0`, `moveBudget: 80`,
      `unitsRemaining: 80`, `resetsAt: 2026-07-05T07:00:00Z`
    - `/api/operations/activity?limit=3` returned the newest remote cleanup events from run
      `a8dfb170-3030-4502-96ea-a734068bb078`
  - Screenshot artifact captured:
    `docs/assets/operations-observability-live.png`
- **Dedup roadmap reconciliation.**
  - Issue `#6` was audited against the current schema and shipped behavior on July 4, 2026.
  - Result: no separate new local dedup detect implementation is needed because:
    - migration `20260629153000_EnforceSinglePlaylistMembership` already removes local
      cross-playlist duplicates and enforces uniqueness going forward,
    - issue `#24` already covers the remaining remote YouTube duplicate detection/execution path.
  - Remaining roadmap work moves to the next real organize-engine blocker: issue `#2`
    (Ollama-primary classifier).
- **Ollama-primary classifier rollout.**
  - Local verification:
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~Categorization|FullyQualifiedName~OrganizePlannerServiceTests"` → **46 passed**
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter FullyQualifiedName~CategorizationPipelineTests` → **7 passed**
  - Behavior change:
    - `ClassifyAsync` now prefers Ollama over local heuristics,
    - fallback remains non-blocking when the Mac-hosted Ollama endpoint is asleep/unreachable,
    - planner threshold is configurable via `Categorization:AutoFileConfidence` (default `0.65`).
  - NAS deploy verification succeeded on July 5, 2026.
  - Live checks:
    - `GET /api/operations/health` returned healthy dependencies with
      `workerHealthy: true`, `quotaExhausted: false`, and `ollamaReachable: false`
      (the intended degraded state that should now fall back cleanly instead of failing),
    - `POST /api/organize/plan` returned `0` videos / `0` actions on the live system.
  - Screenshot artifact captured:
    `docs/assets/ollama-primary-classifier-live.png`
- **Managed-playlist materialization rollout.**
  - Local verification:
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter FullyQualifiedName~PlaylistOrganizerTests` → **10 passed**
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~PlaylistOrganizerTests|FullyQualifiedName~OrganizePlannerServiceTests"` → **14 passed**
    - `dotnet test tests/PlaylistMiner.IntegrationTests/PlaylistMiner.IntegrationTests.csproj --filter "FullyQualifiedName~OrganizeControllerTests|FullyQualifiedName~OperationsControllerTests|FullyQualifiedName~PlaylistsControllerTests|FullyQualifiedName~UndoControllerTests"` → **16 passed**
  - Behavior change:
    - `EnsureManagedPlaylistAsync` returns an existing managed playlist when present,
    - it creates a new private YouTube playlist only when needed,
    - it throws on quota exhaustion before any partial local state is written.
  - NAS deploy verification succeeded on July 5, 2026.
  - Live checks:
    - `GET /api/operations/health` returned healthy dependencies with
      `workerHealthy: true`, `quotaExhausted: false`, and `ollamaReachable: false`,
    - `POST /api/organize/plan` returned `0` videos / `0` actions on the live system.
  - Screenshot artifact captured:
    `docs/assets/managed-playlist-materialization-live.png`
- **Organize executor rollout.**
  - Local verification:
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~OrganizeExecutorServiceTests|FullyQualifiedName~PlaylistOrganizerTests|FullyQualifiedName~YouTubeApiClientTests|FullyQualifiedName~OrganizePlannerServiceTests"` → **25 passed**
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~OrganizeExecutionJobTests|FullyQualifiedName~InboxProcessingJobTests|FullyQualifiedName~OrganizeExecutorServiceTests"` → **8 passed**
    - `dotnet test tests/PlaylistMiner.IntegrationTests/PlaylistMiner.IntegrationTests.csproj --filter "FullyQualifiedName~OrganizeControllerTests|FullyQualifiedName~OrganizeExecuteControllerTests"` → **2 passed**
    - `npm test -- --runInBand OrganizePage.test.tsx` → **4 passed**
  - Behavior change:
    - `/organize` now exposes an operator-facing **Execute Organize Batch** action plus last-run summary,
    - organize execution rebuilds the current plan server-side, executes only move items, creates managed playlists on demand, and checkpoints counters into `pipeline_runs`,
    - when the daily move budget is exhausted or YouTube quota fails mid-run, the executor defers the remaining moves instead of continuing blindly,
    - executor hardening now revalidates local playlist placement before each move and skips stale/already-applied work idempotently,
    - duplicate move entries for the same video inside one execution batch now run only once and are counted as skipped thereafter.
  - NAS deploy verification succeeded on July 5, 2026.
  - Live checks:
    - `GET /api/operations/health` returned healthy dependencies with
      `workerHealthy: true`, `quotaExhausted: false`, and `ollamaReachable: false`,
    - `POST /api/organize/plan` returned `0` videos / `0` actions on the live system,
    - live `POST /api/organize/execute` was intentionally **not** run in this session because it would mutate real YouTube playlists and spend quota.
  - Screenshot artifact captured:
    `docs/assets/organize-executor-live.png`
  - Follow-up hardening verification on July 5, 2026:
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter FullyQualifiedName~OrganizeExecutorServiceTests` → **5 passed**
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~OrganizeExecutorServiceTests|FullyQualifiedName~OrganizeExecutionJobTests|FullyQualifiedName~PlaylistOrganizerTests"` → **17 passed**
    - NAS `pm-api` and `pm-worker` were redeployed with the hardened executor logic.
  - Move rollback / undo hardening on July 5, 2026:
    - `IYouTubeApiClient.AddVideoToPlaylistAsync` now returns the created playlist-item id,
    - `PlaylistOrganizer.MoveVideoAsync` now stores that target playlist-item id locally and in the undo log,
    - if source removal fails after the target add succeeds, the organizer now compensates by removing the newly-added target item before bubbling the failure,
    - `UndoMoveAsync` now removes the target video using the actual target playlist-item id rather than the old source-side id.
  - Additional verification on July 5, 2026:
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~PlaylistOrganizerTests|FullyQualifiedName~YouTubeApiClientTests"` → **19 passed**
    - `dotnet test tests/PlaylistMiner.UnitTests/PlaylistMiner.UnitTests.csproj --filter "FullyQualifiedName~OrganizeExecutorServiceTests|FullyQualifiedName~OrganizeExecutionJobTests|FullyQualifiedName~PlaylistOrganizerTests|FullyQualifiedName~YouTubeApiClientTests"` → **26 passed**
    - `dotnet test tests/PlaylistMiner.IntegrationTests/PlaylistMiner.IntegrationTests.csproj --filter "FullyQualifiedName~OrganizeControllerTests|FullyQualifiedName~OrganizeExecuteControllerTests"` → **2 passed**
    - NAS `pm-api` and `pm-worker` were redeployed again after the rollback/undo fix,
    - live reads after redeploy remained healthy and `POST /api/organize/plan` still returned `0` videos / `0` actions.

## Gotcha: NEXT_PUBLIC_API_URL is baked at web BUILD time
- The browser's API base = `NEXT_PUBLIC_API_URL`, baked into the pm-web bundle during
  `npm run build` (see [api-client.ts](src/web/src/lib/api-client.ts) — browser branch has
  no localhost fallback). If it's wrong, **every** client fetch fails and the UI shows
  "Checking…" / "Not connected" even though the backend is 100% healthy.
- Root cause seen 2026-06-30: NAS `.env` had `NEXT_PUBLIC_API_URL=http://localhost:5050`
  (a dev value), so the browser fetched the user's laptop. Fixed → `https://playlistminer.home.manikantar.com`.
- Changing it requires a **no-cache rebuild** of pm-web, not just recreate:
  `docker compose -f docker-compose.playlistminer.yml --env-file <repo>/.env build --no-cache pm-web`.
- Drift prevention: `deploy-to-nas.sh` now excludes `.env` from the tar, so the local dev
  `.env` (which has localhost) can no longer overwrite the NAS value on deploy.
- Verify the bake: `docker exec pm-web grep -rhoE 'https?://[^"]*manikantar[^"]*' .next | sort -u`.

## Known issues / watch list
- ~~`UndoRepository.GetPendingAsync` LINQ error~~ — fixed (OrderBy moved before projection);
  `GET /api/undo` returns 200 live.
- `workerHealthy` now also true when a run is actively progressing (was false mid-sync).
- Remaining organize-engine gaps are narrower now: executor/product work still needs explicit
  multi-topic filing policy and a clearer operator path if both the source removal and
  compensating rollback fail; `ConsolidateAsync` is still a stub; watch-history import is still
  unbuilt.

## How to check health fast
```
ssh nas "docker ps --filter name=pm-"
ssh nas "docker exec pm-db psql -U playlistminer -d playlistminer -c \
  'select run_id,pipeline_type,phase,status,playlists_processed,playlists_discovered,updated_at \
   from pipeline_runs order by updated_at desc limit 8;'"
```
