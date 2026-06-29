#!/usr/bin/env bash
# deploy-to-nas.sh — Sync PlaylistMiner to the UGREEN NAS and rebuild Docker containers.
# Mirrors ~/repo/MyMoney/deploy-to-nas.sh. See docs/NAS-DEPLOYMENT-SPEC.md + ADR-009.
#
# Usage:
#   ./deploy-to-nas.sh              # build & deploy all (db/api/worker/web)
#   ./deploy-to-nas.sh api          # api only
#   ./deploy-to-nas.sh worker       # worker only
#   ./deploy-to-nas.sh web          # web only
#   ./deploy-to-nas.sh sync-only    # sync source, no rebuild

set -euo pipefail

# ── Config ──────────────────────────────────────────────────
NAS_HOST="nas"
REPO_DIR="$HOME/repo/playlistminer"
NAS_REPO="/volume1/docker/playlistminer/repo"
NAS_COMPOSE="/volume1/docker/docker-compose.playlistminer.yml"
NAS_ENV="$NAS_REPO/.env"
TMP_ARCHIVE="/tmp/playlistminer-sync.tar.gz"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
log()  { echo -e "${BLUE}[deploy]${NC} $*"; }
ok()   { echo -e "${GREEN}[✓]${NC} $*"; }
warn() { echo -e "${YELLOW}[!]${NC} $*"; }
err()  { echo -e "${RED}[✗]${NC} $*" >&2; }

# ── Pre-flight ──────────────────────────────────────────────
cd "$REPO_DIR" || { err "Repo not found at $REPO_DIR"; exit 1; }

if [ -n "$(git status --porcelain)" ]; then
    warn "You have uncommitted changes:"
    git status --short
    echo ""
    read -p "Continue anyway? (y/N) " -n 1 -r; echo
    [[ $REPLY =~ ^[Yy]$ ]] || { log "Aborting."; exit 0; }
fi

log "Testing SSH to $NAS_HOST..."
ssh -o ConnectTimeout=5 "$NAS_HOST" "echo ok" > /dev/null 2>&1 || {
    err "Cannot reach NAS via SSH. Check your connection / ~/.ssh/config alias 'nas'."
    exit 1
}
ok "NAS reachable"

# ── Step 1: Sync code ───────────────────────────────────────
sync_code() {
    log "Packaging repo (excluding build artifacts + secrets)..."
    # COPYFILE_DISABLE + --no-xattrs: UGOS BusyBox tar can't parse macOS extended attributes.
    # SECURITY: exclude .env / *.env / client_secret*.json so a local dev .env never overwrites
    # the NAS's own credentials and the OAuth client secret never lands on the NAS via deploy.
    COPYFILE_DISABLE=1 tar czf "$TMP_ARCHIVE" \
        --no-xattrs \
        --exclude=node_modules \
        --exclude=.git \
        --exclude=bin \
        --exclude=obj \
        --exclude=.next \
        --exclude=coverage \
        --exclude=TestResults \
        --exclude='.env' \
        --exclude='*.env' \
        --exclude='client_secret*.json' \
        . 2>/dev/null || \
    COPYFILE_DISABLE=1 tar czf "$TMP_ARCHIVE" \
        --exclude=node_modules --exclude=.git --exclude=bin --exclude=obj \
        --exclude=.next --exclude=coverage --exclude=TestResults \
        --exclude='.env' --exclude='*.env' --exclude='client_secret*.json' .

    local size; size=$(du -h "$TMP_ARCHIVE" | cut -f1)
    log "Uploading to NAS ($size)..."
    scp -O "$TMP_ARCHIVE" "$NAS_HOST:/tmp/"          # -O: legacy SCP required by UGOS

    log "Extracting on NAS..."
    ssh "$NAS_HOST" "mkdir -p $NAS_REPO && cd $NAS_REPO && tar xzf /tmp/playlistminer-sync.tar.gz && rm /tmp/playlistminer-sync.tar.gz"

    # Keep the central compose file in sync with the repo copy
    ssh "$NAS_HOST" "cp $NAS_REPO/docker-compose.nas.yml $NAS_COMPOSE"

    rm -f "$TMP_ARCHIVE"
    ok "Code synced to NAS"
}

# ── Step 2: Build & deploy ──────────────────────────────────
build_and_deploy() {
    local service="$1"
    local svc="pm-$service"   # compose service names are pm-api / pm-web / pm-worker / pm-db
    log "Building $svc on NAS (N100 — this can take a while)..."
    ssh "$NAS_HOST" "cd $NAS_REPO && docker compose -f $NAS_COMPOSE --env-file $NAS_ENV build $svc"
    ok "$svc image built"

    log "Deploying $svc..."
    # --force-recreate: 'docker restart' does NOT re-read env vars on UGOS
    ssh "$NAS_HOST" "cd $NAS_REPO && docker compose -f $NAS_COMPOSE --env-file $NAS_ENV up -d --force-recreate $svc"
    ok "$svc deployed"

    log "Waiting for $svc..."
    local attempts=0
    while [ $attempts -lt 45 ]; do
        if [ "$service" = "api" ]; then
            # pm-api has a Docker healthcheck (curl /api/health, verifies DB)
            local health
            health=$(ssh "$NAS_HOST" "docker inspect --format='{{.State.Health.Status}}' pm-api 2>/dev/null" || echo "unknown")
            if [ "$health" = "healthy" ]; then ok "pm-api is healthy"; return 0; fi
        else
            local state
            state=$(ssh "$NAS_HOST" "docker inspect --format='{{.State.Status}}' $svc 2>/dev/null" || echo "unknown")
            if [ "$state" = "running" ]; then ok "$svc is running"; return 0; fi
        fi
        sleep 2; attempts=$((attempts + 1))
    done
    warn "$svc not confirmed healthy — check: ssh nas docker logs $svc"
}

# ── Main ────────────────────────────────────────────────────
TARGET="${1:-all}"
case "$TARGET" in
    sync-only) sync_code ;;
    api)       sync_code; build_and_deploy api ;;
    worker)    sync_code; build_and_deploy worker ;;
    web)       sync_code; build_and_deploy web ;;
    all)
        sync_code
        build_and_deploy api
        build_and_deploy worker
        build_and_deploy web
        ;;
    *) echo "Usage: $0 [api|worker|web|all|sync-only]"; exit 1 ;;
esac

echo ""
ok "Deploy complete!"
log "Logs:  ssh nas docker logs -f pm-worker"
log "App:   https://playlistminer.home.manikantar.com"
