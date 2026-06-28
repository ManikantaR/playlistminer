# PlaylistMiner — Organize Engine Spec

The core missing capability: **physically reorganize YouTube playlists** — drain an Incoming
playlist, classify each video into a topic, file it into a managed topic playlist (creating
it if needed), and remove duplicates. Today the app only produces *tag suggestions*; the
move/consolidate/dedup logic is unbuilt or unwired (see §6). This spec defines the engine.

Status: design (2026-06-15). Builds on VISION-v2 (learning agent) + NAS deployment.

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

1. Classify → best-fit topic (highest confidence above threshold).
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
| Classification scoring (keyword/TF-IDF/Ollama) | ⚠️ suggests tags only | make Ollama primary; map topic→playlist |
| `MoveVideoAsync` | ⚠️ exists, **unwired** | wire into organize job + endpoint |
| `ConsolidateAsync` | ❌ **stub** (just lists) | implement real merge/dedup |
| Topic→playlist materialization (auto-create) | ❌ missing | build (§4) |
| Reorg planner (dry-run) | ❌ missing | build (§4) |
| Dedup | ❌ missing | build (§4.4) |
| Watch-history import | ❌ missing | Phase 2 (Takeout) |

## 7. API + UI surface (new)

- `POST /api/organize/plan` → compute dry-run plan (no mutations).
- `POST /api/organize/execute` → execute an approved plan (throttled).
- `POST /api/agent/process-now` → drain Incoming immediately (Mac-awake trigger).
- UI: an "Organize" page — show the plan (creates/moves/dupes/review), approve, watch
  progress + quota budget; review queue for low-confidence items.

## 8. Build order

1. LLM classifier → topic mapping (Ollama primary, reachability-gated).
2. Managed-playlist materialization (auto-create topic playlists).
3. Reorg planner (dry-run) + `/organize/plan` + Organize UI (plan view).
4. Executor (throttled API) + undo + quota gating + `/organize/execute`.
5. Dedup pass folded into planner/executor.
6. (Optional) Playwright executor for bulk.
7. (Phase 2) Takeout watch-history import as a learning signal.
