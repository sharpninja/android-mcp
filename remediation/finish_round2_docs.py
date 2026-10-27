import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'API-CONTRACT.md';s=p.read_text().replace('`(registry_generation,overlay_generation,query_hash,last_entry_id)`','`(registry_id,registry_generation,overlay_generation,query_hash,last_entry_id)`')
s+='''

## Registry and identity implementation details

The corrected schema supports several immutable registries in one database through registry_entries(registry_id,entry_id). effective_entries is an active-registry view, not a mutable/stored document or ETag table. Builder input must contain catalog.registry_version (positive monotonic publication number), android_version, api_level and coverage; each source explicitly supplies api_level (null when unknown) and channel. A new standalone release seeds registry_generation from registry_version. The serving publisher rejects a new registry_version not greater than its active version, except exact idempotent replay. Cursor tokens also include registry_id, so replacement of a file or a selected source set cannot revive an old cursor. In-database switches increment generation atomically. The builder only publishes its private staging database after all gates; transient staged pointers in that private file are never exposed to serving readers.

idempotency_records is the sole administration replay store, keyed by verified principal, operation and key. legacy_import_jobs contains unscoped historical prototype receipts copied from storage v2; these can never authorize replay of an authenticated API operation. registry_events and registry_outbox preserve publication/approval-invalidation evidence. The bridge and catalog DDL coexist in one transaction domain for serving publication. Storage user_version2 is the immutable original release-002 baseline; v3 is created only through a checked offline copy or a new build.

Dispatch manifests carry schema_hash (canonical identity, kind, arguments, return type and codec identity/status), permission_hash and document_hash separately. Provenance-only additions leave schema_hash stable. Publication still invalidates grants and approvals for changed permission_hash, required semantics, argument mapping, backend authority or target constraints even when schema_hash is unchanged: delete/disable the affected client binding rules in the same policy-revision transaction before the new mapping becomes usable. No document or metadata edit grants authority.

External annotation permission_relations.line is a one-based XML item ordinal, not an XML source line; raw_annotation preserves original item name, archive member and annotation values through annotation_evidence locators. Malformed XML is retained as an annotation-xml issue and the source report becomes partial. Declaration/parameter/unmatched annotation cases are fixture-tested. Unknown/unsupported semantics remain noninvocable.
''';p.write_text(s)
p=C/'README.md';s=p.read_text().replace('Entry schema/API contract version remains `1.0.0`','Entry envelopes support documented `1.x` minor versions (base fields 1.0.0; additions confined to extensions/x-*); administration API is `1.1.0`')
s+='''

Builder inputs now require explicit catalog metadata (`catalog.registry_version`, `android_version`, `api_level`, `coverage`) and per-source `api_level`/`channel`. Use null for a source's unknown API applicability rather than inventing API37/stable for arbitrary evidence or history. See API-CONTRACT.md. Permission exports take a separate new output directory: `python3 catalog/tooling/export_permissions.py RELEASE_DIR NEW_EXPORT_DIR`; they never edit an immutable release.
''';p.write_text(s)
p=C/'tooling/contract_reference.py';s=p.read_text().replace("self.registry_generation=1;self.overlay_generation=0;self.history={}","self.registry_generation=1;self.registry_id='fixture-initial';self.overlay_generation=0;self.history={}")
s=s.replace('        self.registry_generation+=1','        self.registry_generation+=1\n        self.registry_id=digest(records)')
s=s.replace('return (self.registry_generation,self.overlay_generation)','return (self.registry_id,self.registry_generation,self.overlay_generation)')
p.write_text(s)
p=C/'tests/test_remediation.py';s=p.read_text().replace("new=sqlite3.connect(r/'new.sqlite');self.assertEqual", "new=sqlite3.connect(r/'new.sqlite');self.addCleanup(new.close);self.assertEqual");p.write_text(s)
p=C/'tests/test_build.py';s=p.read_text().replace("db=sqlite3.connect(r/'out/catalog.sqlite');rows=", "db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close);rows=");p.write_text(s)
