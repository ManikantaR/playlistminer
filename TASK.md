# PlaylistMiner — Task Backlog

> Cross-session task tracker. Mirrors GitHub issues; this file is the at-a-glance view so any
> session can resume without re-deriving context. Pairs with [STATUS.md](STATUS.md) (live state).
> Convention: `[ ]` todo · `[~]` in progress · `[x]` done. Keep issue numbers in sync.

_Last updated: 2026-07-05_

## In progress / just landed
- [x] **Incremental, checkpointed sync** — playlist-by-playlist, committed per playlist, bulk DB
  ops. Fixes the "Full sync stalls forever / nothing to show" breakage. (`SyncService`)
- [x] **Single-flight sync gate** (`SyncConcurrencyGate`) — no concurrent table writers.
- [x] **Stale-run reaper** (15-min) in worker loop + startup — finishes issue #18 stale-detection.
- [x] **Deploy + verify** on NAS — full sync completed end-to-end (406/406, ~13k videos), no
  stuck InProgress; reaper cleared the old stalled runs; `workerHealthy` true during a run.
- [x] Re-verify undo — `GET /api/undo` returns 200 (LINQ bug already fixed by agent).
- [x] **GitHub hygiene pass** — `#9/#16/#17/#18/#19` reconciled and closed after audit and
  runbook updates.

## Organize Engine — locked build order (`docs/ORGANIZE-ENGINE-SPEC.md` §0, §8)
Decisions: playlists primary (tags deferred) · aggressive auto-file + 7-day undo · up-to-2
topics/video · newest-first (`position 0`) insert, no reorder quota · ~20/batch, ~80 moves/day
budget · checkpoint per video + idempotent · dedup detect first · Telegram digest primary.
Implemented by **Codex**; reviewed + merged here (Opus for #5/#2 correctness, Sonnet for UI/docs).

- [x] #9 **(prereq)** Set-as-Incoming UI + designate inbox landed on `main`; OAuth/first-sync
  e2e verification path is now documented in `docs/OPS-RUNBOOK.md`
- [x] #6 **(superseded / closed)** Dedup detect path is covered by the local single-playlist
  membership constraint plus remote cleanup planner/executor (`#24`) for YouTube-side cleanup
- [x] #2 Ollama-primary classifier (reachability-gated; keyword/TF-IDF fallback) → topic+confidence
- [x] #3 Topic→managed-playlist materialization (auto-create)
- [x] #4 Reorg planner (dry-run) + `POST /api/organize/plan` + Organize UI preview
- [~] #5 Executor — manual/API/worker execution slice landed; remaining hardening on multi-topic + stronger idempotency
- [x] #21 Organize observability — activity feed + move-budget quota meter on `/operations`
- [ ] #22 Telegram per-run organize digest (primary "what the agent did" channel)
- [x] #24 Remote YouTube duplicate cleanup planner + executor — merged to `main` via PR #25;
  planner, reconciliation, controlled execution, and first live YouTube batch are done
- [ ] #8 Ollama reachability gating + `POST /api/agent/process-now` + button
- [ ] #7 `ConsolidateAsync` real merge of overlapping-topic playlists (later)

## Operations (#16–19)
- [x] #16 Backend pipeline progress model + status/events API
- [x] #17 Operations UI: live pipeline page + dashboard card
- [x] #18 Worker heartbeat, dependency health, stalled-run detection
- [x] #19 Ops runbook: NAS deploy, live sync babysitting, failure buckets (no secrets)

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
