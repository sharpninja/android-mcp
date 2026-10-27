# Rehydrate omitted data in another environment

The repository intentionally does not contain the source corpus, parser caches, SQLite databases, repeated JSON/JSONL exports, historical migrations or review request/response snapshots. The committed CLI and checksum-locked recipe rebuild the **selected release011 corpus** in a separate directory. They never execute downloaded Android code or operate a device.

## What is reproducible

`rehydration/recipes/release-011.json.gz` retains 45,768 source records with pinned upstream revisions, content SHA-256/Git-format addresses, SDK archive/member pins, parser fingerprints and source-context metadata. It is the one necessary current acquisition recipe, replacing numerous historical inventories. Its adjacent lock checks the compressed recipe. The exact release011 producer source/schema/requirements are retained under `rehydration/producers/release-011`; their hashes are checked before use.

The current reference implementation under `remediation/catalog` includes later work and is tested independently. Rehydration uses the pinned producer, not whichever candidate happens to be current.

Expected selected-corpus checkpoint: 824,994 entries, 45,768 sources, 578 unmatched external annotations and 1,721 exported permission names. **Global completeness is false.** A new build must validate itself; expected counts are a comparison target, not a substitute for running validation. Acquisition availability, byte-equivalent output and Android runtime authority are not promised by a plan or small fixture.

## Prerequisites and clean installation

Use Python 3.14, Expat 2.8.4 and the exact packages in the producer requirements. Python minor, native Expat, tree-sitter libraries and producer file bytes participate in cache identity; mismatches fail rather than relabel old caches. Basic `plan`, `fetch` and `--help` use the standard library; extraction/build require the pinned packages.

```sh
git clone https://github.com/sharpninja/android-mcp.git
cd android-mcp
python3.14 -m venv .venv
# POSIX:
. .venv/bin/activate
# PowerShell alternative: . .venv/Scripts/Activate.ps1
python -m pip install -r rehydration/producers/release-011/requirements-extraction.txt
python tools/rehydrate.py --help
python tools/rehydrate.py plan --workspace ../android-mcp-data
python tools/rehydrate.py doctor --workspace ../android-mcp-data
```

Inspect the plan and available storage before acquisition. The historical011 output directory alone occupied about 20.8GB, including its 10.55GB SQLite and several alternate serializations. Allow additional disk for source archives, blobs, generated caches and temporary files. Full rebuilds are deliberately not part of repository smoke tests.

One input is an **unversioned HTML snapshot** of the official permission reference (2,777,598 bytes, SHA-256 `03c7d0944215660abcfe6ec2a254b1efa282d75dfffe880c3490fde4b3672ad1`). The plan identifies it explicitly. Its live URL may have changed; this file cannot be reconstructed from a Git revision. If the pinned bytes are no longer served, obtain the original snapshot from the owner. Do not substitute current HTML under the old hash. Place it at `permission-reference-source/Manifest.permission.html` under an owner-supplied bundle directory, then seed the new workspace before fetching:

```sh
python tools/rehydrate.py seed --from-dir ../owner-snapshot-bundle --workspace ../android-mcp-data
```

`seed` accepts the recipe's relative blob paths or this documented snapshot path, verifies SHA-256/size/Git-format addresses, copies only matching inputs, and never alters the supplied bundle. It makes no network request. Availability of that historical snapshot is an explicit full-rebuild prerequisite; the fixture tests do not prove upstream availability.

## Acquisition → extraction → generation → validation

These commands are explicit user choices. No installation, network or full regeneration is triggered merely by opening a document or running tests.

```sh
python tools/rehydrate.py fetch --workspace ../android-mcp-data
python tools/rehydrate.py extract --workspace ../android-mcp-data --strict-cache-bytes
python tools/rehydrate.py build --workspace ../android-mcp-data
python tools/rehydrate.py validate --workspace ../android-mcp-data
python tools/rehydrate.py export --workspace ../android-mcp-data
```

Default release is `release-011`. `--recipe PATH` selects an explicit reviewed/custom fixture recipe; unknown historical release names fail rather than falling back. `--output PATH` selects a new build generation. For an intentionally limited acquisition smoke test use `fetch --limit 3`; such a run is not a complete source set and cannot pass a full build preflight. `--delay` defaults to one second between newly retrieved sources. HTTP authentication/rate-limit/network errors stop acquisition; respect the reported Retry-After and rerun later. Never enter credentials into a source recipe.

Gitiles sources are retrieved as data at pinned commits and verified against SHA-256, size and Git-format SHA-1. The SDK source archive has an explicit archive SHA-256 and member hashes. The platform API XML originally used a bounded ZIP-range acquisition without a verified whole-archive hash; the CLI preserves that distinction, uses exact HTTP ranges and verifies the extracted member SHA/size/CRC. A server ignoring Range fails; there is no silent whole-platform download or unverified-member fallback. Only configured HTTPS sources are used. `--allow-file` exists solely for explicit local fixtures.

## Resume, idempotency and immutable generations

- Rerun `fetch` in the same workspace: every existing blob is rehashed and valid files are reused. Downloads are published atomically after verification. A wrong existing file is preserved and causes an error; quarantine it yourself before retrying.
- Rerun `extract`: verified caches are reused. Producer/library fingerprints and declaration counts are checked. Gzip headers/compressor behavior can vary despite identical records. `--strict-cache-bytes` rejects any historical cache-byte difference. Omitting it permits a new derived generation with the difference recorded in cache sidecars/inventory; it does **not** claim the historical registry/file hashes match.
- Successful build/validate/export stages write small receipts pinning recipe, effective inventory and database hashes. Repeating a completed stage verifies those pins and returns `already-complete`. Partial/unsealed outputs are never overwritten: preserve them and pass a new `--output` path.
- Do not mix releases or producer changes in one workspace. A recipe digest binds the workspace. Source files and generated data stay in the selected workspace, which is ignored when using the default `.rehydration` location.

The receipt/inventory timestamp and regenerated gzip encoding may change output identities. The historical published SQLite SHA is a reference, not a promised result of an unexecuted full rebuild. This cleanup verifies the portable workflow on small clean-path fixtures only; it does not claim to have regenerated the production dataset.

## Optional audit/history recovery

Only the current selected release has a committed rehydration recipe. Earlier catalogs, failed staging databases, migration-test copies, original HV conversations and local acquisition/coverage probes are preserved in the owner's original workspaces and older Git checkpoint history where previously committed. They cannot all be reconstructed from upstream source pins: reviews depend on model/service state, transient failures and original receipts; migrated DBs require their exact old input and migration code.

To inspect the earlier publication without changing this checkout, use a separate checkout/worktree at an explicit historical commit. For example, the last broad normal checkpoint is `5eae7e9d67ce4d31392845eae0cb788f1e2bba4c`. It did **not** contain the 144.9GB deferred data. There is no automatic historical bulk download, release-asset upload or 145GB reconstruction mode. Obtain a separately reviewed, checksum-pinned recipe/archive from the owner for a specific historical need.

## Documentation artifacts

Current Markdown, editable C4/Structurizr/class/UI sources, rendered SVGs and the self-contained slideshow are committed. Rebuild them using the independent [pinned Gradle documentation workflow](remediation/design-c4/README.md). The Gradle wrapper/bootstrap and dependency integrity locks remain; downloaded tool distributions, node_modules, screenshots, old ZIPs and local build caches do not.
