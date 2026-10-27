# Source tooling boundary for the Kotlin/Gradle project

The server and UI are Kotlin. Python is an offline extraction and validation
tool, and is not a server runtime dependency. Canonical Gradle and Structurizr/SVG
tasks belong to the design owner; this file describes the source-side entrypoints
to wrap. It does not implement or run a Gradle/Android project.

The main portable command interface, from a frozen `catalog/` tree, is:

```text
<configured-python> catalog/tooling/build_catalog.py <inventory.json> <new-output-directory>
<configured-python> catalog/tooling/validate_catalog.py <output-directory> <inventory.json> <validation-report.json>
<configured-python> catalog/tooling/export_permissions.py <output-directory> <new-permission-export-directory>
```

The configured Python environment and exact extraction dependencies must match
the frozen receipt. No task should silently install/upgrade tools, accept SDK
licenses, authenticate, retrieve private material, or operate a device. Public
source acquisition is a separate explicit task with pinned URLs/hashes and
rate-limit receipts; ordinary validation should be offline.

Suggested Gradle adapters are `catalogExtract`, `catalogValidate`, and
`catalogExportPermissions`. Their actual canonical names are for the Gradle owner
to choose. Declare the complete inventory, producer/contracts/migrations,
dependency versions, source byte hashes, cache fingerprints and metadata proof
inputs. Java/SDK declaration caches must be rejected when their producer
fingerprint differs. New interface/callback/group helpers change processing
provenance; they do not inherently change the Java/SDK declaration-cache producers.

Extraction refuses a populated output directory. It retains staging and failure
evidence and exposes `catalog.sqlite` only after successful schema, integrity,
foreign-key and export gates. Gradle must not turn a failed/partial staging file
into a runtime publication or overwrite an earlier immutable release. Validation
and permission export read the published database without modifying it.

Runtime interchange is SQLite plus UTF-8 JSON/JSONL, with versioned schemas and
plain JSON permission expressions. IDs/hashes are strings; normalized APEX version
codes are decimal strings. Unknown source data and raw expressions stay explicit;
Kotlin readers must preserve unknown fields/operators and apply the contract's
unsupported/deny behavior. Source metadata is not OS or per-client authority.

The callback, functional taxonomy, APEX and exact interface predicates are
source-producer handoffs under `integration/*-actions.json`. Their measured
release010 baselines are not outputs of the running release011, and their future
builder/schema integration requires a new frozen fixture/production snapshot.
Descriptions and unproven registration, lifecycle, installed-module and runtime
facts must not be synthesized as source evidence.

Documentation remains Markdown. This source task creates no Word/PDF output or
new diagrams. The design owner owns Gradle-driven Structurizr SVG exports and
the internal Kotlin ARC protocol compatibility contract.
