"""Retire the birthday feature and purge only its private server materials."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

REPO = Path("/opt/future-viewer")
ENV_FILE = Path("/opt/fv-app/.env.production")
PROJECT = "future-viewer"
APP_NAMES = {"future-viewer-backend-1", "future-viewer-frontend-1"}
BASE = "2322577ea269b2a07563b1a62ef373bac0869110"


def without_birthday(text):
    return "".join(line for line in text.splitlines(keepends=True)
                   if not re.match(r"^\s*Birthday__", line, re.I))


def run(args, env=None):
    result = subprocess.run(args, cwd=REPO, env=env, capture_output=True,
                            text=True, timeout=180)
    if result.returncode:
        raise RuntimeError(f"{args[0]} command failed ({result.returncode}); output withheld")
    return result.stdout.strip()


def inspect(name):
    return json.loads(run(["docker", "inspect", name]))[0]


def other_containers():
    return {
        line.split()[0]: line.split()[1]
        for line in run(["docker", "ps", "-a", "--no-trunc",
                         "--format", "{{.Names}} {{.ID}}"]).splitlines()
        if line.split()[0] not in APP_NAMES
    }


def mounts(container, omit_private=False):
    return sorted((m["Type"], m["Source"], m["Destination"], m["RW"])
                  for m in container["Mounts"]
                  if not (omit_private and m["Destination"] == "/app/private-birthday"))


def request_status(path, post=False):
    req = urllib.request.Request(
        "https://alex-taro.ru" + path,
        data=b'{"key":"retired","ticket":"retired"}' if post else None,
        headers={"Content-Type": "application/json"} if post else {})
    try:
        with urllib.request.urlopen(req, timeout=8) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code


def healthy():
    for _ in range(30):
        try:
            with urllib.request.urlopen("https://alex-taro.ru/health", timeout=5) as response:
                data = json.load(response)
            if data.get("status") == "ok" and data.get("database") == "ready":
                return
        except Exception:
            pass
        time.sleep(2)
    raise RuntimeError("Production health verification failed")


def purge_materials():
    # Exact feature-owned locations; never traverse another site's data.
    for directory in [Path("/opt/fv-app/birthday"), Path("/tmp/fv-birthday-d94a73c6")]:
        if directory.is_symlink():
            raise RuntimeError("Unexpected symlink in birthday storage")
        if directory.exists():
            shutil.rmtree(directory)
        if directory.exists():
            raise RuntimeError("Birthday storage cleanup failed")
    # Preserve unrelated rollback credentials, removing only retired settings.
    for directory in Path("/opt/fv-app/releases").glob("birthday-*"):
        if directory.is_symlink():
            raise RuntimeError("Unexpected snapshot symlink")
        for file in directory.glob("env*"):
            if not file.is_file() or file.is_symlink():
                continue
            text = file.read_text()
            cleaned = without_birthday(text)
            if cleaned != text:
                file.write_text(cleaned)
                file.chmod(0o600)
        result = directory / "result.json"
        if result.is_file() and not result.is_symlink():
            data = json.loads(result.read_text())
            if "new_hash" in data:
                del data["new_hash"]
                data["birthday_retired"] = True
                result.write_text(json.dumps(data))
    if any(re.match(r"^\s*Birthday__", line, re.I) for line in ENV_FILE.read_text().splitlines()):
        raise RuntimeError("Retired birthday settings remain")


def main(sha, branch):
    if os.geteuid() != 0 or not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise RuntimeError("Invalid deployment context")
    if not re.fullmatch(r"codex/remove-birthday-[a-z0-9-]+", branch):
        raise RuntimeError("Unexpected maintenance branch")
    healthy()
    before = {name: inspect(name) for name in APP_NAMES}
    others = other_containers()
    previous = run(["git", "-c", f"safe.directory={REPO}", "rev-parse", "HEAD"])
    original = ENV_FILE.read_text()
    env_stat = ENV_FILE.stat()
    edge = inspect("future-viewer-caddy-1")["NetworkSettings"]["Networks"]["future-viewer_default"]["IPAddress"]
    run(["git", "-c", f"safe.directory={REPO}", "diff", "--quiet"])
    run(["git", "-c", f"safe.directory={REPO}", "diff", "--cached", "--quiet"])
    run(["sudo", "-u", "deploy", "git", "fetch", "origin", branch, "main"])
    if run(["git", "-c", f"safe.directory={REPO}", "rev-parse", "origin/" + branch]) != sha:
        raise RuntimeError("Maintenance branch advanced")
    if run(["git", "-c", f"safe.directory={REPO}", "rev-parse", "origin/main"]) != BASE:
        raise RuntimeError("Main advanced; review concurrent changes before deploying")
    run(["git", "-c", f"safe.directory={REPO}", "merge-base", "--is-ancestor", previous, sha])
    if run(["git", "-c", f"safe.directory={REPO}", "diff", "--name-only", previous, sha,
            "--", "backend/src/FutureViewer.Infrastructure/Persistence/Migrations"]):
        raise RuntimeError("Unexpected database migration")

    stage = Path("/tmp") / ("fv-remove-birthday-" + sha[:12])
    expected = (stage / "images.sha256").read_text().split()[0]
    digest = hashlib.sha256()
    with (stage / "images.tar.gz").open("rb") as archive:
        for chunk in iter(lambda: archive.read(1024 * 1024), b""):
            digest.update(chunk)
    if digest.hexdigest() != expected:
        raise RuntimeError("Image archive checksum mismatch")
    run(["docker", "load", "-i", str(stage / "images.tar.gz")])
    for service in ["backend", "frontend"]:
        if run(["docker", "image", "inspect", f"future-viewer-{service}:{sha}",
                "--format", "{{.Architecture}}"]) != "amd64":
            raise RuntimeError("Unexpected image architecture")

    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = Path("/opt/fv-app/releases") / ("birthday-retirement-" + stamp)
    backup.mkdir(mode=0o700)
    shutil.copy2(ENV_FILE, backup / "env.before")
    (backup / "env.before").chmod(0o600)
    (backup / "before.json").write_text(json.dumps({
        "revision": previous, "images": {k: v["Config"]["Image"] for k, v in before.items()}
    }))
    rollback_images = backup / "rollback-images.json"
    rollback_images.write_text(json.dumps({"services": {
        service: {"image": before[f"future-viewer-{service}-1"]["Config"]["Image"]}
        for service in ["backend", "frontend"]
    }}))
    candidate = backup / "env.candidate"
    candidate.write_text(without_birthday(original))
    candidate.chmod(0o600)
    os.chown(candidate, env_stat.st_uid, env_stat.st_gid)
    environment = dict(os.environ, FV_IMAGE_TAG=sha, FV_SHARED_EDGE_PORT="18080",
                       FV_LEGACY_EDGE_IP=edge, FV_ENV_FILE=str(ENV_FILE))
    compose = ["docker", "compose", "--env-file", str(ENV_FILE),
               "-f", "docker-compose.prod.yml", "-f", "docker-compose.prod.prebuilt.yml",
               "-f", "docker-compose.prod.legacy-edge.yml", "-p", PROJECT]
    try:
        run(["sudo", "-u", "deploy", "git", "merge", "--ff-only", sha])
        if ENV_FILE.read_text() != original:
            raise RuntimeError("Production environment changed during preflight")
        os.replace(candidate, ENV_FILE)
        model = json.loads(run(compose + ["config", "--format", "json"], environment))
        desired_env = model["services"]["backend"]["environment"]
        old_env = dict(value.split("=", 1) for value in before["future-viewer-backend-1"]["Config"]["Env"])
        if any(str(value) != old_env.get(key) for key, value in desired_env.items()):
            raise RuntimeError("Unrelated runtime configuration drift")
        if any(key.lower().startswith("birthday__") for key in desired_env):
            raise RuntimeError("Retired key configuration still present")
        run(compose + ["up", "-d", "--no-deps", "--no-build", "--pull", "never",
                       "backend", "frontend"], environment)
        healthy()
        if request_status("/") != 200:
            raise RuntimeError("Main site is unavailable")
        for path in ["/birthday", "/birthday/", "/birthday/old"]:
            if request_status(path) != 410:
                raise RuntimeError("Retired page remains reachable")
        for path in ["/api/birthday/unlock", "/api/birthday/session", "/api/birthday/answer",
                     "/api/birthday/gift", "/api/birthday/assets/certificate", "/api/birthday/assets/postcard"]:
            if request_status(path, post=True) != 410:
                raise RuntimeError("A retired API remains reachable")
        for service in ["backend", "frontend"]:
            name = f"future-viewer-{service}-1"
            after = inspect(name)
            if after["Config"]["Image"] != f"future-viewer-{service}:{sha}":
                raise RuntimeError("Running image differs from release")
            if mounts(after) != mounts(before[name], omit_private=True):
                raise RuntimeError("Unrelated application mounts changed")
        if other_containers() != others:
            raise RuntimeError("Unrelated container identities changed")
        run(["docker", "compose", "--env-file", str(ENV_FILE), "-f",
             "docker-compose.prod.yml", "-p", PROJECT, "ps"], environment)
    except BaseException:
        run(["sudo", "-u", "deploy", "git", "switch", "--detach", previous])
        restore = backup / "env.restore"
        shutil.copy2(backup / "env.before", restore)
        restore.chmod(0o600)
        os.chown(restore, env_stat.st_uid, env_stat.st_gid)
        os.replace(restore, ENV_FILE)
        rollback = compose[:-2] + ["-f", "docker-compose.prod.birthday.yml",
                                  "-f", str(rollback_images)] + compose[-2:]
        run(rollback + ["up", "-d", "--no-deps", "--no-build", "--pull", "never",
                        "backend", "frontend"], environment)
        healthy()
        print("Application verification failed; previous release and private files retained.")
        raise
    # Irreversible gift-file deletion happens only after the retired release is healthy.
    purge_materials()
    shutil.rmtree(stage)
    (backup / "retired-sha").write_text(sha + "\n")
    print("Birthday game retired: page/API return 410; gift files and key settings removed; main site healthy.")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
