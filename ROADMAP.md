# PlaylistMiner — Roadmap

> Durable product roadmap for resuming any session cold. Pair with [STATUS.md](STATUS.md)
> for current live state and [TASK.md](TASK.md) for the executable backlog.
>
> Last updated: 2026-07-18

## Product North Star

PlaylistMiner is not just a playlist organizer. It is a hands-free personal learning agent
whose sensor and actuator are YouTube playlists.

The user saves videos into a custom inbox playlist, currently intended to be `myinbox`.
PlaylistMiner syncs that inbox, understands each video, files it into the best topic
playlist, updates learning memory, and reports what changed. The user should not have to
babysit the app, but must never be surprised by silent data loss or unexplained changes.

## Operating Principles

- **First-week supervised autonomy.** For the first week, show high-confidence proposed
  actions with UI checkboxes. The user approves selected actions. After enough good results,
  switch to aggressive mode.
- **Aggressive with undo after confidence is earned.** Once trusted, high-confidence actions
  can run automatically, but every mutation must be auditable and undoable.
- **Playlists are the visible product.** YouTube playlists are what the user actually sees
  on phone. Tags are internal classifier signals and learning graph seeds.
- **Exactly one managed playlist per video.** Secondary topics become metadata/concept
  signals, not extra playlist memberships.
- **No surprise deletion.** Cleanup starts as recommendations only, especially top-5 delete
  candidates with reasons. Deletion requires explicit approval until trust is proven.
- **Prefer local AI, allow explicit cloud fallback.** Ollama is first. If unavailable, use
  configured public AI provider only when policy allows it and make that visible.
- **Off-peak heavy work.** Restore, large sync, cleanup, and bulk organize should be staged
  jobs, preferably after 11 PM and before 5 AM local time. For YouTube quota, heavy write
  batches should prefer after midnight Pacific Time.
- **Telegram is the primary notification channel.** The app UI is for operations. Routine
  changes and approvals should flow through Telegram or equivalent push channel.
- **Smart search matters.** The long-term memory should support natural-language and semantic
  search across playlists, titles, descriptions, transcripts, tags, and concept notes.

## Current Phase

We are between "operator console with automation underneath" and "true autonomous agent."

The app can sync, classify, plan, execute organized moves, undo, observe runs, clean remote
duplicates, and restore sampled videos through the app API. It still needs the autonomy
control plane: policy, approval queue, process-now, queued off-peak jobs, Telegram digest,
cleanup recommendations, semantic search, and concept memory.

## Build Order

### 0. Reconcile Trackers and Issues

- Refresh `TASK.md`, `STATUS.md`, and this roadmap whenever major PRs land.
- Update GitHub issue `#5`: close it if current single-playlist executor behavior is accepted,
  or rewrite the remaining acceptance criteria to match the latest one-playlist policy.
- Keep roadmap items as GitHub issues once they are actionable. Use docs for strategy, GitHub
  issues for work execution and PR closure.

### 1. Inbox Reliability

Goal: the user has a reliable source playlist for the agent loop.

Status: shipped via PR #43 on 2026-07-18 and deployed to the NAS.

- Verify why `myinbox` did not appear as expected after UI resync.
- Current live API finding on 2026-07-18: playlist exists as:
  - id `407`
  - YouTube id `PLH_QpnlkswM8`
  - name `myinbox`
  - item count `7`
  - `isInbox: false`
- Decide whether to mark id `407` as inbox.
- Fix UI search/refresh/casing issue if the playlist is present in API but not visible in UI.
- Add test coverage for selecting a newly synced playlist as inbox.

### 2. Automation / Autopilot Policy

Goal: make autonomy explicit and controllable.

Status: shipped via PR #45 on 2026-07-18 and deployed to the NAS. The API and Settings UI
persist policy in the existing `settings` table. Organize planning reads the persisted
high-confidence threshold, operations quota reads the persisted daily move budget, and the
background organize job respects pause/mode.

Add persisted policy settings:

- mode: `manual`, `first_week_approval`, `aggressive_with_undo`
- high-confidence threshold
- review threshold
- daily move budget
- nightly restore budget
- cleanup recommendation count
- off-peak window, default `23:00-05:00`
- public AI fallback enabled/disabled
- public AI provider and model
- transcript-to-cloud policy
- automation pause/resume

UI should show:

- current mode
- next scheduled run
- last run summary
- quota remaining
- pending approvals
- pause/resume controls

### 3. Process Now and Reachability

Goal: if the Mac/Ollama is awake, the user can drain inbox immediately; if not, the worker
does not fail noisily.

- Implement `POST /api/agent/process-now`.
- Add UI button on Organize/Automation page.
- Probe Ollama before LLM-dependent work.
- If Ollama is unavailable and cloud fallback is disabled, leave work queued and log a clean skip.
- If fallback is enabled, route to public provider and record provider used in audit logs.

### 4. Background Operation Queue

Goal: sync, restore, cleanup, organize, and reclassification are durable jobs, not ad hoc
interactive requests.

Add a job table and UI for:

- queued
- scheduled
- running
- completed
- deferred
- failed
- canceled

Job types:

- full sync
- inbox sync
- categorize/reclassify
- organize plan
- organize execute
- restore playlist
- cleanup recommendation
- cleanup execute
- weekly synthesis

Every job should have:

- created by
- source
- target
- max items
- quota estimate
- allowed execution window
- run id
- audit events

