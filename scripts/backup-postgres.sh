#!/usr/bin/env bash
set -Eeuo pipefail

APP_DIR="${FV_APP_DIR:-/opt/fv-app}"
ENV_FILE="${FV_ENV_FILE:-/opt/fv-app/.env.production}"
BACKUP_DIR="${FV_BACKUP_DIR:-/opt/fv-app/backups/future-viewer}"
PROJECT_NAME="${FV_COMPOSE_PROJECT_NAME:-future-viewer}"
AGE_RECIPIENT="${FV_BACKUP_AGE_RECIPIENT:-}"
RETENTION_DAYS="${FV_BACKUP_RETENTION_DAYS:-}"

fail() {
  printf 'BACKUP FAILED: %s\n' "$*" >&2
  exit 1
}

[[ "$APP_DIR" == "/opt/fv-app" ]] || fail "APP_DIR must be the dedicated /opt/fv-app directory"
[[ "$BACKUP_DIR" == "/opt/fv-app/backups/future-viewer" ]] || fail "BACKUP_DIR must be the dedicated future-viewer backup directory"
[[ "$PROJECT_NAME" == "future-viewer" ]] || fail "unexpected Compose project name"
[[ -f "$ENV_FILE" ]] || fail "$ENV_FILE does not exist"
[[ "$AGE_RECIPIENT" == age1* ]] || fail "FV_BACKUP_AGE_RECIPIENT must be an approved age public recipient"
[[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]] || fail "FV_BACKUP_RETENTION_DAYS must be an approved positive integer"
(( RETENTION_DAYS > 0 )) || fail "FV_BACKUP_RETENTION_DAYS must be positive"
command -v age >/dev/null 2>&1 || fail "age is not installed"
command -v docker >/dev/null 2>&1 || fail "docker is not installed"

install -d -m 700 "$BACKUP_DIR"
cd "$APP_DIR"

timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
final_path="$BACKUP_DIR/future-viewer-postgres-$timestamp.dump.age"
temporary_path="$BACKUP_DIR/.future-viewer-postgres-$timestamp.dump.age.partial"
umask 077

cleanup_partial() {
  if [[ -f "$temporary_path" ]]; then
    rm -f -- "$temporary_path"
  fi
}
trap cleanup_partial EXIT

docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml -p "$PROJECT_NAME" \
  exec -T postgres sh -euc 'pg_dump --format=custom --no-owner --no-acl --dbname="$POSTGRES_DB" --username="$POSTGRES_USER"' \
  | age --recipient "$AGE_RECIPIENT" --output "$temporary_path"

[[ -s "$temporary_path" ]] || fail "encrypted backup is empty"
mv -- "$temporary_path" "$final_path"
sha256sum "$final_path" > "$final_path.sha256"
chmod 600 "$final_path" "$final_path.sha256"

if [[ "${FV_ENABLE_BACKUP_ROTATION:-false}" == "true" ]]; then
  find "$BACKUP_DIR" -xdev -type f \
    \( -name 'future-viewer-postgres-*.dump.age' -o -name 'future-viewer-postgres-*.dump.age.sha256' \) \
    -mtime "+$RETENTION_DAYS" -delete
fi

printf 'Encrypted backup created: %s\n' "$final_path"
printf 'Run scripts/restore-test-postgres.sh against this file before treating the backup as verified.\n'
