# PlaylistMiner — Task Backlog

> Cross-session task tracker. Mirrors GitHub issues; this file is the at-a-glance view so any
> session can resume without re-deriving context. Pairs with [STATUS.md](STATUS.md) (live state).
> Convention: `[ ]` todo · `[~]` in progress · `[x]` done. Keep issue numbers in sync.

_Last updated: 2026-06-29_

## In progress / just landed
- [x] **Incremental, checkpointed sync** — playlist-by-playlist, committed per playlist, bulk DB
  ops. Fixes the "Full sync stalls forever / nothing to show" breakage. (`SyncService`)
- [x] **Single-flight sync gate** (`SyncConcurrencyGate`) — no concurrent table writers.
- [x] **Stale-run reaper** (15-min) in worker loop + startup — finishes issue #18 stale-detection.
- [ ] **Deploy + verify** the above on NAS; confirm a full sync now completes (not stuck InProgress).
- [ ] **Close #16/#17/#18** once verified live; finish #19 (operator runbook).
- [ ] Re-verify `UndoRepository.GetPendingAsync` / `/undo` page after deploy.

## P0 — Organize Engine (the core missing ~80%) — `docs/ORGANIZE-ENGINE-SPEC.md`
- [ ] #2 Make Ollama the primary classifier (reachability-gated; keyword/TF-IDF fallback)
- [ ] #3 Topic→playlist materialization (auto-create managed playlists)
- [ ] #4 Reorg planner (dry-run) + `POST /api/organize/plan` + Organize UI
- [ ] #5 Reorg executor — wire `MoveVideoAsync`, throttled + quota-aware + 7-day undo

## P1 — Organize Engine + Operations
- [ ] #6 Deduplication pass (same video across/within playlists)
- [ ] #7 Implement `ConsolidateAsync` (currently a stub) — merge overlapping-topic playlists
- [ ] #8 Ollama reachability gating + `POST /api/agent/process-now` + "Process now" button
- [ ] #9 Phase 0 completion: Set-as-Incoming UI + verify OAuth + first sync end-to-end
- [~] #16 Backend pipeline progress model + status/events API _(built; verify live)_
- [~] #17 Operations UI: live pipeline page + dashboard card _(built; verify live)_
- [~] #18 Worker heartbeat, dependency health, stalled-run detection _(reaper done; verify live)_
- [ ] #19 Ops runbook: NAS deploy, live sync babysitting, failure buckets (no secrets)

## P2 — Learning agent (Phase 2)
- [ ] #10 `concepts/` markdown wiki + mastery scoring (hybrid brain)
- [ ] #11 Watch-history import via Google Takeout
- [ ] #12 Weekly synthesis job → Telegram learning-plan digest

## P3 — Learning agent (future)
- [ ] #13 MCP server facade (OpenClaw/Hermes/HA/Telegram)
- [ ] #14 Home Assistant Voice "what should I learn today?"
- [ ] #15 Outbound concept suggestions from followed channels

## Architecture note: incremental processing (per user direction, 2026-06-29)
Process in small, reviewable, restartable units — never one giant forever-job:
- **Sync**: per-playlist commit (done).
- **Organize** (when built, #2–#5): drain Incoming and classify/file in **small batches**
  (e.g. N videos per run), each batch committed + visible, quota-aware with deferral, so a
  rate-limit or restart never loses work and there's always something to review.
- Every long job writes a `pipeline_run` + events so progress is always visible (issues #16–18).