### 5. Full `AI Skills` Restore

Goal: restore the deleted playlist without manual token access and without quota blowups.

Facts:

- Old DB playlist id `6`: `AI Skills`, old YouTube id `PL2pbi0OI4yFX9psx-U5ZElpREYpW4q7l3`
- New YouTube playlist id `409`: `AI skills`, YouTube id `PLW7KgJNN7b4Y`
- On 2026-07-18, sample restore added 5 videos through the deployed app API.
- After sample restore:
  - old playlist id `6`: `1251 -> 1246`
  - new playlist id `409`: `66 -> 71`

Plan:

- Convert restore-sample into queued restore job. PR #51 implemented `playlist_restore` queue
  support, worker execution via `RestoreBatchAsync`, and a `/operations` button that stages the
  known old->new `AI Skills` restore for the off-peak window; merged and deployed on 2026-07-18.
- Run nightly after quota reset, preferably after midnight Pacific Time; operational local
  window remains `23:00-05:00`.
- Use budget around `120-150` adds/night unless quota pressure says otherwise.
- Current branch adds restore status visibility: `GET /api/playlists/{target}/restore-status`
  reports source total, target total, already-present count, and remaining count, and `/operations`
  displays the known `AI Skills` restore status above the queue.
- Save progress/checkpoint after each successful add.
- Notify via Telegram after each batch:
  - added count
  - remaining count
  - quota used
  - failures
  - next scheduled batch

### 6. First-Week Approval Queue

Goal: train trust before aggressive automation.

- Backend-backed queue of proposed high-confidence actions.
- UI checkboxes for approve/reject.
- Bulk approve only for currently filtered result with clear count.
- Capture user feedback:
  - accepted
  - rejected
  - edited target playlist
  - suggested better topic
- Use feedback to improve classifier/rules.
- After one week, report acceptance rate and recommend whether to switch to aggressive mode.

### 7. Telegram Run Digest and Approval Channel

Goal: the user should know what happened without opening the app.

Per-run digest:

- filed videos
- created playlists
- skipped/review items
- cleanup candidates
- quota spent/remaining
- undo window reminder
- failures/manual cleanup

Approval messages:

- top high-confidence moves for first-week mode
- top cleanup candidates
- restore progress
- one-tap approve/reject if feasible

### 8. Cleanup Recommendation Engine

Goal: reduce hoarding without blind deletion.

Start with recommendation-only top 5 candidates.

Ranking signals:

- unavailable/private/deleted videos
- exact duplicates
- old videos superseded by newer videos
- stale technology/version terms
- low relevance to focus areas
- title/description/transcript mismatch
- low semantic value or repeated intro/marketing content
- very old unwatched backlog

For each candidate show:

- video title
- playlist
- age/date added when known
- reason
- confidence
- transcript/summary snippet when available
- approve delete / keep / ask later

### 9. Public AI Provider Fallback

Goal: the agent works even when Ollama is unavailable, with clear privacy boundaries.

Provider chain:

1. Ollama local
2. configured public provider, e.g. OpenAI or Gemini
3. keyword/TF-IDF fallback

Policy:

- Title/description can use cloud if enabled.
- Transcripts can use cloud only if explicitly enabled.
- Private concept notes/default learning profile should stay local unless the user opts in.
- Every generated suggestion records provider/model/source.

### 10. Transcript and Semantic Search

Goal: make the library useful for learning, not just filing.

Stages:

- Cache transcripts/captions where available.
- Generate summaries/concepts per video.
- Add semantic embeddings for title/description/transcript/concept notes.
- Start with Postgres full-text/trigram; add vector search if needed.
- Search examples:
  - "videos about MCP agent loops"
  - "cleanup candidates for obsolete AngularJS"
  - "what did I save about eval harnesses?"

### 11. Concept Wiki and Weekly Synthesis

Goal: turn saved videos into a learning memory.

- Add `concepts/*.md` wiki.
- Add mastery score per concept.
- Add video-to-concept mapping.
- Weekly synthesis reads:
  - concept notes
  - watched/imported history
  - recent saved videos
  - accepted/rejected classifier feedback
- Weekly Telegram digest should be one focused plan, not daily noise.

### 12. MCP and Voice

Only after the weekly plan is useful:

- MCP tools:
  - `get_learning_plan`
  - `whats_next(topic?)`
  - `list_gaps`
  - `record_watched(videoId)`
  - `get_concept(name)`
  - `list_incoming`
  - `trigger_organize`
- Home Assistant Voice:
  - "what should I learn today?"
  - "what changed last night?"
  - "approve the cleanup suggestions"

## GitHub Issue Strategy

Yes: keep this as GitHub issues for clarity.

Docs should describe product direction and cross-cutting decisions. GitHub issues should be
the unit of implementation. Each issue should include:

- problem / user value
- current state
- scope
- files likely touched
- acceptance criteria
- tests required
- rollout/verification steps
- privacy/quota considerations

Recommended new issues:

- #33 Inbox reliability: `myinbox` selection/sync/UI visibility
- #34 Automation policy and Autopilot page
- #35 Background operation queue and off-peak scheduler
- #36 Playlist restore full background job
- #37 First-week approval queue
- Telegram run digest and approval channel
- #38 Cleanup recommendation engine
- #39 Public AI provider fallback policy
- #40 Transcript ingestion/cache
- #41 Semantic search
- #42 Roadmap hygiene: reconcile organize executor issue #5 with one-playlist policy
