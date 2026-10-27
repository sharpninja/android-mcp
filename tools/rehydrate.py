#!/usr/bin/env python3
"""Fetch pinned inputs and rebuild a selected catalog in a separate workspace.

No network or regeneration occurs with `plan`, `doctor`, or `--help`.
Only the explicitly selected command runs. Downloaded files are data, never code.
"""
from __future__ import annotations

import argparse
import base64
import copy
import gzip
import hashlib
import importlib
import importlib.metadata
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile

REPO = Path(__file__).resolve().parents[1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def safe_path(root, relative):
    rel = Path(relative)
    if rel.is_absolute() or ".." in rel.parts or "\\" in str(relative):
        raise ValueError(f"Expected a safe workspace-relative path: {relative}")
    root = Path(root).resolve()
    result = (root / rel).resolve()
    if not result.is_relative_to(root) or result == root:
        raise ValueError(f"Path escapes workspace: {relative}")
    return result


def atomic_bytes(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, prefix=path.name + ".", suffix=".part", delete=False) as stream:
        temporary = Path(stream.name)
        try:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        except BaseException:
            temporary.unlink(missing_ok=True)
            raise
    os.replace(temporary, path)


def atomic_json(path, value):
    atomic_bytes(path, (json.dumps(value, sort_keys=True, indent=2) + "\n").encode())


def load_recipe(path):
    path = Path(path).resolve()
    lock = path.with_name(path.name.replace('.json.gz', '.lock.json'))
    if path.name.endswith('.json.gz') and lock.is_file():
        if file_sha(path) != json.loads(lock.read_text())["sha256"]:
            raise ValueError("Recipe checksum does not match its committed lock")
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as stream:
        recipe = json.load(stream)
    if recipe.get("recipe_version") != 1 or not recipe.get("inventory", {}).get("sources"):
        raise ValueError("Unsupported or empty rehydration recipe")
    for source in recipe["inventory"]["sources"]:
        if not re.fullmatch(r"[a-f0-9]{64}", source.get("sha256", "")):
            raise ValueError("Every input must have a pinned SHA-256")
        safe_path(Path.cwd(), source["local_path"])
        if source.get("metadata_cache"):
            safe_path(Path.cwd(), source["metadata_cache"]["path"])
    return recipe


def verify_source(path, source):
    if not path.is_file():
        return False
    if file_sha(path) != source["sha256"]:
        raise ValueError(f"Existing input differs from its pin; preserve/quarantine it before retrying: {path}")
    if "size_bytes" in source and path.stat().st_size != source["size_bytes"]:
        raise ValueError(f"Pinned source size differs: {path}")
    if source.get("git_blob"):
        digest = hashlib.sha1()
        digest.update(b"blob " + str(path.stat().st_size).encode() + b"\0")
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
        if digest.hexdigest() != source["git_blob"]:
            raise ValueError(f"Git-format content address differs: {path}")
    return True


def open_url(url, *, allow_file=False, headers=None):
    scheme = urllib.parse.urlsplit(url).scheme
    if scheme not in (("https", "file") if allow_file else ("https",)):
        raise ValueError("Only HTTPS is accepted; --allow-file is for explicit local fixtures")
    return urllib.request.urlopen(urllib.request.Request(url, headers=headers or {}), timeout=60)


class RangeReader(io.RawIOBase):
    """Seekable ZIP transport; refuses a server that ignores a bounded Range."""

    def __init__(self, url):
        self.url, self.position = url, 0
        with open_url(url, headers={"Range": "bytes=0-0", "Accept-Encoding": "identity"}) as response:
            match = re.fullmatch(r"bytes 0-0/(\d+)", response.headers.get("Content-Range", ""))
            if response.status != 206 or not match or len(response.read(2)) != 1:
                raise ValueError("Archive server must support exact HTTP byte ranges")
            self.length = int(match[1])

    def seekable(self):
        return True

    def readable(self):
        return True

    def tell(self):
        return self.position

    def seek(self, offset, whence=0):
        self.position = offset if whence == 0 else self.position + offset if whence == 1 else self.length + offset
        if self.position < 0:
            raise ValueError("Negative archive seek")
        return self.position

    def read(self, size=-1):
        if self.position >= self.length:
            return b""
        end = self.length - 1 if size < 0 else min(self.length - 1, self.position + size - 1)
        if end < self.position:
            return b""
        start = self.position
        with open_url(self.url, headers={"Range": f"bytes={start}-{end}", "Accept-Encoding": "identity"}) as response:
            expected = f"bytes {start}-{end}/{self.length}"
            if response.status != 206 or response.headers.get("Content-Range") != expected:
                raise ValueError("Archive range response does not match request")
            data = response.read(end - start + 2)
        if len(data) != end - start + 1:
            raise ValueError("Archive byte range is incomplete or oversized")
        self.position = end + 1
        return data


def archive_member(source, workspace, allow_file):
    transport = source["transport"]
    url = transport["archive_url"]
    expected = transport.get("archive_sha256")
    if expected:
        archive = safe_path(workspace, "archives/" + expected + ".zip")
        if archive.exists() and file_sha(archive) != expected:
            raise ValueError(f"Archive checksum mismatch: {archive}")
        if not archive.exists():
            archive.parent.mkdir(parents=True, exist_ok=True)
            temporary = archive.with_suffix(".zip.part")
            try:
                with open_url(url, allow_file=allow_file) as response, temporary.open("wb") as output:
                    shutil.copyfileobj(response, output, 1024 * 1024)
                if file_sha(temporary) != expected:
                    raise ValueError("Downloaded SDK archive SHA-256 mismatch")
                os.replace(temporary, archive)
            finally:
                temporary.unlink(missing_ok=True)
        handle = archive
    elif urllib.parse.urlsplit(url).scheme == "file" and allow_file:
        handle = Path(urllib.request.url2pathname(urllib.parse.urlsplit(url).path))
    else:
        # Published platform archive hash was not verified by the original run.
        # Retrieve only ZIP metadata and the pinned member; verify the member SHA.
        handle = RangeReader(url)
    try:
        with zipfile.ZipFile(handle) as package:
            return package.read(transport["archive_member"])
    finally:
        if isinstance(handle, RangeReader):
            handle.close()


def acquire(source, workspace, allow_file=False):
    target = safe_path(workspace, source["local_path"])
    if verify_source(target, source):
        return "reused"
    if source.get("transport", {}).get("archive_member"):
        data = archive_member(source, workspace, allow_file)
    else:
        url = source["url"]
        if urllib.parse.urlsplit(url).hostname == "android.googlesource.com":
            url += ("&" if "?" in url else "?") + "format=TEXT"
            with open_url(url, allow_file=allow_file) as response:
                data = base64.b64decode(b"".join(response.read().split()), validate=True)
        else:
            with open_url(url, allow_file=allow_file) as response:
                data = response.read()
    if sha(data) != source["sha256"]:
        raise ValueError(f"Downloaded member SHA-256 mismatch: {source['path']}")
    if source.get("git_blob") and hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest() != source["git_blob"]:
        raise ValueError("Downloaded Git-format content address mismatch")
    if "size_bytes" in source and len(data) != source["size_bytes"]:
        raise ValueError("Downloaded source size mismatch")
    atomic_bytes(target, data)
    return "downloaded"


def producer_path(recipe):
    path = safe_path(REPO, recipe["producer"]["path"])
    for relative, expected in recipe["producer"]["files"].items():
        if file_sha(safe_path(path, relative)) != expected:
            raise ValueError(f"Producer changed: {relative}; select/create a new recipe, never relabel old caches")
    return path


def doctor(recipe):
    producer_path(recipe)
    errors = []
    wanted = recipe.get("environment", {}).get("python_minor")
    if wanted and list(sys.version_info[:2]) != wanted:
        errors.append(f"Producer cache identity requires Python {'.'.join(map(str, wanted))}")
    requirements = recipe.get("environment", {}).get("packages", {})
    if recipe.get('environment', {}).get('expat'):
        import pyexpat
        if pyexpat.EXPAT_VERSION != recipe['environment']['expat']:
            errors.append('Expat version differs; SDK cache identity must not be relabeled')
    for package, version in requirements.items():
        try:
            actual = importlib.metadata.version(package)
        except importlib.metadata.PackageNotFoundError:
            actual = "missing"
        if actual != version:
            errors.append(f"{package}: need {version}, found {actual}")
    if errors:
        raise ValueError("Environment mismatch:\n" + "\n".join(errors))


def inventory_path(workspace):
    return Path(workspace) / "inventory.json"


def load_inventory(recipe, workspace):
    path = inventory_path(workspace)
    if path.exists():
        value = json.loads(path.read_text())
        if value.pop("rehydration_recipe_sha256", None) != recipe_digest(recipe):
            raise ValueError("Workspace belongs to a different recipe; use a separate workspace")
        return value
    return copy.deepcopy(recipe["inventory"])


def recipe_digest(recipe):
    return sha(json.dumps(recipe, sort_keys=True, separators=(",", ":")).encode())


def save_inventory(recipe, workspace, inventory):
    value = dict(inventory, rehydration_recipe_sha256=recipe_digest(recipe))
    atomic_json(inventory_path(workspace), value)


def extract(recipe, workspace, *, limit=None, strict_cache_bytes=False):
    doctor(recipe)
    producer = producer_path(recipe)
    sys.path.insert(0, str(producer / "tooling"))
    fingerprint = importlib.import_module("cache_fingerprint")
    inventory = load_inventory(recipe, workspace)
    done, byte_differences = 0, 0
    for source in inventory["sources"]:
        cache = source.get("metadata_cache")
        if not cache:
            continue
        if limit is not None and done >= limit:
            break
        target = safe_path(workspace, cache["path"])
        if target.exists():
            actual = file_sha(target)
            if actual != cache["sha256"]:
                sidecar = target.with_name(target.name + '.rehydration.json')
                proof = json.loads(sidecar.read_text()) if sidecar.is_file() else {}
                if (strict_cache_bytes or proof.get('recipe_sha256') != recipe_digest(recipe)
                    or proof.get('source_sha256') != source['sha256']
                    or proof.get('parser_sha256') != cache['parser_sha256']
                    or proof.get('historical_cache_sha256') != cache['sha256']
                    or proof.get('cache_sha256') != actual):
                    raise ValueError(f"Existing cache checksum mismatch: {target}")
                cache['historical_cache_sha256'] = cache['sha256']
                cache['sha256'] = actual
            done += 1
            continue
        raw = safe_path(workspace, source["local_path"])
        if not verify_source(raw, source):
            raise ValueError("Run fetch before extract; missing " + source["path"])
        kind = source["kind"]
        actual_fingerprint = fingerprint.current(kind)
        if actual_fingerprint != cache["parser_sha256"]:
            raise ValueError("Producer/library fingerprint differs for " + kind + "; inspect doctor and environment pins")
        if kind == "java-source":
            result = importlib.import_module("java_source").parse(raw.read_bytes(), source["path"])
        elif kind == "sdk-api-index":
            result = importlib.import_module("sdk_api_index").parse(raw.read_bytes(), target=source["platform_api_version"])
        else:
            raise ValueError("Unsupported cache producer: " + kind)
        records = result["records"]
        if len(records) != cache["records"]:
            raise ValueError(f"Declaration count changed for {source['path']}: {len(records)} != {cache['records']}")
        payload = b"".join((json.dumps(record, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode() for record in records)
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + ".tmp")
        try:
            with gzip.GzipFile(filename=str(temporary), mode="wb", mtime=0, compresslevel=6) as output:
                output.write(payload)
            actual = file_sha(temporary)
            if actual != cache["sha256"]:
                if strict_cache_bytes:
                    raise ValueError("Cache encoding bytes differ from historical pin; originals preserved: " + source["path"])
                byte_differences += 1
                atomic_json(target.with_name(target.name + '.rehydration.json'), dict(
                    recipe_sha256=recipe_digest(recipe), source_sha256=source['sha256'],
                    parser_sha256=cache['parser_sha256'], historical_cache_sha256=cache['sha256'],
                    cache_sha256=actual))
                cache["historical_cache_sha256"] = cache["sha256"]
                cache["sha256"] = actual
                cache["rehydration_note"] = "Same pinned producer/count; gzip encoding regenerated. No historical registry hash equivalence claimed."
            os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)
        done += 1
        if done % 100 == 0:
            print(f"cache sources processed: {done}", flush=True)
    save_inventory(recipe, workspace, inventory)
    return dict(processed_cache_sources=done, regenerated_cache_encoding_differences=byte_differences)


def checked_subprocess(arguments):
    subprocess.run([sys.executable, *map(str, arguments)], check=True, env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1"))


def run_stage(recipe, workspace, command, output=None):
    doctor(recipe)
    producer = producer_path(recipe)
    inventory = load_inventory(recipe, workspace)
    for source in inventory["sources"]:
        if not verify_source(safe_path(workspace, source["local_path"]), source):
            raise ValueError("Missing source; run fetch: " + source["path"])
        if source.get("metadata_cache"):
            cache = source["metadata_cache"]
            path = safe_path(workspace, cache["path"])
            if not path.is_file() or file_sha(path) != cache["sha256"]:
                raise ValueError("Missing/mismatched cache; run extract: " + source["path"])
    save_inventory(recipe, workspace, inventory)
    output = Path(output).resolve() if output else safe_path(workspace, "releases/" + recipe["release"])
    receipt = output.parent / (output.name + "." + command + ".json")
    inputs = sha(inventory_path(workspace).read_bytes())
    database = output / "catalog.sqlite"
    if receipt.exists():
        prior = json.loads(receipt.read_text())
        if prior["inventory_sha256"] != inputs or not database.is_file() or prior["database_sha256"] != file_sha(database):
            raise ValueError("Stage output changed; use a new output path, never overwrite an immutable generation")
        for relative, expected in prior.get('result_files', {}).items():
            target = safe_path(output.parent, relative)
            if not target.is_file() or file_sha(target) != expected:
                raise ValueError('Sealed stage result is missing or changed: ' + relative)
        return dict(status="already-complete", receipt=str(receipt))
    output.parent.mkdir(parents=True, exist_ok=True)
    if command == "build":
        if output.exists() and any(output.iterdir()):
            raise ValueError("Unsealed/partial output exists; preserve it and select a new --output path")
        checked_subprocess([producer / "tooling/build_catalog.py", inventory_path(workspace), output])
    elif command == "validate":
        checked_subprocess([producer / "tooling/validate_catalog.py", output, inventory_path(workspace), output.parent / (output.name + ".validation.json")])
    elif command == "export":
        destination = output.parent / (output.name + ".permissions")
        if destination.exists() and any(destination.iterdir()):
            raise ValueError("Permission output exists without a completed receipt; preserve it and choose a new output")
        checked_subprocess([producer / "tooling/export_permissions.py", output, destination])
    results = {}
    if command == 'validate':
        report = output.parent / (output.name + '.validation.json')
        results[report.name] = file_sha(report)
    elif command == 'export':
        results = {str(p.relative_to(output.parent)): file_sha(p) for p in destination.rglob('*') if p.is_file()}
    atomic_json(receipt, dict(status="complete", command=command, inventory_sha256=inputs, recipe_sha256=recipe_digest(recipe), database_sha256=file_sha(database), result_files=results, historical_byte_identity_claimed=False))
    return dict(status="complete", receipt=str(receipt), historical_byte_identity_claimed=False)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=("plan", "doctor", "seed", "fetch", "extract", "build", "validate", "export"))
    parser.add_argument("--release", default="release-011", help="Default current validated selected corpus; historical releases are not silently substituted")
    parser.add_argument("--recipe", type=Path, help="Explicit custom/fixture recipe instead of the pinned default")
    parser.add_argument("--workspace", type=Path, default=REPO / ".rehydration", help="Separate generated-data directory; may be outside the checkout")
    parser.add_argument("--output", type=Path, help="New generation output path (build/validate/export)")
    parser.add_argument("--limit", type=int, help="Optional user-selected number of sources/caches for a smoke run, never a complete-corpus claim")
    parser.add_argument("--delay", type=float, default=1.0, help="Pause between newly fetched sources; default 1 second")
    parser.add_argument("--strict-cache-bytes", action="store_true", help="Reject regenerated gzip-byte differences instead of recording a new derived generation")
    parser.add_argument("--allow-file", action="store_true", help="Allow file:// URLs for explicit local test fixtures only")
    parser.add_argument("--from-dir", type=Path, help="seed: explicit owner-supplied blob/snapshot bundle root; every imported file is verified")
    args = parser.parse_args(argv)
    if args.limit is not None and args.limit < 0 or args.delay < 0:
        parser.error("--limit and --delay must be nonnegative")
    recipe_file = args.recipe or REPO / "rehydration/recipes" / (args.release + ".json.gz")
    if not recipe_file.is_file():
        parser.error("Recipe unavailable. See REHYDRATE.md for historical recovery limits.")
    recipe = load_recipe(recipe_file)
    workspace = args.workspace.resolve()
    if args.command == "plan":
        sources = recipe["inventory"]["sources"]
        unique = {s["local_path"]: s for s in sources}
        snapshots=[dict(path=s['path'],sha256=s['sha256'],bytes=s.get('size_bytes'),bundle_path=s['transport'].get('required_local_snapshot')) for s in sources if s.get('transport',{}).get('immutable_url') is False]
        print(json.dumps(dict(release=recipe["release"],selected_sources=len(sources),unique_source_paths=len(unique),uncompressed_source_bytes=sum(s.get("size_bytes", 0) for s in unique.values()),mutable_upstream_snapshots=snapshots,expected=recipe.get("expected", {}),workspace=str(workspace),network=False,complete=False), indent=2))
    elif args.command == "doctor":
        doctor(recipe)
        print("Pinned producer and extraction dependencies verified; no network or generation performed.")
    elif args.command == 'seed':
        if args.from_dir is None:
            parser.error('seed requires --from-dir containing an explicit trusted snapshot/blob bundle')
        inventory=load_inventory(recipe,workspace)
        save_inventory(recipe,workspace,inventory)
        counts={'imported':0,'reused':0,'absent':0}
        for source in {s['local_path']:s for s in inventory['sources']}.values():
            target=safe_path(workspace,source['local_path'])
            if verify_source(target,source):
                counts['reused']+=1;continue
            choices=[source['local_path']]
            original=source.get('transport',{}).get('required_local_snapshot')
            if original: choices.append(original)
            candidate=next((p for rel in choices if (p:=safe_path(args.from_dir,rel)).is_file()),None)
            if candidate is None:
                counts['absent']+=1;continue
            verify_source(candidate,source)
            atomic_bytes(target,candidate.read_bytes())
            counts['imported']+=1
        print(json.dumps(counts))
    elif args.command == "fetch":
        # A workspace is bound to one recipe before any acquisition.
        inventory = load_inventory(recipe, workspace)
        save_inventory(recipe, workspace, inventory)
        unique = list({s["local_path"]: s for s in inventory["sources"]}.values())
        chosen = unique if args.limit is None else unique[:args.limit]
        counts = {"downloaded": 0, "reused": 0}
        for index, source in enumerate(chosen, 1):
            action = acquire(source, workspace, args.allow_file)
            counts[action] += 1
            if action == "downloaded" and args.delay:
                time.sleep(args.delay)
            if index % 100 == 0:
                print(f"source paths verified: {index}/{len(chosen)}", flush=True)
        receipt = dict(**counts,selected_paths=len(chosen),total_paths=len(unique),complete=len(chosen)==len(unique))
        atomic_json(workspace / 'fetch-receipt.json', receipt)
        print(json.dumps(receipt))
    elif args.command == "extract":
        print(json.dumps(extract(recipe, workspace, limit=args.limit, strict_cache_bytes=args.strict_cache_bytes)))
    else:
        print(json.dumps(run_stage(recipe, workspace, args.command, args.output)))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print(f"rehydration stopped: {error}", file=sys.stderr)
        raise SystemExit(1)
