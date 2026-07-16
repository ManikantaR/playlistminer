# PlaylistMiner — Organize Engine Spec

The core capability: **physically reorganize YouTube playlists** — drain an Incoming
playlist, classify each video into a topic, file it into a managed topic playlist (creating
it if needed), and remove duplicates. Planner/executor/dedup work is now largely built; the
remaining gaps are Telegram digests, process-now reachability controls, and real playlist
consolidation (see §6). This spec defines the engine.

Status: design (2026-06-15; decisions locked 2026-06-30). Builds on VISION-v2 + NAS deployment.

---

## 0. Locked decisions (2026-06-30)

Agreed with the user after research + grilling. These are the parameters every issue inherits.

- **Playlists are the product; tags are a deferred internal index.** The engine's output is
  YouTube playlist membership (visible on the user's phone YouTube app, where he actually
  watches). Tags/`VideoTag` remain the *classifier signal* + seed for the Phase-2 learning
  graph — **no tag-management UX is built now.**
- **Aggressive auto-file.** When confidence ≥ threshold, the agent moves without asking; the
  7-day `UndoLog` is the safety net. Below threshold → **needs review** queue (no quota spent).
- **Exactly 1 managed playlist per video.** When multiple topics clear the threshold, file to
  the single highest-confidence winner and defer the rest as secondary classifier signals.
  Never multi-home a video across managed playlists locally or on YouTube.
