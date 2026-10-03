#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_NAME="${FV_COMPOSE_PROJECT_NAME:-future-viewer}"
OTHER_PROJECT_NAME="${FV_OTHER_COMPOSE_PROJECT_NAME:-janetka}"
ENV_FILE="${FV_ENV_FILE:-/opt/fv-app/.env.production}"
REQUIRE_OTHER_PROJECT="${FV_REQUIRE_OTHER_PROJECT:-false}"

fail() {
  printf 'ISOLATION CHECK FAILED: %s\n' "$*" >&2
  exit 1
}

container_env_value() {
  local container_id="$1"
  local key="$2"
  docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$container_id" \
    | awk -F= -v wanted="$key" '$1 == wanted { sub(/^[^=]*=/, ""); print; exit }'
}

[[ "$PROJECT_NAME" == "future-viewer" ]] || fail "production Compose project must be named future-viewer"
[[ "$OTHER_PROJECT_NAME" != "$PROJECT_NAME" ]] || fail "other project name must differ"
command -v docker >/dev/null 2>&1 || fail "docker is unavailable"

mapfile -t project_containers < <(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT_NAME")
(( ${#project_containers[@]} > 0 )) || fail "no running or stopped containers found for $PROJECT_NAME"

mapfile -t other_containers < <(docker ps -aq --filter "label=com.docker.compose.project=$OTHER_PROJECT_NAME")
if (( ${#other_containers[@]} == 0 )) && [[ "$REQUIRE_OTHER_PROJECT" == "true" ]]; then
  fail "no containers found for required comparison project $OTHER_PROJECT_NAME"
fi

declare -A project_networks=()
declare -A project_volumes=()
for container_id in "${project_containers[@]}"; do
  actual_project="$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$container_id")"
  [[ "$actual_project" == "$PROJECT_NAME" ]] || fail "container has an unexpected Compose project label"

  while read -r host_ip host_port container_port; do
    [[ -z "${host_ip:-}" ]] && continue
    [[ "$host_ip" == "127.0.0.1" || "$host_ip" == "::1" ]] \
      || fail "container publishes $container_port on non-loopback address $host_ip:$host_port"
    [[ "$host_port" != "80" && "$host_port" != "443" ]] \
      || fail "project owns shared host port $host_port"
  done < <(docker inspect --format '{{range $port, $bindings := .NetworkSettings.Ports}}{{range $bindings}}{{println .HostIp .HostPort $port}}{{end}}{{end}}' "$container_id")

  while read -r network_name; do
    [[ -z "${network_name:-}" ]] && continue
    [[ "$network_name" != *"$OTHER_PROJECT_NAME"* ]] \
      || fail "container joined an other-project network"
    project_networks["$network_name"]=1
  done < <(docker inspect --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' "$container_id")

  while IFS='|' read -r mount_type mount_source mount_name; do
    [[ -z "${mount_type:-}" ]] && continue
    [[ "$mount_source" != *"$OTHER_PROJECT_NAME"* && "$mount_name" != *"$OTHER_PROJECT_NAME"* ]] \
      || fail "container mounts storage belonging to $OTHER_PROJECT_NAME"
    if [[ "$mount_type" == "volume" && -n "$mount_name" ]]; then
      project_volumes["$mount_name"]=1
    fi
  done < <(docker inspect --format '{{range .Mounts}}{{println .Type "|" .Source "|" .Name}}{{end}}' "$container_id")
done

for container_id in "${other_containers[@]}"; do
  while read -r network_name; do
    [[ -z "${network_name:-}" ]] && continue
    [[ -z "${project_networks[$network_name]:-}" ]] \
      || fail "projects share Docker network $network_name"
  done < <(docker inspect --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' "$container_id")

  while IFS='|' read -r mount_type mount_source mount_name; do
    [[ "$mount_type" != "volume" || -z "$mount_name" ]] && continue
    [[ -z "${project_volumes[$mount_name]:-}" ]] \
      || fail "projects share Docker volume $mount_name"
  done < <(docker inspect --format '{{range .Mounts}}{{println .Type "|" .Source "|" .Name}}{{end}}' "$container_id")
done

# Compare effective credentials without printing them. Equal values would make a
# configuration mistake more likely to bridge the two projects after a network or
# proxy error, so production deployment treats reuse as a blocker.
for key in POSTGRES_USER POSTGRES_PASSWORD POSTGRES_DB Jwt__Secret JWT_SECRET; do
  project_value=""
  other_value=""
  for container_id in "${project_containers[@]}"; do
    value="$(container_env_value "$container_id" "$key")"
    [[ -n "$value" ]] && project_value="$value" && break
  done
  for container_id in "${other_containers[@]}"; do
    value="$(container_env_value "$container_id" "$key")"
    [[ -n "$value" ]] && other_value="$value" && break
  done
  if [[ -n "$project_value" && -n "$other_value" && "$project_value" == "$other_value" ]]; then
    fail "$key is reused across Compose projects"
  fi
done

if [[ -f "$ENV_FILE" ]]; then
  env_mode="$(stat -c '%a' "$ENV_FILE")"
  [[ "$env_mode" == "600" || "$env_mode" == "400" ]] \
    || fail "$ENV_FILE must have mode 600 or 400 (actual: $env_mode)"
  env_realpath="$(realpath "$ENV_FILE")"
  [[ "$env_realpath" == /opt/fv-app/* && "$env_realpath" != *"$OTHER_PROJECT_NAME"* ]] \
    || fail "future-viewer env file is outside its dedicated /opt/fv-app directory"
else
  fail "$ENV_FILE does not exist"
fi

printf 'Project isolation check passed for %s' "$PROJECT_NAME"
if (( ${#other_containers[@]} > 0 )); then
  printf ' against %s' "$OTHER_PROJECT_NAME"
else
  printf '; %s was not running, so cross-project comparisons were skipped' "$OTHER_PROJECT_NAME"
fi
printf '.\n'
