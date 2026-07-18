# PlaylistMiner — Task Backlog

> Cross-session task tracker. Mirrors GitHub issues; this file is the at-a-glance view so any
> session can resume without re-deriving context. Pairs with [STATUS.md](STATUS.md) (live state).
> Convention: `[ ]` todo · `[~]` in progress · `[x]` done. Keep issue numbers in sync.

_Last updated: 2026-07-18_

## Current product direction (locked 2026-07-18)

The app is moving from an operator console toward a supervised-then-autonomous learning
agent. See [ROADMAP.md](ROADMAP.md) for the long-form product plan.

- First week: **show high-confidence proposed actions with UI checkboxes**; user approves.
- After confidence: **aggressive automation with undo**, Telegram digest, and audit trail.
- Inbox source: intended to be `myinbox`. Live API currently sees playlist id `407`
  (`PLH_QpnlkswM8`, `itemCount=7`, `isInbox=false`), so the next work is either marking it
  inbox or fixing UI visibility/refresh.
- AI provider chain: **Ollama first**, configured public AI fallback only when enabled, then
  keyword/TF-IDF fallback.
- Heavy work should run as queued background jobs in an off-peak window (`23:00-05:00`);
  YouTube write-heavy work should prefer after daily quota reset at midnight Pacific Time.
- Cleanup is recommendation-first: top candidates with reasons, not blind deletion.
- Smart semantic search is in scope.

## Immediate next build order

- [ ] #33 **Inbox reliability / `myinbox` bug** — verify why `myinbox` is not showing or not
  selected as inbox after resync. Current live API shows id `407`, name `myinbox`,
  `isInbox=false`, `itemCount=7`.
- [ ] #34 **Automation policy / Autopilot settings** — persisted mode (`manual`,
  `first_week_approval`, `aggressive_with_undo`), thresholds, daily budgets, public AI
  fallback policy, off-peak window, pause/resume.
- [ ] **#8 Process now + reachability** — `POST /api/agent/process-now`, UI button, clean
  Ollama-unavailable skip, optional cloud fallback.
- [ ] #35 **Background operation queue** — durable queued/scheduled/running/completed/failed
  jobs for sync, restore, cleanup, organize, reclassify, weekly synthesis.
- [ ] #36 **Full `AI Skills` restore as a nightly job** — source old playlist id `6`, target new
  playlist id `409`; sample restore already added 5 videos on 2026-07-18.
- [ ] #37 **First-week approval queue** — backend-backed high-confidence action queue with UI
  checkboxes, approve/reject/edit target, feedback capture.
- [ ] **#22 Telegram run digest** — what changed, quota, undo window, failures, approvals.
- [ ] #38 **Cleanup recommendation engine** — top 5 delete candidates with reasons from
  duplicates, stale tech, low relevance, unavailable videos, transcript/title signals.
- [ ] #39 **Public AI provider fallback** — OpenAI/Gemini provider abstraction, privacy policy,
  provider/model audit fields.
- [ ] #41 **Semantic search** — title/description/transcript/concept-note search; begin with
  Postgres full-text/trigram and add embeddings/vector search if needed.

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
Decisions: playlists primary (tags deferred) · aggressive auto-file + 7-day undo · exactly 1
managed playlist/video · newest-first (`position 0`) insert, no reorder quota · ~20/batch, ~80 moves/day
budget · checkpoint per video + idempotent · dedup detect first · Telegram digest primary.
Implemented by **Codex**; reviewed + merged here (Opus for #5/#2 correctness, Sonnet for UI/docs).

- [x] #9 **(prereq)** Set-as-Incoming UI + designate inbox landed on `main`; OAuth/first-sync
  e2e verification path is now documented in `docs/OPS-RUNBOOK.md`
- [x] #6 **(superseded / closed)** Dedup detect path is covered by the local single-playlist
  membership constraint plus remote cleanup planner/executor (`#24`) for YouTube-side cleanup
- [x] #2 Ollama-primary classifier (reachability-gated; keyword/TF-IDF fallback) → topic+confidence
- [x] #3 Topic→managed-playlist materialization (auto-create)
- [x] #4 Reorg planner (dry-run) + `POST /api/organize/plan` + Organize UI preview
- [x] #5 Executor — manual/API/worker execution slice landed, including explicit single-topic filing policy under the one-playlist rule
- [x] #21 Organize observability — activity feed + move-budget quota meter on `/operations`
- [ ] #22 Telegram per-run organize digest (primary "what the agent did" channel)
- [x] #24 Remote YouTube duplicate cleanup planner + executor — merged to `main` via PR #25;
  planner, reconciliation, controlled execution, and first live YouTube batch are done
- [ ] #8 Ollama reachability gating + `POST /api/agent/process-now` + button
- [ ] #7 `ConsolidateAsync` real merge of overlapping-topic playlists (later)

### Issue hygiene needed

- [ ] #42 Reconcile GitHub `#5`: issue remains open even though the executor slice and
  one-playlist policy are largely implemented. Either close it or update its remaining
  acceptance criteria to match the current product direction.
- [x] Create GitHub issues for the new 2026-07-18 roadmap items listed above. Use issues for
  implementation clarity; keep docs as the cross-session product memory.

## Operations (#16–19)
- [x] #16 Backend pipeline progress model + status/events API
- [x] #17 Operations UI: live pipeline page + dashboard card
- [x] #18 Worker heartbeat, dependency health, stalled-run detection
- [x] #19 Ops runbook: NAS deploy, live sync babysitting, failure buckets (no secrets)

## P2 — Learning agent (Phase 2)
- [ ] #10 `concepts/` markdown wiki + mastery scoring (hybrid brain)
- [ ] #11 Watch-history import via Google Takeout
- [ ] #12 Weekly synthesis job → Telegram learning-plan digest
- [ ] #40 Transcript ingestion/cache for better classification, cleanup, weekly synthesis, and
  semantic search.

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
