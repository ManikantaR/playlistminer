# PlaylistMiner NAS Deployment Spec

Living document for deploying PlaylistMiner on the UGREEN DXP4800+ NAS. Mirrors the
conventions in `~/repo/MyMoney/specs/NAS-DEPLOYMENT-SPEC.md` (the canonical homelab
reference). This spec is the single source of truth for any AI agent or human picking up
PlaylistMiner deployment work.

**SECURITY RULE**: This file is committed to git. NEVER put real secrets, passwords, API
keys, OAuth client secrets, refresh tokens, account numbers, IPs with credentials, or PII
in this file. Use `<PLACEHOLDER>` patterns. Real values live only in gitignored `.env`
files on the NAS.

---

## 1. Target Hardware & OS (shared homelab)

| Property | Value |
|----------|-------|
| NAS | UGREEN DXP4800+ (RAID 1) |
| CPU | Intel N100 (4-core) |
| OS | UGOS Pro (Debian-based) |
| Docker | Engine 26.1.0 + standalone `docker compose` v2 (no plugin) |
| NAS LAN IP | `10.140.1.95` (MyHome VLAN), 32 GB RAM |
| SSH alias | `nas` (user `manikanta3`, configured in `~/.ssh/config` on Mac) |
| Reverse proxy | Traefik on macvlan `10.140.1.3`, owns **80/443**; entrypoint `websecure` + `tls=true` |
| Domain / TLS | **`home.manikantar.com`** with wildcard Let's Encrypt (AWS Route 53 DNS-01) |
| DNS | AdGuard Home wildcards `*.home.manikantar.com` → `10.140.1.3` (no per-app entry needed) |
| Shared network | `traefik-public` (external) |
| App URL | **https://playlistminer.home.manikantar.com** (web), `…/api` → pm-api |

### UGOS quirks (inherited — must-know)
- **`scp -O` required** (legacy protocol; no SFTP subsystem).
- **BusyBox tar** can't parse macOS xattrs → use `COPYFILE_DISABLE=1 --no-xattrs`.
- **No compose plugin** → standalone `docker compose` v2 binary.
- **`docker restart` does NOT re-read env** → use `docker compose up -d --force-recreate`.
- **`NEXT_PUBLIC_*` is build-time** → must be a Docker build ARG + compose `build.args`,
  never just runtime `environment:` (this is MoneyPulse issue 7.6, already hit here too).

---

## 2. Components

Runtime = **Docker on the NAS** (not podman; podman stays for Mac dev only — see ADR-009).

| Component | Tech | Container | Port (internal) | Host |
|-----------|------|-----------|-----------------|------|
| API | ASP.NET Core (.NET 10) | `pm-api` | 8080 | NAS |
| Worker (agent loop) | .NET Worker + Quartz | `pm-worker` | — | NAS |
| Database | PostgreSQL 16 | `pm-db` | 5432 | NAS |
| Web UI | Next.js | `pm-web` | 3000 | NAS |
| **Ollama (inference)** | Ollama + Qwen ~8B | — | 11434 | **Mac (NOT on NAS)** |

> **Ollama is deliberately NOT a NAS container.** The N100 runs a 7B model at ~0.5–1 tok/s.
> Inference runs on the M1 Mac. The NAS worker reaches it at
> `Ollama__BaseUrl = http://<MAC_LAN_IP>:11434` (Ollama must bind `0.0.0.0`, not just
> localhost). See §6 for the sleep-handling strategy.

**Repo**: `~/repo/playlistminer` (Mac) → `/volume1/docker/playlistminer/repo` (NAS)
**Compose**: `/volume1/docker/docker-compose.playlistminer.yml`
**Env**: `/volume1/docker/playlistminer/repo/.env` (NAS, gitignored)

---

## 3. NAS Directory Layout

```
/volume1/docker/
├── docker-compose.playlistminer.yml
└── playlistminer/
    ├── repo/            # source synced from Mac
    │   └── .env         # NAS env (NEVER commit)
    ├── pg/              # PostgreSQL data (own instance, not shared with MoneyPulse)
    ├── concepts/        # the markdown concept wiki (the "brain" — see VISION-v2 §3)
    └── backup/          # daily pg_dump (7-day retention)
```

---

## 4. OAuth — works directly on the NAS (real HTTPS domain)

Because the homelab now has a **real domain with valid wildcard TLS**
(`playlistminer.home.manikantar.com`), OAuth completes **directly on the NAS** — no
Mac-bootstrap/token-copy needed. The OAuth redirect happens in *your browser*, which
resolves the domain via AdGuard on the LAN and trusts the Let's Encrypt cert; Google never
has to reach the URL itself, it only needs it registered and HTTPS.

> An **API key is still insufficient** — it reads public data only and cannot read your
> private playlists (`mine=true`) or move videos (`playlistItems.insert/delete`). OAuth is
> mandatory; the API key is only for public metadata hydration.

**Setup (one-time):**

1. In **Google Cloud Console**, add this **Authorized redirect URI** to the Web-app OAuth
   client: `https://playlistminer.home.manikantar.com/api/oauth/callback`
   (keep the `http://localhost:5050/...` one too, for Mac dev).
2. Ensure the **OAuth consent screen is "In production"** (Published) — Testing-mode
   refresh tokens expire after 7 days and would kill the headless agent weekly.
