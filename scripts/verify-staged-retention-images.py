#!/usr/bin/env python3
"""Verify staged image configurations and emit their server-local Docker IDs."""
import hashlib
import json
import re
import subprocess
import sys
import tarfile
import tempfile

RELEASE = sys.argv[1] if len(sys.argv) == 4 else ''
if not re.fullmatch(r'[0-9a-f]{40}', RELEASE) or not all(re.fullmatch(r'sha256:[0-9a-f]{64}', v) for v in sys.argv[2:]):
    sys.exit('Expected pinned release and two archive configuration digests')
EXPECTED_CONFIGS = {'backend': sys.argv[2][7:], 'frontend': sys.argv[3][7:]}
TAGS = {role: f'future-viewer-{role}:{RELEASE}' for role in EXPECTED_CONFIGS}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def image_ids():
    result = {}
    for role, tag in TAGS.items():
        value = subprocess.check_output(
            ["docker", "image", "inspect", "--format", "{{.Id}}", tag],
            stderr=subprocess.DEVNULL, text=True,
        ).strip()
        require(re.fullmatch(r"sha256:[0-9a-f]{64}", value), "Invalid Docker image ID")
        result[role] = value
    return result


def verify():
    before = image_ids()
    # TemporaryFile is private (0600), automatically removed, and contains images only.
    with tempfile.TemporaryFile(mode="w+b") as saved:
        subprocess.run(
            ["docker", "image", "save", *TAGS.values()], stdout=saved,
            stderr=subprocess.DEVNULL, check=True,
        )
        require(image_ids() == before, "Image tags changed during export")
        saved.seek(0)
        with tarfile.open(fileobj=saved, mode="r:*") as archive:
            manifest_file = archive.extractfile("manifest.json")
            require(manifest_file is not None, "Missing Docker archive manifest")
            manifest = json.load(manifest_file)
            archived_tags = [tag for item in manifest for tag in (item.get("RepoTags") or [])]
            require(len(archived_tags) == len(set(archived_tags)), "Duplicate archived image tags")
            for role, tag in TAGS.items():
                matches = [item for item in manifest if tag in (item.get("RepoTags") or [])]
                require(len(matches) == 1, "Expected one archived image per release tag")
                config_file = archive.extractfile(matches[0]["Config"])
                require(config_file is not None, "Missing archived image configuration")
                config = config_file.read()
                actual_digest = hashlib.sha256(config).hexdigest()
                expected_digest = EXPECTED_CONFIGS[role]
                if actual_digest != expected_digest:
                    print(f"{role} config SHA256: actual={actual_digest} expected={expected_digest}",
                          file=sys.stderr)
                    raise ValueError("Staged image configuration differs from the verified delivery")
                parsed = json.loads(config)
                require(parsed.get("architecture") == "amd64" and parsed.get("os") == "linux",
                        "Unexpected staged image platform")
    require(image_ids() == before, "Image tags changed during verification")
    return before


if __name__ == "__main__":
    try:
        verified = verify()
    except Exception:
        print("Staged retention image verification failed; deployment must stop.", file=sys.stderr)
        sys.exit(1)
    print(json.dumps(verified, sort_keys=True))
