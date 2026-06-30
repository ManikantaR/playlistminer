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
- [x] **Deploy + verify** on NAS — full sync completed end-to-end (406/406, ~13k videos), no
  stuck InProgress; reaper cleared the old stalled runs; `workerHealthy` true during a run.
- [x] Re-verify undo — `GET /api/undo` returns 200 (LINQ bug already fixed by agent).
- [ ] **Close #16/#17/#18** on GitHub (verified live); finish #19 (operator runbook).

## Organize Engine — locked build order (`docs/ORGANIZE-ENGINE-SPEC.md` §0, §8)
Decisions: playlists primary (tags deferred) · aggressive auto-file + 7-day undo · up-to-2
topics/video · newest-first (`position 0`) insert, no reorder quota · ~20/batch, ~80 moves/day
budget · checkpoint per video + idempotent · dedup detect first · Telegram digest primary.
Implemented by **Codex**; reviewed + merged here (Opus for #5/#2 correctness, Sonnet for UI/docs).

- [ ] #9 **(prereq)** Set-as-Incoming UI + designate inbox + verify OAuth/first-sync e2e
- [ ] #6 **(1st)** Dedup DETECT pass + review list — zero quota, immediate payoff
- [ ] #2 Ollama-primary classifier (reachability-gated; keyword/TF-IDF fallback) → topic+confidence
- [ ] #3 Topic→managed-playlist materialization (auto-create)
- [ ] #4 Reorg planner (dry-run) + `POST /api/organize/plan` + Organize UI preview
- [ ] #5 Executor — wire `MoveVideoAsync`, ~20/batch, move-budget, idempotent, newest-first, undo
- [ ] #NEW-A Organize observability — activity feed + move-budget quota meter on `/operations`
- [ ] #NEW-B Telegram per-run organize digest (primary "what the agent did" channel)
- [ ] #8 Ollama reachability gating + `POST /api/agent/process-now` + button
- [ ] #7 `ConsolidateAsync` real merge of overlapping-topic playlists (later)

## Operations (#16–19)
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
