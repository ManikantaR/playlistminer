# PlaylistMiner Ops Runbook

Secret-free operator guide for deploying PlaylistMiner to the NAS, triggering sync, watching
progress, and classifying common failures.

Use this document instead of tribal knowledge. It is intended to be explicit enough for a human
operator or a low-context agent to follow safely.

Related docs:
- `docs/NAS-DEPLOYMENT-SPEC.md`
- `docs/OAUTH-SETUP.md`

## Safety Rules

- Never print `.env` contents.
- Never dump the `settings` table; it contains encrypted OAuth material.
- Use only aggregate SQL queries and status endpoints.
- Prefer `curl`, `docker ps`, `docker logs`, and safe `psql` counts.
- Do not paste secrets, refresh tokens, or client secrets into chat, issues, or commits.

## Preconditions

- You are on the Mac that has the `nas` SSH alias configured.
- The repo root is `/Users/manikantaradhakrishna/repo/playlistminer`.
- NAS deploys use `./deploy-to-nas.sh`.
- The app URL is `https://playlistminer.home.manikantar.com`.

## Small-Model Checklist

1. Run `./deploy-to-nas.sh all`.
2. Confirm API health with `GET /api/health`.
3. Confirm operations health with `GET /api/operations/health`.
4. Confirm `pm-api`, `pm-worker`, `pm-web`, and `pm-db` are running.
5. Confirm OAuth is connected with `GET /api/oauth/status`.
6. Trigger a full sync with `POST /api/sync/trigger`.
7. Poll `GET /api/pipeline/status`, `GET /api/pipeline/history`, and `GET /api/pipeline/events`.
8. Poll safe DB counts for `playlists`, `videos`, `playlist_videos`, and `sync_requests`.
9. If the run stalls or fails, inspect `pm-worker` logs and classify the failure.
10. Summarize status as one of: `healthy idle`, `processing`, `quota blocked`, `stalled`,
    `OAuth broken`, `client bug`, or `failed`.

## 1. Deploy to NAS

From the repo root:

```bash
./deploy-to-nas.sh all
```

Useful variants:

```bash
./deploy-to-nas.sh api
./deploy-to-nas.sh worker
./deploy-to-nas.sh web
./deploy-to-nas.sh sync-only
```

Expected result:
- `pm-api` rebuilds and reaches `healthy`
- `pm-worker` is `running`
- `pm-web` is `running`
- final output includes the app URL

## 2. Verify Container Health

Check containers directly on the NAS:

```bash
ssh nas "docker ps --filter name=pm-"
```

API health:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/health
```

Expected healthy result:

```json
{"status":"healthy","db":"up"}
```

Operations health snapshot:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/operations/health | jq
```

What to look for:
- `dbHealthy: true`
- `workerHealthy: true`
- `oauthConnected: true`
- `quotaExhausted: false` for normal sync work
- `activeRunStalled: false`

Pipeline dependency snapshot:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/pipeline/health | jq
```

## 3. Verify OAuth Before Triggering Sync

Check OAuth status:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/oauth/status
```

Expected connected result:

```json
{"connected":true}
```

If OAuth is not connected, stop here and follow `docs/OAUTH-SETUP.md`.

## 4. Trigger a Full Sync

Queue a full sync:

```bash
curl -sS -X POST https://playlistminer.home.manikantar.com/api/sync/trigger
```

Expected result:

```json
{"message":"Sync triggered."}
```

This writes a pending row into `sync_requests`. The worker picks it up asynchronously.

## 5. Watch the Sync Until Completion

### Primary API checks

Current pipeline status:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/pipeline/status | jq
```

Recent pipeline history:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/pipeline/history | jq '.[0:5]'
```

Recent sync status:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/sync/status | jq
```

Recent sync history:

```bash
curl -sS https://playlistminer.home.manikantar.com/api/sync/history | jq '.[0:5]'
```

If you have a `runId`, fetch events for that run:

```bash
curl -sS "https://playlistminer.home.manikantar.com/api/pipeline/events?runId=<RUN_ID>" | jq '.[-20:]'
```

### Safe DB checks

Check sync request lifecycle:

```bash
ssh nas "docker exec pm-db psql -U playlistminer -d playlistminer -c \
  \"select id,type,status,requested_at,started_at,completed_at from sync_requests order by requested_at desc limit 10;\""
```

Check recent sync logs:

```bash
ssh nas "docker exec pm-db psql -U playlistminer -d playlistminer -c \
  \"select id,sync_type,status,started_at,completed_at,videos_processed from sync_logs order by started_at desc limit 10;\""
```

Check recent pipeline runs:

```bash
ssh nas "docker exec pm-db psql -U playlistminer -d playlistminer -c \
  \"select run_id,pipeline_type,phase,status,playlists_processed,playlists_discovered,videos_processed,videos_deferred,updated_at from pipeline_runs order by updated_at desc limit 8;\""
```

Check aggregate counts:

```bash
ssh nas "docker exec pm-db psql -U playlistminer -d playlistminer -c \
  \"select (select count(*) from playlists) as playlists, (select count(*) from videos) as videos, (select count(*) from playlist_videos) as playlist_video_links;\""
