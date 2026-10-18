#!/usr/bin/env bash
# One-shot server-side delivery of the reviewed admin reading views release. Run as root
# after loading its images: bash this-script NEW_BACKEND_IMAGE_ID NEW_FRONTEND_IMAGE_ID
set -Eeuo pipefail
umask 077

readonly RELEASE=89a96400be559116412afa389ac514bb921caaf8
readonly OLD_REV=ace5ed21c70b11d9e8b986db366db9034e022feb
readonly OLD_BACKEND=future-viewer-backend:ace5ed21c70b11d9e8b986db366db9034e022feb
readonly OLD_FRONTEND=future-viewer-frontend:ace5ed21c70b11d9e8b986db366db9034e022feb
readonly OLD_BACKEND_ID=sha256:4796391359ade31ac06191eab0ebc1a393958a71b45c3cdecf8bfbaaa03a9bba
readonly OLD_FRONTEND_ID=sha256:a6300a8d29595bdfb99c9484ac825a7207ec55820341b22fe006d1a2e02b6472
readonly OLD_CADDY_ID=sha256:5f5c8640aae01df9654968d946d8f1a56c497f1dd5c5cda4cf95ab7c14d58648
readonly CHECKOUT=/opt/future-viewer
readonly ENV_FILE=/opt/fv-app/.env.production
readonly NEW_BACKEND_ID=${1:-}
readonly NEW_FRONTEND_ID=${2:-}
readonly NEW_MIGRATION=20261018170408_AddAdminReadingViews
readonly MIGRATION_SQL_SHA256=a921ded060ef1b94bcb4c125b0993ca4d89acade64a754c07831b872c9d55fa7
readonly DELIVERY_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
readonly MIGRATION_SQL="$DELIVERY_DIR/migration.sql"
PHASE=guards
STATE_DIR=
RESTORE_ID=
RESTORE_NAME=
APP_UPDATE_STARTED=0
ENV_UPDATED=0
GIT_UPDATED=0