3. From a LAN browser: open `https://playlistminer.home.manikantar.com/settings` →
   **Connect YouTube** → consent → redirected back → encrypted refresh token stored in the
   NAS Postgres `settings` table (`oauth.refresh_token`).
4. Verify: `https://playlistminer.home.manikantar.com/api/oauth/status` →
   `{"connected":true}`.

**Fallback (off-LAN, or domain issues):** authorize on the Mac at
`http://localhost:5050/api/oauth/callback`, then copy the encrypted token row to the NAS
Postgres. This requires `YouTube__EncryptionKey` to be **identical on both hosts**.
```bash
# On Mac — dump just the oauth row, ship to NAS, apply
podman exec pm-db pg_dump -U playlistminer -d playlistminer --data-only \
  --table=settings --column-inserts -O 2>/dev/null | grep "oauth.refresh_token" > /tmp/oauth_row.sql
scp -O /tmp/oauth_row.sql nas:/tmp/
ssh nas "docker exec -i pm-db psql -U playlistminer -d playlistminer < /tmp/oauth_row.sql && rm /tmp/oauth_row.sql"
rm /tmp/oauth_row.sql
```

---

## 5. Deployment workflow

Adapt MoneyPulse's `deploy-to-nas.sh` (build happens **on the NAS** per decision):
`tar (COPYFILE_DISABLE=1 --no-xattrs)` → `scp -O` → `docker compose -f <compose> --env-file
<env> build` → `up -d --force-recreate` → health-check poll.

```bash
./deploy-to-nas.sh           # build + deploy all
./deploy-to-nas.sh api       # api only
./deploy-to-nas.sh web       # web only
./deploy-to-nas.sh sync-only # sync source, no rebuild
```

> Note: .NET 10 + Next.js builds on the N100 (4 cores) are slow and RAM-heavy. If build
> time becomes painful, revisit "build on Mac / GitHub Actions → GHCR, NAS pulls" (ADR-009
> records this as the fallback).

---

## 6. Ollama reachability — graceful queue + on-demand trigger

The worker runs 24/7 on the NAS; the Mac (Ollama) sleeps. Chosen behavior:

- **Graceful queue:** before an understanding/synthesis job, the worker probes
  `GET {Ollama__BaseUrl}/api/tags`. If unreachable, it leaves videos in **Incoming**,
  logs a skip, and retries next cycle. No failures, eventual consistency — filing happens
  whenever the Mac is awake.
- **On-demand trigger:** add `POST /api/agent/process-now` + a **"Process now"** button in
  the web UI. When your Mac is up, one click drains the Incoming queue immediately instead
  of waiting for the next scheduled cycle.
- Weekly synthesis is scheduled but also gated by the same reachability probe (skips and
  re-arms if the Mac is asleep).

---

## 7. Integration with the existing homelab stack

- **Traefik:** docker labels on `pm-web`/`pm-api` (already in `docker-compose.nas.yml`) —
  `Host(playlistminer.home.manikantar.com)` on entrypoint `websecure` + `tls=true`, with
  `/api` (priority 10) → pm-api:8080 and `/` (priority 1) → pm-web:3000.
- **AdGuard:** no change — the `*.home.manikantar.com` wildcard already resolves to
  `10.140.1.3`.
- **Uptime Kuma:** monitor `https://playlistminer.home.manikantar.com/api/health`
  (returns 503 if DB down) and a check on the Mac's Ollama endpoint.
- **Homepage:** PlaylistMiner card added in `homelab-stack/homepage/services.yaml`
  (customapi widget → `http://pm-api:8080/api/health`).
- **Watchtower:** only if we move to registry-pulled images; build-on-NAS images aren't
  auto-updated.
- **Backup:** daily `pg_dump` of `pm-db` to `/volume1/docker/playlistminer/backup/`
  (the BackupJob already exists in the worker — point it at the mounted backup dir).
  The `concepts/` wiki is git-versioned separately.

---

## 8. Secrets

Reuse MoneyPulse's posture: gitleaks pre-commit + GitHub Actions secret scan, `.env`
gitignored. PlaylistMiner-specific secret env vars (NAS `.env`):

| Variable | Purpose |
|----------|---------|
| `POSTGRES_PASSWORD` | PostgreSQL auth |
| `YOUTUBE_API_KEY` | Public metadata hydration |
| `YOUTUBE_CLIENT_ID` / `YOUTUBE_CLIENT_SECRET` | OAuth |
| `YOUTUBE_ENCRYPTION_KEY` | AES-256 for refresh token — **must match the Mac's value** |

---

## 9. Changelog

| Date | Change | Category |
|------|--------|----------|
| 2026-06-14 | Created PlaylistMiner NAS deployment spec (Docker on UGREEN, OAuth token-transfer, build-on-NAS, Ollama-on-Mac graceful queue) | Deploy |
| 2026-06-15 | Corrected to real homelab: domain `home.manikantar.com`, Traefik macvlan .3 on 443 (websecure+tls), HTTPS. OAuth now works directly on NAS (Mac-copy = fallback). Added `/api/health` + curl in API image, Traefik docker labels, Homepage card. Fixed deploy script `pm-*` service names. | Deploy |
