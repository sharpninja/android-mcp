# Permission export migration

This replaces the existing `tooling/export_permissions.py:run` responsibility in
the current and pinned release-011 producers. It is implementation work under
FR-BUILD-004 / TEST-BUILD-004, not an additional Android permission capability.
Both original source paths remain retained and separately identified in the
authoritative legacy inventory. No completion or historical-byte claim is made.

The operation belongs to the existing Catalog NUKE target, selected by the
`Export` operation parameter and explicit `--source-catalog` selection. The typed registry will remain the sole
source for accepted parameters, dispatch, help and planning. Registry wiring is
added only after the exporter service has native proof.

The exporter must open the selected `catalog.sqlite` through the existing
`CatalogDatabase.OpenExisting(readOnly: true)` engine. It emits the original
three artifacts: `permissions.jsonl`, `permission-expression-samples.json`, and
`permission-coverage.json`. It preserves the SQL UNION name ordering, definition
and symbol rows, explicit null fields, eight original expression logic groups
and at most three samples per group. Names are hashed as the UTF-8 bytes of
`permission:` followed by the exact name, using the shared content hash. Every
summary is validated against the existing `PermissionSummary.schema.json` with
the existing JSON Schema engine. Authority stays `unknown`, MCP grants stay
`not-inferred`, and coverage stays `complete: false`.

Use the shared immutable streaming store for JSONL, shared JSON serialization
and content hashing, and the existing SQLite connection/command authority.
There must be no second database, serializer, schema validator implementation,
filesystem engine or permission inference. Schema parsing/evaluation uses the
already pinned Json.Schema dependency. Preserve originals and any failed partial
output. The final coverage artifact is written only after summaries and samples
succeed. An invalid schema, row, source selection or output scope cannot be
reported as successful export.

Output must be a distinct empty directory outside the sealed release tree. This
enforces the existing immutable-release requirement, including descendants that
the old equality-only guard failed to exclude. Empty existing output is allowed;
existing files are never overwritten. This stronger release-preservation guard
must be explicit in tests and usage. Canonical C# JSON serialization receives a
new producer identity; no historical byte identity is inferred from equivalent
JSON values or identical source copies.

Mock-first coverage must include empty input; manifest-only, constant-only and
overlapping names; repeated source definitions; symbol order and null protection
levels; all eight logic groups and the three-sample bound; raw annotation null
versus JSON value; malformed attributes/expressions/raw annotations; schema
failure; read-only input and input hash retention; same/descendant/nonempty
output; existing output conflicts; cancellation and retained partial failure;
unknown authority fields; exact file/count/size/hash reporting; and each failed
assertion's independent negative control. Use the SAME cases for native SQLite
and store bindings. Then prove the actual category CLI on a small retained
catalog fixture, refresh normal method/file accounting from that evidence, and
include the unfiltered current suite before the next publication checkpoint.

No Python, custom Node, GitHub Actions or external reviewer is executed. This
plan does not introduce an HV gate before PR Ready.
