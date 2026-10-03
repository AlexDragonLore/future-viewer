#!/usr/bin/env bash
set -Eeuo pipefail

BACKUP_PATH="${1:-}"
AGE_IDENTITY_FILE="${FV_BACKUP_AGE_IDENTITY_FILE:-}"
TEST_PASSWORD="restore-test-only-$(date +%s)-$RANDOM"
TEST_CONTAINER="future-viewer-restore-test-$(date +%s)-$RANDOM"

fail() {
  printf 'RESTORE TEST FAILED: %s\n' "$*" >&2
  exit 1
}

[[ -n "$BACKUP_PATH" && -f "$BACKUP_PATH" ]] || fail "pass an encrypted .dump.age backup file"
[[ "$BACKUP_PATH" == /opt/fv-app/backups/future-viewer/* ]] || fail "backup must be in the dedicated future-viewer backup directory"
[[ -n "$AGE_IDENTITY_FILE" && -f "$AGE_IDENTITY_FILE" ]] || fail "FV_BACKUP_AGE_IDENTITY_FILE is required"
command -v age >/dev/null 2>&1 || fail "age is not installed"
command -v docker >/dev/null 2>&1 || fail "docker is not installed"

cleanup() {
  docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run -d --name "$TEST_CONTAINER" --network none \
  --label com.alex-taro.purpose=isolated-restore-test \
  -e POSTGRES_PASSWORD="$TEST_PASSWORD" \
  -e POSTGRES_DB=restore_test \
  postgres:17-alpine >/dev/null

for _ in $(seq 1 30); do
  if docker exec "$TEST_CONTAINER" pg_isready --username=postgres --dbname=restore_test >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$TEST_CONTAINER" pg_isready --username=postgres --dbname=restore_test >/dev/null \
  || fail "isolated restore database did not become ready"

age --decrypt --identity "$AGE_IDENTITY_FILE" "$BACKUP_PATH" \
  | docker exec -i "$TEST_CONTAINER" pg_restore \
      --exit-on-error --no-owner --no-acl --username=postgres --dbname=restore_test

schema_count="$(docker exec "$TEST_CONTAINER" psql --tuples-only --no-align \
  --username=postgres --dbname=restore_test \
  --command="select count(*) from information_schema.tables where table_schema = 'public';")"
[[ "$schema_count" =~ ^[0-9]+$ && "$schema_count" -gt 0 ]] \
  || fail "restore completed without application tables"

printf 'Restore test passed in isolated temporary container; public table count: %s.\n' "$schema_count"
