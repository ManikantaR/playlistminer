# Prompt 10: Phase 0 — Bring-Up & Verification (Learning Agent v2)

## Context
Per `docs/VISION-v2.md`, PlaylistMiner is being reframed from a playlist organizer into a
personal learning agent. **Phase 0 is a hard gate: build nothing in Phases 1–4 until the
"hands" work end-to-end with real playlists.** This prompt closes Phase 0.

Phase 0 is mostly operational (run, connect, verify) plus two small code tasks. It is NOT
a feature build. Exit criteria: *I can connect YouTube, sync, see my real videos, and the
Incoming inbox is designated.*

The work splits into: (A) deploy the API-URL fix, (B) verify OAuth live, (C) verify sync
fills the DB, (D) designate the Incoming inbox. Tasks marked **[Copilot]** are code;
tasks marked **[Operator]** are commands Mani runs locally (live Google credentials +
podman live on his machine, not in CI).

---

## A. Deploy the build-time API URL fix  **[Operator]**

`NEXT_PUBLIC_API_URL` is baked into the Next.js bundle at **build time**, not runtime.
The fix (Dockerfile ARG + compose build arg + api-client fallback → `:5050`) is already in
the tree but needs a rebuild.

```bash
cd ~/repo/playlistminer
# Rebuild only the web container so the correct API URL is baked in
podman-compose up -d --build pm-web

# Sanity: confirm the bundle points at :5050, not :5000
podman exec $(podman ps --filter name=pm-web --format '{{.Names}}') \
  sh -c "grep -ro 'localhost:50[0-9][0-9]' .next | sort -u"
```
**Pass:** only `localhost:5050` appears. **Then:** reload the app, click "Trigger Sync" —
the "failed to trigger sync" error must be gone.

> Note: `podman-compose.yml` pm-web has a leftover runtime `environment: NEXT_PUBLIC_API_URL`
> defaulting to `:5000`. It's harmless (runtime env is ignored by the baked bundle) but
> misleading — see task F to clean it up.

---

## B. Verify OAuth live  **[Operator]**

The OAuthController + hooks are implemented but never tested against live Google
credentials. The redirect URI must match exactly in Google Cloud Console.

```bash
# Confirm the redirect URI the API will use matches Google Cloud Console exactly:
#   http://localhost:5050/api/oauth/callback
# (podman-compose.yml currently builds it from API_PORT default 5000 — verify API_PORT=5050
#  is set in .env, or the callback URL will mismatch and Google will reject it.)
grep -E 'API_PORT|RedirectUri' .env podman-compose.yml
```

Then in the browser: Settings → **Connect YouTube** → Google consent → should redirect to
`/settings?connected=true` and the status dot turns green.

**Pass:** `GET /api/oauth/status` returns `{"connected":true}` and the encrypted refresh
token row exists:
```bash
podman exec $(podman ps --filter name=pm-db --format '{{.Names}}') \
  psql -U playlistminer -d playlistminer -c \
  "select key, length(value) from settings where key='oauth.refresh_token';"
```

---

## C. Verify sync fills the DB from real playlists  **[Operator]**

```bash
# Trigger a sync (or click Trigger Sync in the UI)
curl -s -X POST http://localhost:5050/api/sync/trigger

# Watch worker logs for the sync run
podman logs -f $(podman ps --filter name=pm-worker --format '{{.Names}}')

# Confirm real data landed
podman exec $(podman ps --filter name=pm-db --format '{{.Names}}') \
  psql -U playlistminer -d playlistminer -c \
  "select count(*) as videos from videos; select count(*) as playlists from playlists;"
```
**Pass:** counts reflect your real YouTube playlists; `sync_log` has a completed row.

---

## D. Designate the Incoming inbox  **[Copilot]**

The `Playlist.IsInbox` flag already exists and `POST /api/playlists/{id}/set-inbox` is
specced. Verify the endpoint exists and works; if missing, implement it TDD-first.

```
In PlaylistMiner.UnitTests / IntegrationTests, ensure coverage for designating an inbox:

PlaylistServiceTests (or PlaylistsController integration):
- Test_SetInbox_MarksPlaylistAsInbox
- Test_SetInbox_ClearsPreviousInbox   (only one inbox at a time)
- Test_SetInbox_PlaylistNotFound_Returns404

Then ensure POST /api/playlists/{id}/set-inbox:
- Sets IsInbox=true on the target playlist
- Sets IsInbox=false on any other playlist currently marked inbox
- Returns 404 if the playlist id does not exist
- Is idempotent (re-designating the same playlist is a no-op success)

Frontend (src/web): on the Playlists page, add a "Set as Incoming" action per playlist and
a visual "Incoming" badge on the designated one. TDD with React Testing Library.
```
**Pass:** I can pick my "Incoming" playlist in the UI and exactly one playlist is flagged.

---

## E. Phase 0 exit checklist  **[Operator]**

- [ ] `podman-compose up -d --build` brings up all 5 containers healthy
- [ ] Web bundle points at `:5050`; "Trigger Sync" works with no error
- [ ] OAuth Connect → green status; encrypted refresh token persisted
- [ ] Sync populates `videos` + `playlists` from my real account; `sync_log` completed
- [ ] Exactly one playlist designated as Incoming (inbox)
- [ ] `dotnet test` green; frontend `npm test` + Playwright green

When all boxes are checked, Phase 0 is done and Phase 1 (Ollama understanding + concepts
wiki) may begin.

---

## F. Cleanup (low priority)  **[Copilot]**

- Remove the misleading runtime `environment: NEXT_PUBLIC_API_URL: ...:5000` from the
  `pm-web` service in `podman-compose.yml` (the value is build-time only; runtime env is
  dead config).
- Align the `API_PORT` default and `YouTube__RedirectUri` in `podman-compose.yml` so the
  OAuth callback URL is consistently `:5050` (currently defaults to `5000`).
- Add a `.env.example` entry documenting `API_PORT=5050` and
  `NEXT_PUBLIC_API_URL=http://localhost:5050`.