```

### Completion conditions

Treat a full sync as complete when all of the following are true:
- `sync_requests` row transitions to `completed`
- latest `sync_logs.status` is `completed`
- latest `pipeline_runs.status` is `completed` or another expected terminal state
- counts have stabilized
- `activeRunStalled` is `false`

## 6. Verify OAuth -> First Full Sync End-to-End

Use this when closing out first-time setup work.

1. Open `https://playlistminer.home.manikantar.com/settings`.
2. Confirm the UI shows YouTube as connected, or complete the OAuth flow from
   `docs/OAUTH-SETUP.md`.
3. Confirm `GET /api/oauth/status` returns `{"connected":true}`.
4. Confirm an inbox playlist is selected in Settings.
5. Trigger a full sync with `POST /api/sync/trigger`.
6. Watch `GET /api/pipeline/status` until the sync finishes.
7. Confirm non-zero counts in `playlists`, `videos`, and `playlist_videos`.
8. Confirm the UI no longer looks empty because backend data actually exists.

This closes the remaining verification part of issue `#9`.

## 7. How to Tell Backend Empty vs UI Bug

If the web UI looks empty, do not assume the backend is broken.

Check in this order:

1. `GET /api/health`
2. `GET /api/operations/health`
3. `GET /api/pipeline/status`
4. Aggregate DB counts

Interpretation:
- API healthy + non-zero DB counts + empty UI:
  likely client-side configuration or build drift
- API healthy + zero DB counts + no successful sync:
  backend is still empty, not a UI bug
- API unhealthy:
  treat as deployment or dependency issue first

Known real-world client bug:
- `NEXT_PUBLIC_API_URL` is baked at web build time.
- If it points at localhost or the wrong host, the browser can appear disconnected while the
  backend is healthy.
- See `docs/NAS-DEPLOYMENT-SPEC.md` and `STATUS.md`.

## 8. Failure Taxonomy

### OAuth not connected

Signals:
- `GET /api/oauth/status` returns `connected: false`
- `GET /api/operations/health` returns `oauthConnected: false`

Check:
- `docs/OAUTH-SETUP.md`
- Settings page

First action:
- complete or repair OAuth before doing anything else

### Quota exhausted

Signals:
- `quotaExhausted: true`
- pipeline run becomes `deferred`
- events mention quota exhaustion or 403 quota failures

Check:
- `GET /api/operations/health`
- `GET /api/pipeline/history`
- latest pipeline events

First action:
- stop retrying aggressively; wait for quota reset

### Worker alive but stalled

Signals:
- `workerHealthy: false` or `activeRunStalled: true`
- latest run stays `in_progress` with stale `updated_at`

Check:
- `GET /api/operations/health`
- `GET /api/pipeline/status`
- `docker logs pm-worker`

First action:
- inspect worker logs, identify the blocked phase, then decide whether to redeploy/restart

### Worker missing dependency / native library

Signals:
- worker container restarts or exits
- logs show startup failure, DI failure, or missing runtime dependency

Check:

```bash
ssh nas "docker logs --tail 200 pm-worker"
```

First action:
- redeploy after code/config fix; do not inspect secrets

### `ISyncTrigger` / DI issue

Signals:
- `POST /api/sync/trigger` returns success, but no `sync_requests` row appears
- API or worker logs show service resolution failure

Check:
- API logs
- worker logs
- `sync_requests` table

First action:
- fix service registration or startup wiring; this is not a YouTube problem

### YouTube deserialization mismatch / API shape drift

Signals:
- sync starts, then fails early in playlist or metadata fetch
- logs show JSON or model binding errors

Check:
- `pm-api` or `pm-worker` logs
- latest pipeline event phase

First action:
- inspect the failing endpoint contract and patch the DTO/client mapping

### Metadata fetch 403

Signals:
- sync advances into metadata hydration and then fails/deferred on 403

Check:
- pipeline events
- worker logs
- operations health quota flag

First action:
- distinguish quota exhaustion from auth/scope problems

### UI stale vs backend empty

Signals:
- browser says “Checking…” or looks blank
- API endpoints still return healthy data

Check:
- direct `curl` calls to API endpoints
- DB aggregate counts
- web build-time API URL configuration

First action:
- rebuild/redeploy web if `NEXT_PUBLIC_API_URL` is wrong

## 9. Safe Log Commands

Worker logs:

```bash
ssh nas "docker logs --tail 200 pm-worker"
```

API logs:

```bash
ssh nas "docker logs --tail 200 pm-api"
```

Follow worker logs live:

```bash
ssh nas "docker logs -f pm-worker"
```

Do not use logs as the primary truth if the status endpoints and DB already answer the question.

## 10. What Not To Do

- Do not run `select * from settings;`
- Do not print `.env`
- Do not paste raw OAuth rows into issues or chat
- Do not assume an empty UI means empty backend
- Do not keep retriggering sync if quota is exhausted
- Do not use destructive DB commands during routine babysitting