fail() { printf 'Deployment stopped during %s: %s\n' "$PHASE" "$1" >&2; exit 1; }
git_deploy() { sudo -n -u deploy -H git -C "$CHECKOUT" "$@"; }
compose() {
  env FV_IMAGE_TAG="$RELEASE" FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=172.18.0.5 \
    docker compose --env-file "$ENV_FILE" -f "$CHECKOUT/docker-compose.prod.yml" \
    -f "$CHECKOUT/docker-compose.prod.prebuilt.yml" -f "$CHECKOUT/docker-compose.prod.legacy-edge.yml" \
    -p future-viewer "$@"
}
service_id() {
  local cid
  cid=$(docker ps -aq --no-trunc --filter label=com.docker.compose.project=future-viewer \
    --filter "label=com.docker.compose.service=$1")
  [[ "$cid" =~ ^[0-9a-f]{64}$ ]] || return 1
  printf '%s' "$cid"
}
image_id() { docker image inspect --format '{{.Id}}' "$1" 2>/dev/null; }
live_sql() {
  docker exec -i "$POSTGRES_ID" sh -euc \
    'exec psql -X -qAt -v ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' 2>/dev/null
}
migrations() {
  printf '%s\n' 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' | live_sql
}
restored_sql() {
  docker exec -i "$RESTORE_ID" psql -X -qAt -v ON_ERROR_STOP=1 -U postgres -d restore_verify 2>/dev/null
}
retained_counts_sql() {
  cat <<'SQL_COUNTS'
SELECT format('SELECT %L, count(*) FROM %I.%I;', table_name, table_schema, table_name)
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
  AND table_name NOT IN ('admin_reading_views', '__EFMigrationsHistory')
ORDER BY table_name
\gexec
SQL_COUNTS
}
review_state_sql() {
  cat <<'SQL_REVIEW'
SELECT json_build_object(
  'views_table_present', to_regclass('public.admin_reading_views') IS NOT NULL,
  'foreign_keys', (SELECT count(*) FROM information_schema.table_constraints WHERE table_name='admin_reading_views' AND constraint_type='FOREIGN KEY'),
  'migration_applied', (SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018170408_AddAdminReadingViews'));
SQL_REVIEW
}
verify_review_state() {
  python3 - "$1" <<'PY_REVIEW'
import json,sys
state=json.load(open(sys.argv[1]))
assert state=={'views_table_present':True,'foreign_keys':2,'migration_applied':1}, 'Reading view schema differs'
PY_REVIEW
}
health() {
  local attempt admin_status
  for attempt in $(seq 1 30); do
    admin_status=$(curl --silent --max-time 10 --output /dev/null --write-out '%{http_code}' https://alex-taro.ru/api/admin/readings) || admin_status=000
    if curl --fail --silent --max-time 10 --output /dev/null https://alex-taro.ru/health \
      && curl --fail --silent --max-time 10 --output /dev/null https://alex-taro.ru/ \
      && curl --fail --silent --max-time 10 --output /dev/null https://janetka.ru/ \
      && [[ "$admin_status" == 401 ]]; then return 0; fi
    sleep 2
  done
  return 1
}
cleanup_restore() {
  [[ -n "$RESTORE_ID" ]] || return 0
  [[ $(docker inspect --format '{{index .Config.Labels "com.alex-taro.restore-run"}}' "$RESTORE_ID" 2>/dev/null) == "$RESTORE_NAME" ]] || return 1
  docker rm -f "$RESTORE_ID" >/dev/null 2>&1 || return 1
  RESTORE_ID=
}
snapshot() {
  python3 - "$1" <<'PY'
import hashlib,json,subprocess,sys
def command(*args): return subprocess.check_output(args,stderr=subprocess.DEVNULL)
def inspect(kind,ids): return json.loads(command('docker',kind,'inspect',*ids)) if ids else []
containers=inspect('container',command('docker','ps','-aq').decode().split())
apps={}; others={}
for item in containers:
    labels=item['Config'].get('Labels') or {}
    role=labels.get('com.docker.compose.service') if labels.get('com.docker.compose.project')=='future-viewer' else None
    mounts=sorted(({k:m.get(k) for k in ('Type','Name','Source','Destination','Driver','Mode','RW','Propagation')}
                   for m in item['Mounts'] if m['Type'] in ('bind','volume')),key=lambda x:x['Destination'])
    networks={name:{k:net.get(k) for k in ('NetworkID','IPAddress','GlobalIPv6Address','IPAMConfig')}
              for name,net in item['NetworkSettings']['Networks'].items()}
    record={'id':item['Id'],'name':item['Name'],'image':item['Image'],'reference':item['Config']['Image'],
            'mounts':mounts,'networks':networks}
    if role in ('backend','frontend'):
        if role in apps: raise SystemExit('Duplicate application service')
        record['networks']={name:net['NetworkID'] for name,net in networks.items()}
        apps[role]=record
    else: others[item['Id']]=record
networks=inspect('network',command('docker','network','ls','-q').decode().split())
resources={}
for net in networks:
    net.pop('Containers',None)
    resources['network:'+net['Id']]=hashlib.sha256(json.dumps(net,sort_keys=True).encode()).hexdigest()
for volume in inspect('volume',command('docker','volume','ls','-q').decode().split()):
    resources['volume:'+volume['Name']]=hashlib.sha256(json.dumps(volume,sort_keys=True).encode()).hexdigest()
with open(sys.argv[1],'w') as f: json.dump({'apps':apps,'others':others,'resources':resources},f,sort_keys=True)
PY
}
verify_snapshot() {
  snapshot "$STATE_DIR/current-state.json" || return 1
  python3 - "$STATE_DIR/before-state.json" "$STATE_DIR/current-state.json" "$1" "$2" <<'PY'
import json,sys
before,after=[json.load(open(p)) for p in sys.argv[1:3]]
assert before['others']==after['others'], 'Other containers or mounts changed'
assert before['resources']==after['resources'], 'Networks or volumes changed'
assert set(after['apps'])=={'backend','frontend'}, 'Application service set changed'
for role,image in zip(('backend','frontend'),sys.argv[3:]):
    assert after['apps'][role]['image']==image, 'Unexpected application image'
    for field in ('mounts','networks'):
        assert before['apps'][role][field]==after['apps'][role][field], 'Application persistence or network changed'
PY
}
restore_env() {
  python3 - "$STATE_DIR/env.before" "$ENV_FILE" <<'PY'
import os,sys,tempfile
source,target=sys.argv[1:]
fd,temp=tempfile.mkstemp(prefix='.env.rollback-',dir=os.path.dirname(target))
try:
    os.fchmod(fd,0o600); os.fchown(fd,0,0)
    with os.fdopen(fd,'wb') as f: f.write(open(source,'rb').read()); f.flush(); os.fsync(f.fileno())
    os.replace(temp,target)
finally:
    if os.path.exists(temp): os.unlink(temp)
PY
}
finish() {
  local code=$1 result=failed failed_phase=$PHASE
  trap - EXIT INT TERM HUP
  set +e
  cleanup_restore || code=1
  if (( code != 0 )); then
    if (( APP_UPDATE_STARTED )); then
      migrations >"$STATE_DIR/migrations.failure" || true
      # This migration is additive. The previous app can run with the receipt table retained.
      if compose -f "$STATE_DIR/rollback-images.yml" up -d --no-deps --no-build --pull never backend frontend >/dev/null 2>&1 \
        && health && verify_snapshot "$OLD_BACKEND_ID" "$OLD_FRONTEND_ID"; then
        restore_env || result=env_restore_failed
        git_deploy checkout --detach "$OLD_REV" >/dev/null 2>&1 || result=checkout_restore_failed
        [[ "$result" == failed ]] && result=rolled_back
      else result=manual_recovery_required; fi
    else
      if (( ENV_UPDATED )); then restore_env || result=env_restore_failed; fi
      if (( GIT_UPDATED )); then
        git_deploy diff --quiet && git_deploy diff --cached --quiet \
          && git_deploy checkout --detach "$OLD_REV" >/dev/null 2>&1 || result=checkout_restore_failed
      fi
    fi
  else result=success; fi
  if [[ -n "$STATE_DIR" ]]; then
    printf 'release=%s\nstatus=%s\nphase=%s\nexit_code=%s\n' "$RELEASE" "$result" "$failed_phase" "$code" >"$STATE_DIR/status"
  fi
  printf 'Admin reading views delivery: %s (phase=%s).\n' "$result" "$failed_phase"
  [[ -z "$STATE_DIR" ]] || printf 'Protected release record: %s\n' "$STATE_DIR"
  exit "$code"
}
trap 'finish $?' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

[[ $EUID == 0 && $# == 2 ]] || fail 'root execution and two image IDs required'
[[ "$NEW_BACKEND_ID" =~ ^sha256:[0-9a-f]{64}$ && "$NEW_FRONTEND_ID" =~ ^sha256:[0-9a-f]{64}$ ]] || fail 'invalid image ID arguments'
for tool in docker sudo git python3 openssl gzip curl flock sha256sum; do command -v "$tool" >/dev/null || fail 'required command missing'; done
[[ -d /opt/fv-app && ! -L /opt/fv-app && -d "$CHECKOUT/.git" ]] || fail 'unexpected deployment paths'
[[ -f "$MIGRATION_SQL" && ! -L "$MIGRATION_SQL" ]] || fail 'reviewed migration SQL is missing'
[[ $(sha256sum "$MIGRATION_SQL" | cut -d' ' -f1) == "$MIGRATION_SQL_SHA256" ]] || fail 'migration SQL checksum differs'
exec 9>/opt/fv-app/guest-history-deploy.lock
flock -n 9 || fail 'another retention delivery is running'
[[ -f "$ENV_FILE" && ! -L "$ENV_FILE" && $(stat -c '%u:%g:%a' "$ENV_FILE") == 0:0:600 ]] || fail 'unexpected env owner or permissions'
[[ $(docker compose version --short) == 5.1.3 ]] || fail 'unexpected Compose version'
[[ $(git_deploy rev-parse HEAD) == "$OLD_REV" && $(git_deploy branch --show-current) == main ]] || fail 'unexpected server checkout'
[[ $(git_deploy status --porcelain --untracked-files=all) == '?? deploy/Caddyfile.bak-20260708215835' ]] || fail 'unexpected checkout changes'
BACKEND_ID=$(service_id backend) || fail 'backend service is not unique'
FRONTEND_ID=$(service_id frontend) || fail 'frontend service is not unique'
POSTGRES_ID=$(service_id postgres) || fail 'postgres service is not unique'
CADDY_ID=$(service_id caddy) || fail 'Caddy service is not unique'
[[ $(docker inspect --format '{{.Config.Image}}' "$BACKEND_ID") == "$OLD_BACKEND" \
  && $(docker inspect --format '{{.Image}}' "$BACKEND_ID") == "$OLD_BACKEND_ID" \
  && $(docker inspect --format '{{.Config.Image}}' "$FRONTEND_ID") == "$OLD_FRONTEND" \
  && $(docker inspect --format '{{.Image}}' "$FRONTEND_ID") == "$OLD_FRONTEND_ID" ]] || fail 'unexpected application images'
[[ $(docker inspect --format '{{.Image}}' "$CADDY_ID") == "$OLD_CADDY_ID" \
  && $(docker inspect --format '{{(index .NetworkSettings.Networks "future-viewer_default").IPAddress}}' "$CADDY_ID") == 172.18.0.5 ]] || fail 'unexpected shared Caddy'
for cid in "$BACKEND_ID" "$FRONTEND_ID" "$POSTGRES_ID" "$CADDY_ID"; do
  [[ $(docker inspect --format '{{.State.Running}}' "$cid") == true ]] || fail 'required container is not running'
done
[[ $(image_id "future-viewer-backend:$RELEASE") == "$NEW_BACKEND_ID" \
  && $(image_id "future-viewer-frontend:$RELEASE") == "$NEW_FRONTEND_ID" ]] || fail 'loaded images differ from CI IDs'
[[ $(image_id "$OLD_BACKEND") == "$OLD_BACKEND_ID" && $(image_id "$OLD_FRONTEND") == "$OLD_FRONTEND_ID" ]] || fail 'rollback image tags changed'
PG_IMAGE=$(docker inspect --format '{{.Image}}' "$POSTGRES_ID")
PG_VERSION=$(printf 'SHOW server_version_num;\n' | live_sql)
[[ "$PG_VERSION" =~ ^17[0-9]{4}$ ]] || fail 'PostgreSQL 17 required'
health || fail 'pre-deployment health failed'

PHASE=capture
install -d -m 700 -o root -g root /opt/fv-app/releases
STATE_DIR=$(mktemp -d "/opt/fv-app/releases/admin-unseen-$RELEASE-$(date -u +%Y%m%dT%H%M%SZ)-XXXXXX")
chmod 700 "$STATE_DIR"
cp -p "$ENV_FILE" "$STATE_DIR/env.before"
cp "$MIGRATION_SQL" "$STATE_DIR/migration.sql"
chmod 600 "$STATE_DIR/migration.sql"
[[ $(sha256sum "$STATE_DIR/migration.sql" | cut -d' ' -f1) == "$MIGRATION_SQL_SHA256" ]] || fail 'protected migration SQL checksum differs' 
snapshot "$STATE_DIR/before-state.json"
printf '%s\n' "$OLD_REV" >"$STATE_DIR/git.before"
migrations >"$STATE_DIR/migrations.before"
[[ -s "$STATE_DIR/migrations.before" ]] || fail 'migration history is empty'
docker cp "$CADDY_ID:/etc/caddy/Caddyfile" "$STATE_DIR/Caddyfile.before" >/dev/null 2>&1
chmod 600 "$STATE_DIR/Caddyfile.before"
cp -p "$CHECKOUT/deploy/Caddyfile.bak-20260708215835" "$STATE_DIR/Caddyfile.legacy-backup"
cat >"$STATE_DIR/rollback-images.yml" <<EOF
services:
  backend:
    image: $OLD_BACKEND
  frontend:
    image: $OLD_FRONTEND
EOF
DB_BYTES=$(printf 'SELECT pg_database_size(current_database());\n' | live_sql)
[[ "$DB_BYTES" =~ ^[0-9]+$ ]] || fail 'database size unavailable'
IMAGE_BYTES=$(docker image inspect --format '{{.Size}}' "$OLD_BACKEND" "$OLD_FRONTEND" "$OLD_CADDY_ID" | python3 -c 'import sys;print(sum(int(x) for x in sys.stdin))')
GIT_BYTES=$(du -sb "$CHECKOUT/.git" | cut -f1)
FREE_BYTES=$(df -PB1 "$STATE_DIR" | awk 'NR==2 {print $4}')
(( FREE_BYTES > IMAGE_BYTES + GIT_BYTES + DB_BYTES * 2 + 536870912 )) || fail 'insufficient disk space for recovery assets'
docker image save "$OLD_BACKEND" "$OLD_FRONTEND" "$OLD_CADDY_ID" 2>/dev/null | gzip -1 >"$STATE_DIR/previous-images.tar.gz"
git -c safe.directory="$CHECKOUT" -C "$CHECKOUT" bundle create "$STATE_DIR/git-before.bundle" HEAD >/dev/null 2>&1

PHASE=migration_check
git_deploy fetch origin main >/dev/null 2>&1
[[ $(git_deploy rev-parse origin/main) == "$RELEASE" ]] || fail 'origin/main differs from the fixed release'
git_deploy merge-base --is-ancestor "$OLD_REV" "$RELEASE" || fail 'release is not a fast-forward'
git_deploy ls-tree -r --name-only "$RELEASE" backend/src/FutureViewer.Infrastructure/Persistence/Migrations \
  | python3 -c 'import re,sys;print("\n".join(sorted(m.group(1) for line in sys.stdin if (m:=re.search(r"/(\d{14}_[^/]+)\.cs$",line.strip())) and not m.group(1).endswith(".Designer"))))' \
  >"$STATE_DIR/migrations.release"
python3 - "$STATE_DIR/migrations.before" "$STATE_DIR/migrations.release" "$NEW_MIGRATION" <<'PY_MIGRATIONS'
import sys
before,release=[open(p).read().splitlines() for p in sys.argv[1:3]]
expected=sys.argv[3]
assert expected not in before and release==sorted(before+[expected]), 'Expected exactly the reviewed admin receipt migration'
PY_MIGRATIONS
# Historical migrations must be byte-for-byte unchanged; only the new pair and snapshot may differ.
git_deploy diff --name-status "$OLD_REV" "$RELEASE" -- backend/src/FutureViewer.Infrastructure/Persistence/Migrations \
  >"$STATE_DIR/migration-files.diff"
python3 - "$STATE_DIR/migration-files.diff" "$NEW_MIGRATION" <<'PY_MIGRATION_FILES'
import sys
prefix='backend/src/FutureViewer.Infrastructure/Persistence/Migrations/'
expected={'A\t'+prefix+sys.argv[2]+'.cs','A\t'+prefix+sys.argv[2]+'.Designer.cs','M\t'+prefix+'AppDbContextModelSnapshot.cs'}
assert set(open(sys.argv[1]).read().splitlines())==expected, 'Unexpected migration source changes'
PY_MIGRATION_FILES

PHASE=encrypted_backup
openssl rand -base64 48 >"$STATE_DIR/backup.pass"
chmod 600 "$STATE_DIR/backup.pass"
docker exec "$POSTGRES_ID" sh -euc \
  'exec pg_dump --format=custom --no-owner --no-acl --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' 2>/dev/null \
  | openssl enc -aes-256-cbc -salt -pbkdf2 -iter 200000 -pass "file:$STATE_DIR/backup.pass" \
    -out "$STATE_DIR/database.dump.enc" 2>/dev/null
[[ -s "$STATE_DIR/database.dump.enc" ]] || fail 'encrypted database backup is empty'
sha256sum "$STATE_DIR/database.dump.enc" >"$STATE_DIR/database.dump.enc.sha256"

PHASE=isolated_restore
RESTORE_NAME="fv-admin-unseen-restore-$(date -u +%s)-$(openssl rand -hex 6)"
TMPFS_BYTES=$(( DB_BYTES * 4 + 134217728 ))
(( TMPFS_BYTES >= 268435456 )) || TMPFS_BYTES=268435456
MEMORY_BYTES=$(( TMPFS_BYTES + 268435456 ))
AVAILABLE_BYTES=$(awk '/^MemAvailable:/ {printf "%.0f\n", $2 * 1024}' /proc/meminfo)
(( AVAILABLE_BYTES > MEMORY_BYTES + 134217728 )) || fail 'insufficient memory for isolated tmpfs restore'
RESTORE_ID=$(docker create --name "$RESTORE_NAME" --pull never --network none --read-only \
  --label "com.alex-taro.restore-run=$RESTORE_NAME" \
  --memory "$MEMORY_BYTES" --memory-swap "$MEMORY_BYTES" \
  --tmpfs "/var/lib/postgresql/data:rw,nosuid,nodev,size=$TMPFS_BYTES,mode=0755" \
  --tmpfs /var/run/postgresql:rw,nosuid,nodev,size=16777216,mode=0777 \
  --tmpfs /tmp:rw,nosuid,nodev,size=16777216,mode=1777 \
  -e PGDATA=/var/lib/postgresql/data/pgdata -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=restore_verify \
  "$PG_IMAGE" 2>/dev/null)
docker start "$RESTORE_ID" >/dev/null 2>&1
for attempt in $(seq 1 40); do
  if docker exec "$RESTORE_ID" pg_isready -h 127.0.0.1 -U postgres -d restore_verify >/dev/null 2>&1; then break; fi
  sleep 1
done
docker exec "$RESTORE_ID" pg_isready -h 127.0.0.1 -U postgres -d restore_verify >/dev/null 2>&1 || fail 'isolated PostgreSQL did not become ready'
openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -pass "file:$STATE_DIR/backup.pass" \
  -in "$STATE_DIR/database.dump.enc" 2>/dev/null \
  | docker exec -i "$RESTORE_ID" pg_restore --exit-on-error --no-owner --no-acl -U postgres -d restore_verify >/dev/null 2>&1
printf '%s\n' 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' \
  | docker exec -i "$RESTORE_ID" psql -X -qAt -v ON_ERROR_STOP=1 -U postgres -d restore_verify \
    >"$STATE_DIR/migrations.restored" 2>/dev/null
cmp -s "$STATE_DIR/migrations.before" "$STATE_DIR/migrations.restored" || fail 'restored migration history differs'
printf '%s\n' "SELECT json_build_object('public_tables',(SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'),'readings',(SELECT count(*) FROM readings),'users',(SELECT count(*) FROM users));" \
  | docker exec -i "$RESTORE_ID" psql -X -qAt -v ON_ERROR_STOP=1 -U postgres -d restore_verify \
    >"$STATE_DIR/restore-aggregates.json" 2>/dev/null
python3 - "$STATE_DIR/restore-aggregates.json" <<'PY'
import json,sys
v=json.load(open(sys.argv[1])); assert v['public_tables']>0 and v['readings']>=0 and v['users']>=0
PY
PHASE=isolated_migration_rehearsal
retained_counts_sql | restored_sql >"$STATE_DIR/retained-counts.before"
[[ -s "$STATE_DIR/retained-counts.before" ]] || fail 'retained table counts are missing'
restored_sql <"$STATE_DIR/migration.sql" >"$STATE_DIR/migration-rehearsal.log"
retained_counts_sql | restored_sql >"$STATE_DIR/retained-counts.after"
cmp -s "$STATE_DIR/retained-counts.before" "$STATE_DIR/retained-counts.after" || fail 'migration changed retained table counts'
printf '%s\n' 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' \
  | restored_sql >"$STATE_DIR/migrations.rehearsed"
cmp -s "$STATE_DIR/migrations.release" "$STATE_DIR/migrations.rehearsed" || fail 'rehearsed migration history differs'
review_state_sql | restored_sql >"$STATE_DIR/review.rehearsed.json"
verify_review_state "$STATE_DIR/review.rehearsed.json" || fail 'isolated admin reading views verification failed'
cleanup_restore || fail 'isolated restore container cleanup failed'
verify_snapshot "$OLD_BACKEND_ID" "$OLD_FRONTEND_ID" >/dev/null 2>&1 || fail 'production resources changed during backup'

PHASE=checkout
[[ $(git_deploy rev-parse HEAD) == "$OLD_REV" \
  && $(git_deploy status --porcelain --untracked-files=all) == '?? deploy/Caddyfile.bak-20260708215835' ]] || fail 'checkout changed during backup'
GIT_UPDATED=1
git_deploy merge --ff-only "$RELEASE" >/dev/null 2>&1
[[ $(git_deploy rev-parse HEAD) == "$RELEASE" ]] || fail 'checkout did not reach the release'
cmp -s "$STATE_DIR/env.before" "$ENV_FILE" || fail 'environment changed during backup'
PHASE=environment
ENV_UPDATED=1
python3 - "$ENV_FILE" "$RELEASE" "$STATE_DIR/env.before" <<'PY'
import os,re,stat,sys,tempfile
path,release,previous=sys.argv[1:]
data=open(path,'rb').read(); assert data==open(previous,'rb').read()
info=os.stat(path,follow_symlinks=False); assert stat.S_ISREG(info.st_mode) and stat.S_IMODE(info.st_mode)==0o600 and info.st_uid==info.st_gid==0
lines=data.splitlines(keepends=True); matched=[]
for i,line in enumerate(lines):
    if not re.match(rb'[ \t]*(?:export[ \t]+)?FV_IMAGE_TAG[ \t]*=',line): continue
    match=re.fullmatch(rb'([ \t]*(?:export[ \t]+)?FV_IMAGE_TAG[ \t]*=[ \t]*)(["\']?)([A-Za-z0-9_.-]+)\2([ \t]*(?:#[^\r\n]*)?)(\r\n|\n|\r)?',line)
    assert match, 'Unexpected image tag assignment format'
    matched.append(i)
    lines[i]=match.group(1)+match.group(2)+release.encode()+match.group(2)+match.group(4)+(match.group(5) or b'')
assert len(matched)==1, 'Expected exactly one existing image tag assignment'
fd,temp=tempfile.mkstemp(prefix='.env.retention-',dir=os.path.dirname(path))
try:
    os.fchmod(fd,0o600); os.fchown(fd,0,0)
    with os.fdopen(fd,'wb') as f: f.write(b''.join(lines)); f.flush(); os.fsync(f.fileno())
    os.replace(temp,path)
finally:
    if os.path.exists(temp): os.unlink(temp)
PY
compose config --quiet >/dev/null 2>&1
migrations >"$STATE_DIR/migrations.pre-restart"
cmp -s "$STATE_DIR/migrations.before" "$STATE_DIR/migrations.pre-restart" || fail 'database migrations changed before restart'
cat >"$STATE_DIR/RECOVERY.txt" <<'RECOVERY'
This release adds personal admin view receipts. All existing content and configuration are preserved.
Encrypted backup was restored in isolation and the exact migration rehearsed.
On failure, roll back the application images using rollback-images.yml and env.before; keep the additive table and live data.
Do not restore a backup over live production without preserving subsequent activity.
RECOVERY
PHASE=application_update
APP_UPDATE_STARTED=1
compose up -d --no-deps --no-build --pull never backend frontend >/dev/null 2>&1
PHASE=verification
health || fail 'production health did not recover'
verify_snapshot "$NEW_BACKEND_ID" "$NEW_FRONTEND_ID" >/dev/null 2>&1 || fail 'image, persistence, or neighbor verification failed'
docker exec "$(service_id frontend)" sh -ec 'grep -rq "/api/readings/history/summary" /usr/share/nginx/html/assets' >/dev/null 2>&1 \
  || fail 'frontend compact history endpoint marker was not found'
docker exec "$(service_id frontend)" sh -ec 'grep -rq "Куплено:" /usr/share/nginx/html/assets' >/dev/null 2>&1 \
  || fail 'frontend purchased subscription label was not found'
docker exec "$(service_id frontend)" sh -ec 'grep -rq "Подписчики" /usr/share/nginx/html/assets' >/dev/null 2>&1 \
  || fail 'frontend subscriber audience filter was not found'
[[ $(curl --silent --max-time 15 --output /dev/null --write-out '%{http_code}' https://alex-taro.ru/api/admin/readings/00000000-0000-0000-0000-000000000000/interpretation) == 401 ]] \
  || fail 'new admin answer endpoint is not available with authorization'
[[ $(curl --silent --max-time 15 --output /dev/null --write-out '%{http_code}' https://alex-taro.ru/api/readings/history/summary) == 401 ]] \
  || fail 'new compact history endpoint is not available with authorization'
migrations >"$STATE_DIR/migrations.after"
cmp -s "$STATE_DIR/migrations.release" "$STATE_DIR/migrations.after" || fail 'database migration history differs from reviewed release'
review_state_sql | live_sql >"$STATE_DIR/review.production.json"
verify_review_state "$STATE_DIR/review.production.json" || fail 'production admin reading views verification failed'
docker exec "$(service_id frontend)" sh -ec 'grep -rq "Непросмотренные" /usr/share/nginx/html/assets && grep -rq "Сначала старые" /usr/share/nginx/html/assets && grep -rq "/api/admin/readings/viewed" /usr/share/nginx/html/assets' >/dev/null 2>&1 \
  || fail 'new admin filters or acknowledgment endpoint missing from frontend'
[[ $(curl --silent --max-time 15 --request POST --header 'Content-Type: application/json' --data '{"readingIds":[]}' --output /dev/null --write-out '%{http_code}' https://alex-taro.ru/api/admin/readings/viewed) == 401 ]] \
  || fail 'admin view acknowledgment endpoint is not protected'
docker cp "$CADDY_ID:/etc/caddy/Caddyfile" "$STATE_DIR/Caddyfile.after" >/dev/null 2>&1
cmp -s "$STATE_DIR/Caddyfile.before" "$STATE_DIR/Caddyfile.after" || fail 'shared Caddy configuration changed'
[[ $(stat -c '%u:%g:%a' "$ENV_FILE") == 0:0:600 ]] || fail 'environment permissions changed'
[[ $(git_deploy rev-parse HEAD) == "$RELEASE" && $(git_deploy rev-parse origin/main) == "$RELEASE" ]] || fail 'unexpected final Git revision'
compose ps --format json >"$STATE_DIR/compose-status.json"
env FV_SHARED_EDGE_PORT=18080 docker compose --env-file "$ENV_FILE" \
  -f "$CHECKOUT/docker-compose.prod.yml" -p future-viewer ps >"$STATE_DIR/compose-base-status.txt"
printf 'backend_image=%s\nfrontend_image=%s\nbackup_restore=verified\nmigration_rehearsal=verified\nretained_table_counts=unchanged_in_rehearsal\nhealth=passed\nmigrations=admin_reading_views_applied\nneighbors_mounts_networks=unchanged\n' \
  "$NEW_BACKEND_ID" "$NEW_FRONTEND_ID" >"$STATE_DIR/verification"
PHASE=complete
