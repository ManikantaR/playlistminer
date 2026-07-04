# PlaylistMiner — Live Status

> Single source of truth for "where are we right now". Update this whenever system state
> changes (deploy, sync run, new bug). Pairs with [TASK.md](TASK.md) (the backlog).
> Goal: any session — human or agent — can resume cold from this file.

_Last updated: 2026-07-04_

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
- Organize engine still ~80% unbuilt (see [TASK.md](TASK.md), issues #2–#9): categorization
  only *suggests* tags; `PlaylistOrganizer.MoveVideoAsync` is unwired; `ConsolidateAsync` is a
  stub; no dedup; no watch-history import.

## How to check health fast
```
ssh nas "docker ps --filter name=pm-"
ssh nas "docker exec pm-db psql -U playlistminer -d playlistminer -c \
  'select run_id,pipeline_type,phase,status,playlists_processed,playlists_discovered,updated_at \
   from pipeline_runs order by updated_at desc limit 8;'"
```