- **Newest-filed-first on YouTube.** Insert at `position 0` (free — it's the same insert).
  **Never spend quota reordering** a playlist to maintain a sort; richer sorting is a free
  app-side view, later. (User browses playlists in the YouTube phone app, so stored order
  matters and must stay cheap.)
- **Batch size ~20 videos/run**, throttled to a **daily move budget (~80 moves, configurable)**
  reserving ~2,000 units for reads. When the budget is spent → defer remainder to tomorrow.
- **Checkpoint per video; idempotent moves.** Advance the cursor only for fully-completed
  videos. Every move carries an idempotency key (`videoId + targetPlaylistId + runId`) so a
  crash/retry/quota-wall never double-moves or skips. (See research: the #1 checkpoint bug is
  advancing the cursor on partial success.)
- **Dedup is a separate, near-free DETECT pass and ships first.** Detecting duplicates is a
  DB query (0 quota); only *resolution* costs a 50-unit delete. Same-video-twice-in-one-
  playlist (exact dup) → auto-resolve; same video across *different* playlists is now treated
  as drift from the single-playlist policy and should be reconciled back to one winner.
- **Observability suite (user lives in the phone YouTube app, not this UI):**
  - **Telegram per-run digest is the primary channel** ("Filed 18 → AI Agents/TypeScript,
    created 1 playlist, 3 need review, moves 18/80").
  - **Activity feed** (append-only "what changed") + **move-budget quota meter** on `/operations`.
- **Move approval = none (aggressive)**; the dry-run **plan** (§4) still exists as a *preview/
  audit* surface and for the manual "review & run" path, but the scheduled loop auto-executes.

---

## 1. Hard constraints (researched, non-negotiable)

- **Watch Later is inaccessible.** The YouTube Data API cannot read *or* write `WL`
  (`playlistItems.insert/delete` for WL/HL fully deprecated; WL list returns empty even
  for the owner). → **Incoming MUST be a user-created custom playlist**, never Watch Later.
- **Watch history is Takeout-only** (API returns nothing). Deferred to Phase 2.
- **Quota wall:** add(50)+remove(50) = ~100 units/move; 10,000 units/day ⇒ ~100 moves/day
  via API. Bulk reorg must be throttled over days, or use browser automation.

## 2. Data model additions

- `Playlist.IsManaged` (exists) — true for app-owned topic playlists.
- `Playlist.Topic` (exists) — the canonical topic, e.g. "AI Agents". 1:1 with a Tag.
- Reuse `Tag` as the topic vocabulary (a managed playlist mirrors a tag/topic).
- `VideoTag` (exists) — classification output; `confidence` drives best-fit selection.
- No new tables required for v1; the reorg "plan" can be computed on demand. (Optional:
  persist a `ReorgPlan`/`ReorgItem` for auditability — defer unless needed.)

## 3. Classification (LLM-first, reachability-gated)

- Primary: **Ollama on the Mac**. Worker probes `GET {OllamaBaseUrl}/api/tags` first; if
  unreachable, leave videos queued in Incoming and retry next cycle + expose "Process now".
- Model configurable via `OLLAMA_MODEL` (mistral:7b / gemma — 7–9B for interactive, 12B
  for overnight batch on the 16 GB M1).
- Prompt: given video title + description + the controlled topic vocabulary (existing tag
  names), return 1–N topics with confidence. Constrain output to the known vocabulary;
  allow a "suggest new topic" escape hatch (see §4).
- Keep keyword + TF-IDF as a cheap fallback when Ollama is down.

## 4. Reorg planner (dry-run, no mutations)

Produces a reviewable plan; executes nothing. Inputs: Incoming playlist (+ optionally
existing unmanaged playlists for a full re-sort). For each video:

1. Classify → best-fit topic (highest confidence above threshold). If multiple topics clear the
   threshold, keep the secondaries as classifier signal only and file to the single winner.
2. Resolve target managed playlist for that topic; if none exists, mark **CREATE playlist**.
3. If the video already sits in the correct managed playlist → **no-op**.
4. **Dedup:** if the same `YouTubeId` exists in multiple managed playlists, keep it in the
   single best-fit (highest confidence) and mark the others **REMOVE**. Also collapse exact
   repeats within one playlist.
5. Low-confidence / unknown-topic videos → **needs review** (surfaced in UI, optional
   "suggest new topic: X" from the LLM).

Output: ordered list of actions `{create_playlist | move | remove_duplicate | review}` with
an estimated quota cost. User approves before execution.

## 5. Executor (user chose throttled API + Playwright option)

- **Default — API, quota-aware:** drive `PlaylistOrganizer.MoveVideoAsync` (already exists)
  + `CreatePlaylistAsync`. Check `IQuotaTracker` before each op; stop when near the daily
  cap, resume next cycle. Every move writes a 7-day `UndoLog` (already exists).
- **Bulk — Playwright browser automation (optional):** for large backlogs or to bypass the
  quota wall (mirrors Vic563/yt-playlist-organizer). Drives the YouTube web UI. Heavier and
  brittle (UI changes); behind a flag, not the default.
- Idempotent: re-running converges (no-ops for already-correct placements).

## 6. What exists vs. what to build

| Piece | State | Action |
|---|---|---|
| `FullSyncAsync` (pull all playlists/items/meta) | ✅ works | reuse |
| YouTube CRUD (create/add/remove) | ✅ works | reuse |
| Classification scoring (keyword/TF-IDF/Ollama) | ✅ Ollama-primary with fallback | reuse existing `VideoTag` topic signal |
| `MoveVideoAsync` | ✅ wired into organize executor | reuse |
| `ConsolidateAsync` | ❌ **stub** (just lists) | implement real merge/dedup |
| Topic→playlist materialization (auto-create) | ✅ works | reuse |
| Reorg planner (dry-run) | ✅ works | reuse |
| Dedup | ✅ local single-playlist constraint + remote cleanup path | continue staged remote cleanup |
| Watch-history import | ❌ missing | Phase 2 (Takeout) |

## 7. API + UI surface (new)

- `POST /api/organize/plan` → compute dry-run plan (no mutations).
- `POST /api/organize/execute` → execute an approved plan (throttled).
- `POST /api/agent/process-now` → drain Incoming immediately (Mac-awake trigger).
- UI: an "Organize" page — show the plan (creates/moves/dupes/review), approve, watch
  progress + quota budget; review queue for low-confidence items.

## 8. Build order (locked 2026-06-30 — maps to GitHub issues)

0. **Prereq — Set-as-Incoming UI + designate inbox** (#9). The loop drains "Incoming"; it must
   be selectable. Cheap, unblocks everything.
1. **Dedup DETECT pass + review list** (#6) — *first*: zero quota, immediate visible payoff
   ("found 47 duplicates"). Auto-resolve exact same-playlist dups only.
2. **Ollama-primary classifier, reachability-gated** (#2) — topic + confidence per video.
3. **Topic→managed-playlist materialization** (auto-create) (#3).
4. **Reorg planner (dry-run)** + `POST /api/organize/plan` + Organize UI preview (#4) —
   single winning topic/video, confidence, estimated quota.
5. **Executor** (#5) — wire `MoveVideoAsync`; ~20-video batches, move-budget quota-aware,
   idempotent, newest-first (`position 0`) insert, 7-day undo, checkpoint per video, defer on
   budget. `POST /api/organize/execute` + scheduled auto-run.
6. **Organize observability** (#21) — activity feed + move-budget quota meter on `/operations`.
7. **Telegram per-run digest** (#22; relates to #12) — primary "what the agent did" channel.
8. **Ollama reachability gating + `POST /api/agent/process-now`** (#8).
9. **`ConsolidateAsync`** real merge of overlapping-topic playlists (#7) — later.
10. (Optional) Playwright executor for bulk backfill beyond the daily budget.
11. (Phase 2) Takeout watch-history import as a learning signal (#11).
