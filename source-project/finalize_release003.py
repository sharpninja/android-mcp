"""Write validated release documentation and a streaming file-hash manifest."""
import pathlib,json,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;O=R/'catalog/output/release-003'
validation=json.loads((O/'validation-report.json').read_text());permissions=json.loads((O/'permission-coverage.json').read_text());coverage=json.loads((R/'coverage-manifest-release003.json').read_text())
assert validation['integrity_check']=='ok' and not validation['foreign_key_check'] and not validation['json_schema_failures']
def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  while b:=f.read(4*1024*1024):h.update(b)
 return h.hexdigest()
text=f'''# Android catalog release 003 — populated PARTIAL snapshot

Primary runtime file: `catalog.sqlite`. This release contains **{validation['entries_validated']:,} discovery entries** across declared signature, Java-source, JVM-index and AIDL namespaces. Counts are not a claim of distinct callable or supported runtime APIs. All imported source occurrences remain available in `binding_declarations` and `permission_relations`.

Google's stable metadata identifies **platform API 37.2**, **package revision 1**, **SDK Extension 24**. The complete official source-data archive (16,910 files, including all 16,893 Java files) is pinned by published SHA-1 and local SHA-256. AST extraction produced 530,028 declaration records with zero grammar-error regions. The published API lifecycle index provides 90,272 current descriptors and identifies 3,409 historical/future records excluded from that current view. Earlier API37.0 public signatures are retained as version-specific evidence.

The combined frozen corpus contains {coverage['source_paths']['by_status']['downloaded']:,} downloaded paths of {coverage['source_paths']['total']:,} enumerated paths. The bounded AOSP17r1 corpus has {coverage['aosp_paths']['by_status']['downloaded']:,} of {coverage['aosp_paths']['selected']:,} selected paths downloaded. Excluded projects and exact missing paths are enumerated; unknown coverage is never zero. This release is **not an all-current-completeness claim**.

Permission export: `{permissions['permission_names']:,}` names, `{permissions['manifest_definition_records']:,}` manifest-definition occurrences, `{permissions['manifest_defined_names']:,}` distinct manifest-defined names. The pinned core manifest contributes 1,160 definition elements. Definitions, constants, source requirements, OS authority and MCP client policy are separate. Missing metadata means unknown. Source protection flags do not grant access.

## Source semantics and identities

`java:` retains exact Metalava signature spelling; `java-source:` retains source declaration spelling; `jvm:` retains exact published JVM owner/member descriptors; `aidl:` retains proven AIDL descriptor/method syntax. Proven source-to-JVM links appear in extensions; permission metadata is linked only when the exact descriptor is proven. Unresolved wildcard/type-variable identities remain separate and unknown. Source declarations include hidden/private/package members; visibility alone is not proof of public SDK membership.

The lifecycle index omits Java type kind, field types, constants, generics, throws and annotations. Generated Manifest/R declarations absent from the source archive retain those unknowns. Every imported AIDL schema is retained raw. Explicit AIDL method IDs are source evidence; wire transaction codes and codecs remain unknown unless separately proven. Unsupported structured bodies/import resolution are quantified in reports.

## Validation and use

`validation-report.json` records exhaustive Entry JSON Schema validation, hashes/ETags, integrity/FK checks, duplicate occurrence checks, export reconciliation and source reconciliation. `validation.json` records pre-publication checks. The runtime database is atomically published only after schema checks and all exports succeed. Failed stages retain their staging files and never publish a runtime filename.

The current extraction regression suite has 34 passing tests. Native-parser recovery uses indexed Point access and task-local Tree-sitter0.25.2 / tree-sitter-java0.23.5. Original task-local Tree-sitter0.26.0 was replaced before the later no-dependency-change instruction; no system installation changed. Recovery evidence and failing-before/passing-after fixtures are included separately.

Use indexed `bindings` lookups, exact `binding_declarations`, `permission_definitions`, `permission_symbols`, `permission_relations`, `annotation_evidence`, `aidl_schemas` and `parse_reports`. `runtime_catalog` projects imported entries and local overrides/tombstones. Consumers must select source scope/version; test and frozen declarations are retained. Static metadata does not authorize invocation. No bridge application, device operation, HTTP service, SDK installation or license acceptance was performed.

Storage DDL remains version1.1.0 / SQLite user_version2; Entry/API contracts remain1.0.0. These are the existing catalog contracts plus the tested publication gate, not a claim that the separate design/schema/CRUD remediation loop has completed. `openapi.json` is a specification, not an implemented service.

## Reproduction

Use the task-local pinned packages in `requirements-extraction.txt`. Tests: `python -m unittest discover -s catalog/tests -v`. `catalog/tooling/restore_snapshot.py source-inventory-release003.json` checks exact acquired bytes without network; add `--download` only for permitted restoration. It uses source-data archives and bounded metadata ranges, never the platform binary payload. Existing corrupt bytes fail validation; denial stops retrieval,429 honors Retry-After/backoff.

If declaration caches are absent, restore source bytes, run `stage_sdk372_sources.py` and `stage_sdk372_index.py`, then freeze a NEW combined inventory with `merge_catalog_inventory.py`. Rebuilt compression/timestamps may change cache/container hashes; declaration/source hashes and exact source versions remain explicit. Never overwrite the frozen release inventory. Build to an empty output directory with `catalog/tooling/build_catalog.py`, then run `validate_catalog.py` and `export_permissions.py`. No byte-identical SQLite/container claim is made because publication timestamps differ.

Bulk raw sources remain preserved locally and reproducibly addressed by the inventory. Delivery variants may omit those bulk bytes. `ATTRIBUTION.md`, archive receipts, source inventory and coverage manifest remain authoritative for provenance and limitations. No new Claude review was run by this source/data task; earlier hostile-validation scores apply to an older frozen prototype.
'''
(O/'README.md').write_text(text)
paths=[p for p in O.iterdir() if p.is_file() and p.name!='release-manifest.json']
manifest=dict(release='003',created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,files=[dict(path=p.name,bytes=p.stat().st_size,sha256=digest(p)) for p in sorted(paths)],inventory=dict(path='source-inventory-release003.json',sha256=digest(R/'source-inventory-release003.json')),coverage=dict(path='coverage-manifest-release003.json',sha256=digest(R/'coverage-manifest-release003.json')))
(O/'release-manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps(dict(release='003',entries=validation['entries_validated'],permissions=permissions,sqlite=next(f for f in manifest['files'] if f['path']=='catalog.sqlite')),indent=2))
