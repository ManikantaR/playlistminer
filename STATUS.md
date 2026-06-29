# PlaylistMiner — Live Status

> Single source of truth for "where are we right now". Update this whenever system state
> changes (deploy, sync run, new bug). Pairs with [TASK.md](TASK.md) (the backlog).
> Goal: any session — human or agent — can resume cold from this file.

_Last updated: 2026-06-29_

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

## Data (as of 2026-06-29)
- Playlists: **406** synced
- Videos: **11,614** synced
- Categorization runs: completing (~6,900 videos/run)

## What works
- Full + inbox sync pull real data from YouTube.
- Categorization pipeline (keyword + TF-IDF; Ollama when Mac reachable) produces tag **suggestions**.
- Operations UI (`/operations`) + dashboard card show live pipeline progress (issues #16/#17).
- Worker heartbeat + dependency health (issue #18, partial).

## Recently fixed (2026-06-29)
- **Full sync stalled forever.** Root cause: monolithic all-or-nothing sync with per-item DB
  N+1 in the linking phase + concurrent full/inbox syncs colliding on the same tables.
  - Sync is now **incremental + checkpointed**: playlist-by-playlist, committed per playlist,
    bulk DB ops. Partial progress is always persisted/reviewable and restartable.
  - Added **single-flight gate** (`SyncConcurrencyGate`) — two syncs never write concurrently.
  - Added **stale-run reaper** (15-min threshold) in the worker loop + on startup, so an
    interrupted run can never show "in progress" forever (completes issue #18 stale-detection).

## Known issues / watch list
- `UndoRepository.GetPendingAsync` had a LINQ-translation error on `/api/undo/pending`
  (agent touched `UndoRepository.cs`; re-verify on the live `/undo` page after deploy).
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
