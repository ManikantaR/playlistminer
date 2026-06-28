# PlaylistMiner — YouTube OAuth Setup (step by step)

Goal: let PlaylistMiner read your playlists and move videos between them. This requires
**OAuth** (an API key alone can't touch private playlists). Do this once.

**SECURITY:** never commit Client Secret / API key / the `client_secret_*.json`. They live
only in the NAS `.env` (gitignored).

---

## What you need to end up with
Four values in the NAS `.env` at `/volume1/docker/playlistminer/repo/.env`:
- `YOUTUBE_CLIENT_ID`
- `YOUTUBE_CLIENT_SECRET`
- `YOUTUBE_API_KEY`
- `YOUTUBE_ENCRYPTION_KEY` (you generate this — see step 6)

---

## Step 1 — Project + API
1. Go to **Google Cloud Console** → https://console.cloud.google.com/ → pick (or create)
   a project, e.g. `playlistminer`.
2. **APIs & Services → Library** → search **"YouTube Data API v3"** → **Enable**.

## Step 2 — OAuth consent screen (the part that bites)
1. **APIs & Services → OAuth consent screen** (newer console: "Google Auth Platform →
   Branding/Audience").
2. **User type: External** → Create.
3. App name = `PlaylistMiner`; user support email + developer email = yours.
4. **Scopes** → Add:
   - `https://www.googleapis.com/auth/youtube`
   - `https://www.googleapis.com/auth/youtube.force-ssl`
   (These are "sensitive" — for personal use you do **not** need Google verification.)
5. **Publishing status → click "PUBLISH APP" → set to "In production".**
   ⚠️ This is critical: in **Testing** mode refresh tokens **expire after 7 days** and the
   headless NAS agent dies weekly. In Production (even unverified) they don't expire.
   You'll see an "unverified app" warning at login — that's expected for personal apps.

## Step 3 — OAuth client ID (Web application)
1. **APIs & Services → Credentials → Create Credentials → OAuth client ID**.
2. **Application type: Web application**. Name: `PlaylistMiner`.
3. **Authorized redirect URIs** — add **both**, exactly (no trailing slash):
   - `https://playlistminer.home.manikantar.com/api/oauth/callback`  ← NAS (primary)
   - `http://localhost:5050/api/oauth/callback`  ← Mac dev (fallback)
4. **Create** → copy the **Client ID** and **Client Secret**.

## Step 4 — API key (public metadata)
1. **Credentials → Create Credentials → API key**.
2. Copy it; **Restrict key → API restrictions → YouTube Data API v3** (recommended).

## Step 5 — Put values in the NAS `.env`
SSH to the NAS and edit `/volume1/docker/playlistminer/repo/.env` (do NOT commit it):
```
YOUTUBE_CLIENT_ID=<from step 3>
YOUTUBE_CLIENT_SECRET=<from step 3>
YOUTUBE_API_KEY=<from step 4>
YOUTUBE_ENCRYPTION_KEY=<from step 6>
```

## Step 6 — Generate the encryption key
On your Mac (encrypts the stored refresh token; must be base64 32 bytes):
```
openssl rand -base64 32
```
Paste the output as `YOUTUBE_ENCRYPTION_KEY`.

## Step 7 — Apply env + restart
```
ssh nas "cd /volume1/docker && docker compose -f docker-compose.playlistminer.yml \
  --env-file /volume1/docker/playlistminer/repo/.env up -d --force-recreate pm-api pm-worker"
```
(`docker restart` does NOT re-read env on UGOS — must use `--force-recreate`.)

## Step 8 — Create the Incoming playlist (on YouTube)
In YouTube, create a normal playlist named **Incoming** (NOT Watch Later — the API can't
touch Watch Later). This is where you'll drop recommendations for the agent to organize.
Later, mark it as the inbox in PlaylistMiner Settings.

## Step 9 — Connect
1. From a LAN browser: open **https://playlistminer.home.manikantar.com/settings**.
2. Click **Connect YouTube** → Google consent → (click **Advanced → Go to PlaylistMiner**
   past the unverified warning) → you're redirected back with a green "Connected".
3. Verify: **https://playlistminer.home.manikantar.com/api/oauth/status** → `{"connected":true}`.

---

## Troubleshooting
- **`redirect_uri_mismatch`** → the URI in step 3 doesn't match *exactly* (scheme/host/path).
- **"Connected" then stops working in ~7 days** → consent screen still in Testing (step 2.5).
- **403 / access blocked** → app in Testing and your account isn't a test user; publish it.
- **Can't reach the URL** → must be on the LAN (AdGuard resolves `*.home.manikantar.com`);
  test from your Mac/phone, not the NAS host (macvlan isolation).
