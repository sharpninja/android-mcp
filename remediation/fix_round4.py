import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
for p in (C/'contracts').glob('*.json'):
 d=json.loads(p.read_text())
 def visit(x):
  if isinstance(x,dict):
   if 'aidl-interface' in x.get('enum',[]):
    for kind in ('aidl-parcelable','aidl-union','aidl-enum'):
     if kind not in x['enum']:x['enum'].append(kind)
   for key,value in x.items():
    if key=='HistoryEvent' and isinstance(value,dict):value['properties']['reason']={'type':'string','description':'Persisted immutable owner mutation justification; absent only for legacy/system history without one.'}
    if key=='Registry' and isinstance(value,dict):value['properties']['api_level']['type']=['integer','null']
    visit(value)
  elif isinstance(x,list):
   for v in x:visit(v)
 visit(d);p.write_text(json.dumps(d,indent=2)+'\n')
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text();anchor='PRAGMA user_version=3;'
extra='''CREATE TRIGGER active_imported_base_insert BEFORE INSERT ON active_registry WHEN EXISTS(SELECT 1 FROM overlays o WHERE o.imported_binding_id IS NOT NULL AND o.state IN ('active','suppressed') AND NOT EXISTS(SELECT 1 FROM registry_entries e WHERE e.registry_id=new.registry_id AND e.entry_id=o.imported_binding_id AND e.record_sha256=o.base_record_hash)) BEGIN SELECT RAISE(ABORT,'Resolve changed or missing imported overlay base before publication'); END;
CREATE TRIGGER active_imported_base_update BEFORE UPDATE OF registry_id ON active_registry WHEN EXISTS(SELECT 1 FROM overlays o WHERE o.imported_binding_id IS NOT NULL AND o.state IN ('active','suppressed') AND NOT EXISTS(SELECT 1 FROM registry_entries e WHERE e.registry_id=new.registry_id AND e.entry_id=o.imported_binding_id AND e.record_sha256=o.base_record_hash)) BEGIN SELECT RAISE(ABORT,'Resolve changed or missing imported overlay base before publication'); END;
CREATE TABLE entry_history_reasons(entry_id TEXT NOT NULL,revision INTEGER NOT NULL,reason TEXT NOT NULL,PRIMARY KEY(entry_id,revision),FOREIGN KEY(entry_id,revision) REFERENCES entry_history(entry_id,revision));
CREATE TRIGGER history_reason_no_update BEFORE UPDATE ON entry_history_reasons BEGIN SELECT RAISE(ABORT,'History justification is append-only'); END;
CREATE TRIGGER history_reason_no_delete BEFORE DELETE ON entry_history_reasons BEGIN SELECT RAISE(ABORT,'History justification is append-only'); END;
'''
if 'CREATE TABLE entry_history_reasons' not in s:s=s.replace(anchor,extra+anchor)
p.write_text(s);(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
p=R/'design/Android MCP Bridge Design.md';s=p.read_text().replace('The JSON constraint schema is closed: exact connection/descriptor/target/display handles, inclusive nonnegative code range, and allowed flag bits; empty target/handle sets deny, and there is no implicit wildcard.','constraints_json is scope-only (allowed_targets, allowed_handles, allowed_displays, max_items); connection, descriptor, inclusive code range, flags mask, contract hash and effect are authoritative SQL columns. Empty scope sets deny, and there is no implicit wildcard.')
s=s.replace('exact raw JSON in x-raw-node','exact parsed JSON value in x-raw-node').replace('binding_occurrences','occurrences')
s+='''

permission_reader.read is the offline future semantic-node reader: unknown operators become validated unknown wrappers with x-raw-node, preserving the parsed JSON value; original lexical bytes remain in immutable source/import evidence. Known malformed ASTs are rejected. Generated extractor AST validation remains strict. No extension/raw node is interpreted as authority. Runtime/HTTP reader integration remains a release gate for bridge implementation.

Catalog source occurrences are occurrences(binding_id,source_id,line), joined to binding_declarations at the same key. External annotation_evidence uses its own source_id/locator key. The design does not require a second binding_occurrences table. Every new owner CRUD mutation persists its reason in append-only entry_history_reasons(entry_id,revision,reason), in the same transaction as entry_history; HistoryEvent joins and returns it. Legacy/system events without supplied justification omit the optional reason rather than fabricate one.
''';p.write_text(s)
p=C/'README.md';s=p.read_text();s+='''

Identity canonicalization v2 covers all source languages: java:owner#method(normalizedTypes), java:owner#field and java:package:qualified.package; java-source: uses the same source-spelled forms (including java-source:package:); aidl:descriptor#method(normalizedTypes), aidl:descriptor#field and aidl:descriptor type roots; jvm:binary/Owner and jvm:binary/Owner#name(JVM-argument-descriptors)JVM-return-descriptor, with jvm:binary/Owner#fieldName where the lifecycle index omits a field descriptor. SHA-256 hashes the complete prefixed string. These remain distinct IDs until explicit proven equivalence is recorded in extensions.proven_current_sdk_jvm_identity; equivalence never silently retargets grants. AIDL parcelable/union/enum roots use additive1.1.0 kinds aidl-parcelable/aidl-union/aidl-enum with unknown wire codecs, not unsupported-source labels after successful declaration parsing.

New owner mutation reasons live in entry_history_reasons, an append-only sidecar keyed to the immutable history revision. HistoryEvent.reason is optional for legacy/system records only; every Mutation reason must be inserted atomically with its corresponding history. Registry.api_level may be null when applicability is not established.
''';p.write_text(s)
p=C/'API-CONTRACT.md';s=p.read_text()+'''

Pointer admission rejects active or suppressed imported overlays whose base_record_hash is missing or differs from the target registry entry. The publication transaction marks those overlays conflict and appends history before selecting the new pointer; tombstones are included. User Mutation.reason is stored in entry_history_reasons and returned as HistoryEvent.reason (legacy/system absence allowed). The sidecar shares entry/revision FK and is append-only; the service inserts both rows in the same transaction. A null Registry.api_level means unknown applicability, not an inferred API level. permission_reader.read demonstrates future-node quarantine while builder-generated ASTs remain strictly validated.
''';p.write_text(s)
print('round004 schema/docs fixes written')
